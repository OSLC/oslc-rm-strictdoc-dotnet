using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OSLC4Net.Core.Model;
using OSLC4Net.Domains.RequirementsManagement;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Writing;
using RmConstants = OSLC4Net.Domains.RequirementsManagement.Constants;

namespace StrictDocOslcRm.Services;

public interface ILinkSidecarService
{
    Task ReplaceLinksAsync(
        Uri resourceUri,
        string? contentType,
        string rdfBody,
        Uri publicBaseUri,
        CancellationToken cancellationToken = default);

    Task ApplyLinksAsync(
        Requirement requirement,
        Uri resourceUri,
        CancellationToken cancellationToken = default);

    Task<string?> GetNTriplesAsync(Uri resourceUri, CancellationToken cancellationToken = default);

    Task<string?> GetResourceNTriplesAsync(Uri resourceUri, CancellationToken cancellationToken = default);
}

public sealed class FileLinkSidecarService(
    IConfiguration configuration,
    ILogger<FileLinkSidecarService> logger) : ILinkSidecarService
{
    private const string RdfValue = "http://www.w3.org/1999/02/22-rdf-syntax-ns#value";
    private const string RdfSubject = OslcConstants.RDF_NAMESPACE + "subject";
    private const string RdfPredicate = OslcConstants.RDF_NAMESPACE + "predicate";
    private const string RdfObject = OslcConstants.RDF_NAMESPACE + "object";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly Uri AffectedBy = new(RmConstants.Domains.RM.P.AffectedBy);
    private static readonly Uri ImplementedBy = new(RmConstants.Domains.RM.P.ImplementedBy);
    private static readonly Uri TrackedBy = new(RmConstants.Domains.RM.P.TrackedBy);
    private static readonly Uri ValidatedBy = new(RmConstants.Domains.RM.P.ValidatedBy);
    private static readonly Uri SatisfiedBy = new(RmConstants.Domains.RM.P.SatisfiedBy);

    private static readonly HashSet<string> WritablePredicateUris =
    [
        AffectedBy.AbsoluteUri,
        ImplementedBy.AbsoluteUri,
        TrackedBy.AbsoluteUri,
        ValidatedBy.AbsoluteUri,
        SatisfiedBy.AbsoluteUri
    ];

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly string _storePath = configuration["LinkSidecars:StorePath"]
        ?? "/data/sidecars/link-sidecars.json";

    public async Task ReplaceLinksAsync(
        Uri resourceUri,
        string? contentType,
        string rdfBody,
        Uri publicBaseUri,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resourceUri);
        ArgumentNullException.ThrowIfNull(publicBaseUri);

        if (string.IsNullOrWhiteSpace(rdfBody))
        {
            throw new InvalidDataException("PUT body must contain an RDF representation.");
        }

        var incoming = ParseIncomingGraph(resourceUri, contentType, rdfBody);
        var sidecarGraph = ExtractWritableSidecarGraph(incoming, resourceUri, publicBaseUri);
        var ntriples = SerializeNTriples(sidecarGraph);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var store = await LoadStoreAsync(cancellationToken).ConfigureAwait(false);
            var key = resourceUri.AbsoluteUri;

            if (sidecarGraph.Triples.Count == 0)
            {
                store.Remove(key);
                logger.LogInformation("Removed empty link sidecar for {ResourceUri}", resourceUri);
            }
            else
            {
                store[key] = new LinkSidecarEntry
                {
                    NTriples = ntriples,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                logger.LogInformation(
                    "Stored {TripleCount} sidecar RDF triples for {ResourceUri}",
                    sidecarGraph.Triples.Count,
                    resourceUri);
            }

            await SaveStoreAsync(store, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ApplyLinksAsync(
        Requirement requirement,
        Uri resourceUri,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requirement);
        ArgumentNullException.ThrowIfNull(resourceUri);

        var ntriples = await GetNTriplesAsync(resourceUri, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(ntriples))
        {
            return;
        }

        var graph = new Graph { BaseUri = resourceUri };
        using var reader = new StringReader(ntriples);
        new NTriplesParser().Load(graph, reader);

        var rdfValuesBySubject = graph.Triples
            .Where(triple =>
                triple.Subject is IUriNode &&
                triple.Predicate is IUriNode predicate &&
                predicate.Uri.AbsoluteUri == RdfValue &&
                triple.Object is IUriNode)
            .ToDictionary(
                triple => ((IUriNode)triple.Subject).Uri.AbsoluteUri,
                triple => ((IUriNode)triple.Object).Uri,
                StringComparer.Ordinal);

        foreach (var triple in graph.Triples)
        {
            if (triple.Subject is not IUriNode subject ||
                subject.Uri.AbsoluteUri != resourceUri.AbsoluteUri ||
                triple.Predicate is not IUriNode predicate)
            {
                continue;
            }

            if (!WritablePredicateUris.Contains(predicate.Uri.AbsoluteUri))
            {
                continue;
            }

            var target = ExtractLinkedResource(triple.Object, rdfValuesBySubject);
            if (target == null)
            {
                continue;
            }

            AddRequirementLink(requirement, predicate.Uri, target);
        }
    }

    public async Task<string?> GetNTriplesAsync(Uri resourceUri, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var store = await LoadStoreAsync(cancellationToken).ConfigureAwait(false);
            return store.TryGetValue(resourceUri.AbsoluteUri, out var entry) ? entry.NTriples : null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<string?> GetResourceNTriplesAsync(Uri resourceUri, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var store = await LoadStoreAsync(cancellationToken).ConfigureAwait(false);
            var result = new Graph { BaseUri = resourceUri };

            foreach (var entry in store.Values)
            {
                if (string.IsNullOrWhiteSpace(entry.NTriples))
                {
                    continue;
                }

                var graph = new Graph { BaseUri = resourceUri };
                using var reader = new StringReader(entry.NTriples);
                new NTriplesParser().Load(graph, reader);

                foreach (var triple in graph.Triples.Where(triple => IsUri(triple.Subject, resourceUri)))
                {
                    CopyTriple(graph, result, triple, resourceUri, resourceUri, new HashSet<INode>());
                }
            }

            return result.Triples.Count == 0 ? null : SerializeNTriples(result);
        }
        finally
        {
            _lock.Release();
        }
    }

    private static IGraph ParseIncomingGraph(Uri resourceUri, string? contentType, string rdfBody)
    {
        var graph = new Graph { BaseUri = resourceUri };
        var parser = CreateParser(contentType);

        using var reader = new StringReader(rdfBody);
        parser.Load(graph, reader);
        return graph;
    }

    private static IRdfReader CreateParser(string? contentType)
    {
        var mediaType = contentType?.Split(';', 2)[0].Trim().ToLowerInvariant();

        return mediaType switch
        {
            null or "" or "application/rdf+xml" or "application/xml" or "text/xml" => new RdfXmlParser(),
            "text/turtle" or "application/x-turtle" => new TurtleParser(TurtleSyntax.Rdf11Star, false),
            "application/n-triples" or "application/n-triples+text" => new NTriplesParser(),
            _ => throw new NotSupportedException($"Unsupported RDF content type: {contentType}")
        };
    }

    private static IGraph ExtractWritableSidecarGraph(IGraph incoming, Uri resourceUri, Uri publicBaseUri)
    {
        var target = new Graph { BaseUri = resourceUri };
        var copiedBlankNodes = new HashSet<INode>();

        foreach (var triple in incoming.Triples)
        {
            if (!IsUri(triple.Subject, resourceUri) ||
                triple.Predicate is not IUriNode predicate ||
                !WritablePredicateUris.Contains(predicate.Uri.AbsoluteUri))
            {
                continue;
            }

            CopyTriple(incoming, target, triple, resourceUri, publicBaseUri, copiedBlankNodes);
            CopyBlankNodeClosure(incoming, target, triple.Object, resourceUri, publicBaseUri, copiedBlankNodes);
            CopyUriNodeClosureForReifiedLinkObject(incoming, target, triple.Object, resourceUri, publicBaseUri, copiedBlankNodes);
            CopyReifiedStatementClosures(incoming, target, triple, resourceUri, publicBaseUri, copiedBlankNodes);
        }

        return target;
    }

    private static void CopyBlankNodeClosure(
        IGraph source,
        IGraph target,
        INode node,
        Uri resourceUri,
        Uri publicBaseUri,
        ISet<INode> copiedBlankNodes)
    {
        if (node.NodeType != NodeType.Blank || !copiedBlankNodes.Add(node))
        {
            return;
        }

        foreach (var triple in source.GetTriplesWithSubject(node))
        {
            CopyTriple(source, target, triple, resourceUri, publicBaseUri, copiedBlankNodes);
            CopyBlankNodeClosure(source, target, triple.Object, resourceUri, publicBaseUri, copiedBlankNodes);
        }
    }

    private static void CopyReifiedStatementClosures(
        IGraph source,
        IGraph target,
        Triple assertedLink,
        Uri resourceUri,
        Uri publicBaseUri,
        ISet<INode> copiedBlankNodes)
    {
        var rdfSubject = source.CreateUriNode(new Uri(RdfSubject));
        var rdfPredicate = source.CreateUriNode(new Uri(RdfPredicate));
        var rdfObject = source.CreateUriNode(new Uri(RdfObject));

        var candidates = source.GetTriplesWithPredicateObject(rdfSubject, assertedLink.Subject)
            .Select(triple => triple.Subject)
            .Distinct();

        foreach (var candidate in candidates)
        {
            var hasPredicate = source.GetTriplesWithSubjectPredicate(candidate, rdfPredicate)
                .Any(triple => triple.Object.Equals(assertedLink.Predicate));
            var hasObject = source.GetTriplesWithSubjectPredicate(candidate, rdfObject)
                .Any(triple => triple.Object.Equals(assertedLink.Object));

            if (!hasPredicate || !hasObject)
            {
                continue;
            }

            foreach (var triple in source.GetTriplesWithSubject(candidate))
            {
                CopyTriple(source, target, triple, resourceUri, publicBaseUri, copiedBlankNodes);
                CopyBlankNodeClosure(source, target, triple.Object, resourceUri, publicBaseUri, copiedBlankNodes);
            }
        }
    }

    private static void CopyUriNodeClosureForReifiedLinkObject(
        IGraph source,
        IGraph target,
        INode node,
        Uri resourceUri,
        Uri publicBaseUri,
        ISet<INode> copiedBlankNodes)
    {
        if (node is not IUriNode uriNode)
        {
            return;
        }

        var triples = source.GetTriplesWithSubject(uriNode).ToArray();
        var looksLikeReifiedLink =
            triples.Any(triple => triple.Predicate is IUriNode predicate && predicate.Uri.AbsoluteUri == RdfValue) ||
            triples.Any(triple => triple.Predicate is IUriNode predicate && predicate.Uri.AbsoluteUri == RdfSubject);

        if (!looksLikeReifiedLink)
        {
            return;
        }

        foreach (var triple in triples)
        {
            CopyTriple(source, target, triple, resourceUri, publicBaseUri, copiedBlankNodes);
            CopyBlankNodeClosure(source, target, triple.Object, resourceUri, publicBaseUri, copiedBlankNodes);
        }
    }

    private static void CopyTriple(
        IGraph source,
        IGraph target,
        Triple triple,
        Uri resourceUri,
        Uri publicBaseUri,
        ISet<INode> copiedBlankNodes)
    {
        _ = source;
        _ = copiedBlankNodes;

        target.Assert(new Triple(
            CopyNode(target, triple.Subject, resourceUri, publicBaseUri),
            CopyNode(target, triple.Predicate, resourceUri, publicBaseUri),
            CopyNode(target, triple.Object, resourceUri, publicBaseUri)));
    }

    private static INode CopyNode(IGraph target, INode node, Uri resourceUri, Uri publicBaseUri)
    {
        return node switch
        {
            IUriNode uriNode => target.CreateUriNode(uriNode.Uri),
            ILiteralNode literalNode when literalNode.DataType != null =>
                target.CreateLiteralNode(literalNode.Value, literalNode.DataType),
            ILiteralNode literalNode when !string.IsNullOrEmpty(literalNode.Language) =>
                target.CreateLiteralNode(literalNode.Value, literalNode.Language),
            ILiteralNode literalNode => target.CreateLiteralNode(literalNode.Value),
            IBlankNode blankNode => target.CreateUriNode(CreateSkolemUri(resourceUri, publicBaseUri, blankNode.InternalID)),
            _ => throw new NotSupportedException($"Unsupported RDF node type: {node.NodeType}")
        };
    }

    private static Uri CreateSkolemUri(Uri resourceUri, Uri publicBaseUri, string blankNodeId)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{resourceUri.AbsoluteUri}|{blankNodeId}"))).ToLowerInvariant();

        return new Uri(publicBaseUri, $"/.well-known/genid/oslc_{hash}");
    }

    private static string SerializeNTriples(IGraph graph)
    {
        using var writer = new System.IO.StringWriter();
        new NTriplesWriter(NTriplesSyntax.Rdf11)
        {
            SortTriples = true
        }.Save(graph, writer);
        return writer.ToString();
    }

    private static Uri? ExtractLinkedResource(INode node, IReadOnlyDictionary<string, Uri> rdfValuesBySubject)
    {
        if (node is IUriNode uriNode &&
            rdfValuesBySubject.TryGetValue(uriNode.Uri.AbsoluteUri, out var reifiedValue))
        {
            return reifiedValue;
        }

        if (node is IUriNode uriNodeDirect && !uriNodeDirect.Uri.AbsolutePath.Contains("/.well-known/genid/", StringComparison.Ordinal))
        {
            return uriNodeDirect.Uri;
        }

        if (node is not IUriNode skolemNode)
        {
            return null;
        }

        if (rdfValuesBySubject.TryGetValue(skolemNode.Uri.AbsoluteUri, out var value))
        {
            return value;
        }

        return null;
    }

    private static void AddRequirementLink(Requirement requirement, Uri predicate, Uri target)
    {
        var predicateUri = predicate.AbsoluteUri;

        if (predicateUri == AffectedBy.AbsoluteUri)
        {
            requirement.AffectedBy ??= [];
            requirement.AffectedBy.Add(target);
        }
        else if (predicateUri == ImplementedBy.AbsoluteUri)
        {
            requirement.ImplementedBy ??= [];
            requirement.ImplementedBy.Add(target);
        }
        else if (predicateUri == TrackedBy.AbsoluteUri)
        {
            requirement.TrackedBy ??= [];
            requirement.TrackedBy.Add(target);
        }
        else if (predicateUri == ValidatedBy.AbsoluteUri)
        {
            requirement.ValidatedBy ??= [];
            requirement.ValidatedBy.Add(target);
        }
        else if (predicateUri == SatisfiedBy.AbsoluteUri)
        {
            requirement.SatisfiedBy ??= [];
            requirement.SatisfiedBy.Add(target);
        }
    }

    private static bool IsUri(INode node, Uri uri)
    {
        return node is IUriNode uriNode &&
               string.Equals(uriNode.Uri.AbsoluteUri, uri.AbsoluteUri, StringComparison.Ordinal);
    }

    private async Task<Dictionary<string, LinkSidecarEntry>> LoadStoreAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_storePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(_storePath);
        return await JsonSerializer
            .DeserializeAsync<Dictionary<string, LinkSidecarEntry>>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false) ?? [];
    }

    private async Task SaveStoreAsync(
        Dictionary<string, LinkSidecarEntry> store,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_storePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{_storePath}.tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, store, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, _storePath, overwrite: true);
    }

    private sealed class LinkSidecarEntry
    {
        public required string NTriples { get; init; }
        public required DateTimeOffset UpdatedAt { get; init; }
    }
}
