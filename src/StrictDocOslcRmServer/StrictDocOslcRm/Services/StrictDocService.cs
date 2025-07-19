using Microsoft.Extensions.Caching.Memory;
using StrictDocOslcRm.Models;
using System.Text.Json;
using OSLC4Net.Domains.RequirementsManagement;
using OSLC4Net.Core.Model;

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
        _jsonFilePath = configuration["StrictDoc:JsonFilePath"] ?? 
                       "/Users/ezandbe/code/a/oslc/oslc4net-misc/strictdoc-oslc/docs/example-composite.json";
    }

    public async Task<List<StrictDocDocument>> GetDocumentsAsync()
    {
        if (_cache.TryGetValue(DocumentsCacheKey, out List<StrictDocDocument>? cachedDocuments))
        {
            return cachedDocuments!;
        }

        var documents = await LoadDocumentsFromFileAsync();
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

        var requirements = await GetAllRequirementsAsync();
        var requirement = requirements.FirstOrDefault(r => r.Identifier == uid);

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
        var documents = await GetDocumentsAsync();
        var targetDocument = documents.FirstOrDefault(d => d.Mid == documentMid);
        
        if (targetDocument == null)
        {
            _logger.LogWarning("Document with MID {DocumentMid} not found", documentMid);
            return new List<Requirement>();
        }

        // Extract requirements only from this specific document
        var requirements = ExtractRequirementsFromNodes(targetDocument.Nodes, targetDocument.Mid, targetDocument.Title, baseUrl);
        _logger.LogInformation("Found {Count} requirements for document {DocumentMid} ({Title})", 
            requirements.Count, documentMid, targetDocument.Title);
        
        return requirements;
    }

    public async Task<List<Requirement>> GetAllRequirementsAsync(string? baseUrl = null)
    {
        var cacheKey = RequirementsCacheKey + (baseUrl != null ? $"_{baseUrl.GetHashCode()}" : "");
        
        if (_cache.TryGetValue(cacheKey, out List<Requirement>? cachedRequirements))
        {
            return cachedRequirements!;
        }

        var documents = await GetDocumentsAsync();
        var requirements = new List<Requirement>();

        foreach (var document in documents)
        {
            var docRequirements = ExtractRequirementsFromNodes(document.Nodes, document.Mid, document.Title, baseUrl);
            requirements.AddRange(docRequirements);
        }

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

            var jsonContent = await File.ReadAllTextAsync(_jsonFilePath);
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

    private List<Requirement> ExtractRequirementsFromNodes(List<StrictDocNode> nodes, string documentMid, string documentTitle, string? baseUrl = null)
    {
        var requirements = new List<Requirement>();

        foreach (var node in nodes)
        {
            if (node.NodeType == StrictDocNodeTypes.Requirement && !string.IsNullOrEmpty(node.Uid))
            {
                var requirement = CreateRequirementFromNode(node, documentMid, documentTitle, baseUrl);
                requirements.Add(requirement);
            }
            else if (node.NodeType == StrictDocNodeTypes.CompositeRequirement)
            {
                // TODO: Implement RequirementCollection mapping
                _logger.LogInformation("Composite requirement found but not yet implemented: {Title}", node.Title);
            }

            // Recursively process child nodes
            if (node.Nodes != null)
            {
                var childRequirements = ExtractRequirementsFromNodes(node.Nodes, documentMid, documentTitle, baseUrl);
                requirements.AddRange(childRequirements);
            }
        }

        return requirements;
    }

    private Requirement CreateRequirementFromNode(StrictDocNode node, string documentMid, string documentTitle, string? baseUrl = null)
    {
        var requirement = new Requirement();
        
        // Map UID to Identifier and URI
        requirement.Identifier = node.Uid;
        
        // Map TITLE to Title
        requirement.Title = node.Title;
        
        // Map STATEMENT to Description
        requirement.Description = node.Statement;

        // Process RELATIONS with type PARENT to Decomposes property
        if (node.Relations != null)
        {
            var parentRelations = node.Relations
                .Where(r => r.Type.Equals(StrictDocRelationTypes.Parent, StringComparison.OrdinalIgnoreCase))
                .Select(r => r.Value)
                .Where(v => !string.IsNullOrEmpty(v))
                .ToList();

            if (parentRelations.Any())
            {
                // Set Decomposes property with parent requirement URIs using new format
                var decomposes = parentRelations
                    .Select(parentUid => 
                    {
                        if (!string.IsNullOrEmpty(baseUrl))
                        {
                            return new Uri($"{baseUrl}/?a={parentUid}");
                        }
                        else
                        {
                            // Fallback to old format if baseUrl not provided
                            return new Uri($"http://strictdoc.local/?a={parentUid}");
                        }
                    })
                    .ToArray();
                requirement.Decomposes = new HashSet<Uri>(decomposes);
            }
        }

        return requirement;
    }
}
