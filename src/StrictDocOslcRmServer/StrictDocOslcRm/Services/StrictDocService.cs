using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using OSLC4Net.Domains.RequirementsManagement;
using StrictDocOslcRm.Models;

namespace StrictDocOslcRm.Services;

/// <summary>
/// Service for loading and parsing StrictDoc JSON data
/// </summary>
public interface IStrictDocService
{
    Task<List<StrictDocDocument>> GetDocumentsAsync();
    Task<Requirement?> GetRequirementByUidAsync(string uid);
    Task<List<Requirement>> GetRequirementsForDocumentAsync(string documentMid, string? baseUrl = null);
    Task<List<Requirement>> GetAllRequirementsAsync(string? baseUrl = null);
}

/// <summary>
/// Implementation of IStrictDocService with caching
/// </summary>
public class StrictDocService : IStrictDocService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<StrictDocService> _logger;
    private readonly string _jsonFilePath;
    private const string DocumentsCacheKey = "strictdoc_documents";
    private const string RequirementsCacheKey = "strictdoc_requirements";
    private const string RequirementByUidCachePrefix = "strictdoc_req_";

    public StrictDocService(IMemoryCache cache, ILogger<StrictDocService> logger, IConfiguration configuration)
    {
        _cache = cache;
        _logger = logger;
        _jsonFilePath = configuration["StrictDoc:JsonFilePath"] ?? throw new ArgumentNullException(nameof(configuration), "StrictDoc:JsonFilePath configuration is required");
    }

    public async Task<List<StrictDocDocument>> GetDocumentsAsync()
    {
        if (_cache.TryGetValue(DocumentsCacheKey, out List<StrictDocDocument>? cachedDocuments))
        {
            return cachedDocuments!;
        }

        var documents = await LoadDocumentsFromFileAsync().ConfigureAwait(false);
        _cache.Set(DocumentsCacheKey, documents, TimeSpan.FromHours(1));
        return documents;
    }

    public async Task<Requirement?> GetRequirementByUidAsync(string uid)
    {
        var cacheKey = RequirementByUidCachePrefix + uid;

        if (_cache.TryGetValue(cacheKey, out Requirement? cachedRequirement))
        {
            return cachedRequirement;
        }

        // Check if we have negative cache (requirement doesn't exist)
        var negativeCacheKey = cacheKey + "_negative";
        if (_cache.TryGetValue(negativeCacheKey, out _))
        {
            return null;
        }

        var requirements = await GetAllRequirementsAsync().ConfigureAwait(false);
        var requirement = requirements.FirstOrDefault(r => string.Equals(r.Identifier, uid, StringComparison.Ordinal));

        if (requirement != null)
        {
            _cache.Set(cacheKey, requirement, TimeSpan.FromHours(1));
        }
        else
        {
            // Negative caching - cache the fact that this UID doesn't exist
            _cache.Set(negativeCacheKey, true, TimeSpan.FromMinutes(30));
        }

        return requirement;
    }

    public async Task<List<Requirement>> GetRequirementsForDocumentAsync(string documentMid, string? baseUrl = null)
    {
        var documents = await GetDocumentsAsync().ConfigureAwait(false);
        var targetDocument = documents.FirstOrDefault(d => string.Equals(d.Mid, documentMid, StringComparison.Ordinal));

        if (targetDocument == null)
        {
            _logger.LogWarning("Document with MID {DocumentMid} not found", documentMid);
            return new List<Requirement>();
        }

        var targetUids = FlattenRequirementNodes(targetDocument.Nodes)
            .Select(node => node.Uid!)
            .ToHashSet(StringComparer.Ordinal);
        var requirements = ExtractRequirementsFromNodes(
                documents.SelectMany(document => document.Nodes).ToList(),
                baseUrl)
            .Where(requirement => requirement.Identifier is not null && targetUids.Contains(requirement.Identifier))
            .ToList();
        _logger.LogInformation("Found {Count} requirements for document {DocumentMid} ({Title})",
            requirements.Count, documentMid, targetDocument.Title);

        return requirements;
    }

    public async Task<List<Requirement>> GetAllRequirementsAsync(string? baseUrl = null)
    {
        var cacheKey = RequirementsCacheKey + (baseUrl != null ? $"_{StringComparer.Ordinal.GetHashCode(baseUrl)}" : "");

        if (_cache.TryGetValue(cacheKey, out List<Requirement>? cachedRequirements))
        {
            return cachedRequirements!;
        }

        var documents = await GetDocumentsAsync().ConfigureAwait(false);
        var requirements = ExtractRequirementsFromNodes(
            documents.SelectMany(document => document.Nodes).ToList(),
            baseUrl);

        _cache.Set(cacheKey, requirements, TimeSpan.FromHours(1));
        return requirements;
    }

    private async Task<List<StrictDocDocument>> LoadDocumentsFromFileAsync()
    {
        try
        {
            if (!File.Exists(_jsonFilePath))
            {
                _logger.LogWarning("StrictDoc JSON file not found at {FilePath}", _jsonFilePath);
                return new List<StrictDocDocument>();
            }

            var jsonContent = await File.ReadAllTextAsync(_jsonFilePath).ConfigureAwait(false);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var strictDocData = JsonSerializer.Deserialize<StrictDocData>(jsonContent, options);
            return strictDocData?.Documents ?? new List<StrictDocDocument>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading StrictDoc data from {FilePath}", _jsonFilePath);
            return new List<StrictDocDocument>();
        }
    }

    private static List<Requirement> ExtractRequirementsFromNodes(
        List<StrictDocNode> nodes, string? baseUrl = null)
    {
        var requirementNodes = FlattenRequirementNodes(nodes).ToList();
        var requirementsByUid = requirementNodes.ToDictionary(
            node => node.Uid!,
            CreateRequirementFromNode,
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

    private static Requirement CreateRequirementFromNode(StrictDocNode node)
    {
        var requirement = new Requirement();

        // Map UID to Identifier and URI
        requirement.Identifier = node.Uid ?? throw new InvalidOperationException("Node UID is required for requirement mapping");

        // Map TITLE to Title
        requirement.Title = node.Title ?? "No Title";

        // Map STATEMENT to Description
        requirement.Description = node.Statement ?? "No Description";

        return requirement;
    }
}
