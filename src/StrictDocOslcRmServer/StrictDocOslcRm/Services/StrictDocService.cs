using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using OSLC4Net.Domains.RequirementsManagement;
using StrictDocOslcRm.Models;

namespace StrictDocOslcRm.Services;

public interface IStrictDocService
{
    Task<List<StrictDocDocument>> GetDocumentsAsync(
        ConfigurationContext context,
        CancellationToken cancellationToken = default);

    Task<Requirement?> GetRequirementByUidAsync(
        string uid,
        ConfigurationContext context,
        string? baseUrl = null,
        CancellationToken cancellationToken = default);

    Task<List<Requirement>> GetRequirementsForDocumentAsync(
        string documentMid,
        ConfigurationContext context,
        string? baseUrl = null,
        CancellationToken cancellationToken = default);

    Task<List<Requirement>> GetAllRequirementsAsync(
        ConfigurationContext context,
        string? baseUrl = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Loads StrictDoc snapshots from the selected local OSLC configuration context.
/// </summary>
public sealed class StrictDocService(IMemoryCache cache, ILogger<StrictDocService> logger)
    : IStrictDocService
{
    public async Task<List<StrictDocDocument>> GetDocumentsAsync(
        ConfigurationContext context,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"strictdoc_documents_{context.Identifier}_{GetSnapshotVersion(context)}";
        if (cache.TryGetValue(cacheKey, out List<StrictDocDocument>? cachedDocuments))
        {
            return cachedDocuments!;
        }

        var documents = await LoadDocumentsFromFileAsync(context, cancellationToken).ConfigureAwait(false);
        cache.Set(cacheKey, documents, TimeSpan.FromMinutes(5));
        return documents;
    }

    public async Task<Requirement?> GetRequirementByUidAsync(
        string uid,
        ConfigurationContext context,
        string? baseUrl = null,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"strictdoc_requirement_{context.Identifier}_{GetSnapshotVersion(context)}_{baseUrl}_{uid}";
        if (cache.TryGetValue(cacheKey, out Requirement? cachedRequirement))
        {
            return cachedRequirement;
        }

        var requirement = (await GetAllRequirementsAsync(context, baseUrl, cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(candidate => string.Equals(candidate.Identifier, uid, StringComparison.Ordinal));
        if (requirement is not null)
        {
            cache.Set(cacheKey, requirement, TimeSpan.FromMinutes(5));
        }

        return requirement;
    }

    public async Task<List<Requirement>> GetRequirementsForDocumentAsync(
        string documentMid,
        ConfigurationContext context,
        string? baseUrl = null,
        CancellationToken cancellationToken = default)
    {
        var documents = await GetDocumentsAsync(context, cancellationToken).ConfigureAwait(false);
        var targetDocument = documents.FirstOrDefault(document =>
            string.Equals(document.Mid, documentMid, StringComparison.Ordinal));

        if (targetDocument is null)
        {
            logger.LogWarning(
                "Document with MID {DocumentMid} was not found in configuration {Configuration}",
                documentMid,
                context.Identifier);
            return [];
        }

        var requirements = ExtractRequirementsFromNodes(
            targetDocument.Nodes,
            targetDocument.Mid,
            targetDocument.Title,
            baseUrl);
        logger.LogInformation(
            "Found {Count} requirements for document {DocumentMid} in configuration {Configuration}",
            requirements.Count,
            documentMid,
            context.Identifier);
        return requirements;
    }

    public async Task<List<Requirement>> GetAllRequirementsAsync(
        ConfigurationContext context,
        string? baseUrl = null,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"strictdoc_requirements_{context.Identifier}_{GetSnapshotVersion(context)}_{baseUrl}";
        if (cache.TryGetValue(cacheKey, out List<Requirement>? cachedRequirements))
        {
            return cachedRequirements!;
        }

        var documents = await GetDocumentsAsync(context, cancellationToken).ConfigureAwait(false);
        var requirements = new List<Requirement>();
        foreach (var document in documents)
        {
            requirements.AddRange(ExtractRequirementsFromNodes(
                document.Nodes,
                document.Mid,
                document.Title,
                baseUrl));
        }

        cache.Set(cacheKey, requirements, TimeSpan.FromMinutes(5));
        return requirements;
    }

    private static long GetSnapshotVersion(ConfigurationContext context) =>
        File.Exists(context.StrictDocPath)
            ? File.GetLastWriteTimeUtc(context.StrictDocPath).Ticks
            : 0;

    private async Task<List<StrictDocDocument>> LoadDocumentsFromFileAsync(
        ConfigurationContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(context.StrictDocPath))
            {
                logger.LogWarning(
                    "StrictDoc JSON snapshot was not found at {FilePath} for configuration {Configuration}",
                    context.StrictDocPath,
                    context.Identifier);
                return [];
            }

            var jsonContent = await File.ReadAllTextAsync(context.StrictDocPath, cancellationToken)
                .ConfigureAwait(false);
            var strictDocData = JsonSerializer.Deserialize<StrictDocData>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return strictDocData?.Documents ?? [];
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Error loading StrictDoc JSON snapshot from {FilePath} for configuration {Configuration}",
                context.StrictDocPath,
                context.Identifier);
            return [];
        }
    }

    private List<Requirement> ExtractRequirementsFromNodes(
        List<StrictDocNode> nodes,
        string documentMid,
        string documentTitle,
        string? baseUrl = null)
    {
        _ = documentMid;
        _ = documentTitle;
        var requirements = new List<Requirement>();

        foreach (var node in nodes)
        {
            if (string.Equals(node.NodeType, StrictDocNodeTypes.Requirement, StringComparison.Ordinal) &&
                !string.IsNullOrEmpty(node.Uid))
            {
                requirements.Add(CreateRequirementFromNode(node, baseUrl));
            }
            else if (string.Equals(node.NodeType, StrictDocNodeTypes.CompositeRequirement, StringComparison.Ordinal))
            {
                // REVISIT: Map StrictDoc composite requirements to OSLC RM RequirementCollection
                // resources when the server publishes collection identities and membership semantics.
                logger.LogInformation("Composite requirement found but not yet mapped: {Title}", node.Title);
            }

            if (node.Nodes is not null)
            {
                requirements.AddRange(ExtractRequirementsFromNodes(node.Nodes, documentMid, documentTitle, baseUrl));
            }
        }

        return requirements;
    }

    private static Requirement CreateRequirementFromNode(StrictDocNode node, string? baseUrl = null)
    {
        var requirement = new Requirement
        {
            Identifier = node.Uid ?? throw new InvalidOperationException("Node UID is required for requirement mapping"),
            Title = node.Title ?? "No Title",
            Description = node.Statement ?? "No Description"
        };

        if (!string.IsNullOrEmpty(baseUrl))
        {
            requirement.InstanceShape = new Uri($"{baseUrl}/oslc/shapes/requirement");
        }

        if (node.Relations is not null)
        {
            var decomposes = node.Relations
                .Where(relation => relation.Type.Equals(StrictDocRelationTypes.Parent, StringComparison.OrdinalIgnoreCase))
                .Select(relation => relation.Value)
                .Where(value => !string.IsNullOrEmpty(value))
                .Select(parentUid => new Uri($"{baseUrl ?? "http://strictdoc.local"}/?a={parentUid}"))
                .ToArray();
            if (decomposes.Length > 0)
            {
                requirement.Decomposes = [.. decomposes];
            }
        }

        return requirement;
    }
}
