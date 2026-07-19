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

        var targetUids = FlattenRequirementNodes(targetDocument.Nodes)
            .Select(node => node.Uid!)
            .ToHashSet(StringComparer.Ordinal);
        var requirements = ExtractRequirementsFromNodes(
                documents.SelectMany(document => document.Nodes).ToList(),
                baseUrl)
            .Where(requirement => requirement.Identifier is not null && targetUids.Contains(requirement.Identifier))
            .ToList();
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
        var requirements = ExtractRequirementsFromNodes(
            documents.SelectMany(document => document.Nodes).ToList(),
            baseUrl);

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

    private static List<Requirement> ExtractRequirementsFromNodes(
        List<StrictDocNode> nodes, string? baseUrl = null)
    {
        var requirementNodes = FlattenRequirementNodes(nodes).ToList();
        var requirementsByUid = requirementNodes.ToDictionary(
            node => node.Uid!,
            node => CreateRequirementFromNode(node, baseUrl),
            StringComparer.Ordinal);

        foreach (var sourceNode in requirementNodes)
        {
            var source = requirementsByUid[sourceNode.Uid!];
            foreach (var relation in sourceNode.Relations ?? [])
            {
                if (!relation.Type.Equals(StrictDocRelationTypes.Parent, StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(relation.Value))
                {
                    continue;
                }

                var targetUid = relation.Value.Trim();
                requirementsByUid.TryGetValue(targetUid, out var target);
                ApplyRmRelation(source, target, targetUid, relation.Role, baseUrl);
            }
        }

        return requirementNodes.Select(node => requirementsByUid[node.Uid!]).ToList();
    }

    private static IEnumerable<StrictDocNode> FlattenRequirementNodes(IEnumerable<StrictDocNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (string.Equals(node.NodeType, StrictDocNodeTypes.Requirement, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(node.Uid))
            {
                yield return node;
            }

            if (node.Nodes is not null)
            {
                foreach (var child in FlattenRequirementNodes(node.Nodes))
                {
                    yield return child;
                }
            }
        }
    }

    private static void ApplyRmRelation(Requirement source, Requirement? target, string targetUid,
        string? role, string? baseUrl)
    {
        var sourceUri = RequirementUri(source.Identifier!, baseUrl);
        var targetUri = RequirementUri(targetUid, baseUrl);
        switch (role?.Trim().ToLowerInvariant())
        {
            case null:
            case "":
            case "decomposes":
                source.Decomposes.Add(targetUri);
                target?.DecomposedBy.Add(sourceUri);
                break;
            case "decomposed by":
                source.DecomposedBy.Add(targetUri);
                target?.Decomposes.Add(sourceUri);
                break;
            case "elaborates":
                source.Elaborates.Add(targetUri);
                target?.ElaboratedBy.Add(sourceUri);
                break;
            case "elaborated by":
                source.ElaboratedBy.Add(targetUri);
                target?.Elaborates.Add(sourceUri);
                break;
            case "specifies":
                source.Specifies.Add(targetUri);
                target?.SpecifiedBy.Add(sourceUri);
                break;
            case "specified by":
                source.SpecifiedBy.Add(targetUri);
                target?.Specifies.Add(sourceUri);
                break;
            case "constrains":
                source.Constrains.Add(targetUri);
                target?.ConstrainedBy.Add(sourceUri);
                break;
            case "constrained by":
                source.ConstrainedBy.Add(targetUri);
                target?.Constrains.Add(sourceUri);
                break;
            case "satisfies":
                source.Satisfies.Add(targetUri);
                target?.SatisfiedBy.Add(sourceUri);
                break;
            case "satisfied by":
                source.SatisfiedBy.Add(targetUri);
                target?.Satisfies.Add(sourceUri);
                break;
        }
    }

    private static Uri RequirementUri(string uid, string? baseUrl) =>
        new($"{baseUrl ?? "http://strictdoc.local"}/?a={Uri.EscapeDataString(uid)}");

    private static Requirement CreateRequirementFromNode(StrictDocNode node, string? baseUrl)
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

        return requirement;
    }
}
