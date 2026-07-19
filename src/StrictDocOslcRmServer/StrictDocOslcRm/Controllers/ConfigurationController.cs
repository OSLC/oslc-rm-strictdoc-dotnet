using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using OSLC4Net.Core;
using OSLC4Net.Core.Model;
using OSLC4Net.Core.Query;
using StrictDocOslcRm.Models;
using StrictDocOslcRm.Services;
using VDS.RDF;
using VDS.RDF.Parsing;

namespace StrictDocOslcRm.Controllers;

[ApiController]
[Route("/oslc_config")]
[Produces("application/rdf+xml", "text/turtle", "application/ld+json", "text/html")]
public sealed class ConfigurationController(
    IBaseUrlService baseUrlService,
    IConfigurationContextService configurationContextService,
    IConfigurationBaselineService configurationBaselineService) : Controller
{
    [HttpGet("components/{componentId}")]
    public IActionResult GetComponent(string componentId)
    {
        if (!string.Equals(componentId, ConfigurationResourceFactory.ComponentId, StringComparison.Ordinal))
        {
            return NotFound();
        }

        return Ok(ConfigurationResourceFactory.CreateComponent(baseUrlService.GetBaseUrl()));
    }

    [HttpGet("configurations")]
    public async Task<GenericConfigurationContainer> GetConfigurationContainer()
    {
        var baseUrl = baseUrlService.GetBaseUrl();
        var contexts = await configurationContextService.GetAllAsync(HttpContext.RequestAborted).ConfigureAwait(false);
        return new GenericConfigurationContainer(new Uri($"{baseUrl}/oslc_config/configurations"))
        {
            Members = contexts
                .Select(context => new Uri(ConfigurationResourceFactory.ConfigurationUri(baseUrl, context)))
                .ToList()
        };
    }

    [HttpOptions("components/{componentId}")]
    public IActionResult OptionsComponent(string componentId)
    {
        Response.Headers.Allow = "GET, HEAD, OPTIONS";
        return Ok();
    }

    [HttpGet("configurations/{branch}/{tag}")]
    public async Task<IActionResult> GetConfiguration(string branch, string tag)
    {
        try
        {
            var context = await configurationContextService.GetAsync(branch, tag, HttpContext.RequestAborted)
                .ConfigureAwait(false);
            var baseUrl = baseUrlService.GetBaseUrl();
            var contexts = await configurationContextService.GetAllAsync(HttpContext.RequestAborted)
                .ConfigureAwait(false);
            return Ok(ConfigurationResourceFactory.CreateTypedConfiguration(
                context,
                baseUrl,
                FindPreviousBaseline(context, contexts, baseUrl)));
        }
        catch (ConfigurationContextNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpOptions("configurations/{branch}/{tag}")]
    public IActionResult OptionsConfiguration(string branch, string tag)
    {
        Response.Headers.Allow = "GET, HEAD, OPTIONS";
        return Ok();
    }

    [HttpGet("configurations/{branch}/HEAD/baselines")]
    public async Task<IActionResult> GetBaselines(string branch)
    {
        try
        {
            _ = await configurationContextService.GetAsync(branch, "HEAD", HttpContext.RequestAborted)
                .ConfigureAwait(false);
            var contexts = await configurationContextService.GetAllAsync(HttpContext.RequestAborted)
                .ConfigureAwait(false);
            var baselines = contexts.Where(context =>
                string.Equals(context.Branch, branch, StringComparison.Ordinal) && !context.IsMutable);
            return Ok(ConfigurationResourceFactory.CreateBaselinesContainer(branch, baselines, baseUrlService.GetBaseUrl()));
        }
        catch (ConfigurationContextNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpOptions("configurations/{branch}/HEAD/baselines")]
    public IActionResult OptionsBaselines(string branch)
    {
        Response.Headers.Allow = "GET, HEAD, OPTIONS, POST";
        return Ok();
    }

    /// <summary>
    /// OSLC Configuration Management baseline creation: POSTing to the baselines LDPC of a stream.
    /// </summary>
    [HttpPost("configurations/{branch}/HEAD/baselines")]
    public async Task<IActionResult> CreateBaselineFromStream(string branch)
    {
        string tag;
        try
        {
            tag = await ReadBaselineTagAsync(Request, HttpContext.RequestAborted).ConfigureAwait(false);
        }
        catch (BaselineRequestException exception)
        {
            return BadRequest(exception.Message);
        }
        catch (NotSupportedException exception)
        {
            return StatusCode(StatusCodes.Status415UnsupportedMediaType, exception.Message);
        }

        return await CreateBaselineAsync(branch, tag).ConfigureAwait(false);
    }

    /// <summary>
    /// Publishes an immutable local baseline by snapshotting the branch's current HEAD export.
    /// </summary>
    [HttpPost("configurations/{branch}/baselines/{tag}")]
    public async Task<IActionResult> CreateBaseline(string branch, string tag)
    {
        // REVISIT: Remove this administrative alias after deployed clients have moved to the
        // standards-based stream baselines LDPC endpoint above.
        return await CreateBaselineAsync(branch, tag).ConfigureAwait(false);
    }

    private async Task<IActionResult> CreateBaselineAsync(string branch, string tag)
    {
        try
        {
            var result = await configurationBaselineService.CreateAsync(branch, tag, HttpContext.RequestAborted)
                .ConfigureAwait(false);
            var resource = ConfigurationResourceFactory.CreateTypedConfiguration(result.Baseline, baseUrlService.GetBaseUrl());
            return CreatedAtAction(nameof(GetConfiguration), new { branch, tag }, resource);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
        catch (ConfigurationContextNotFoundException exception)
        {
            return NotFound(exception.Message);
        }
        catch (BaselineAlreadyExistsException exception)
        {
            return Conflict(exception.Message);
        }
    }

    [HttpGet("configurations/query")]
    public async Task<IActionResult> Query([FromQuery] string? terms = null)
    {
        var contexts = await configurationContextService.GetAllAsync(HttpContext.RequestAborted).ConfigureAwait(false);
        var searchTerms = terms ?? Request.Query["oslc.searchTerms"].ToString();
        var configurations = contexts
            .Select(context => ConfigurationResourceFactory.CreateConfiguration(context, baseUrlService.GetBaseUrl()))
            .Where(configuration => string.IsNullOrWhiteSpace(searchTerms) ||
                $"{configuration.Identifier}\n{configuration.Title}".Contains(searchTerms, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var selectedProperties = BuildSelectedProperties(Request.Query["oslc.select"]);
        var response = new ResponseInfoArray<GenericConfiguration>(
            configurations,
            selectedProperties,
            configurations.Length,
            (string)null!);
        return Ok(response);
    }

    [HttpGet("configurations/selector")]
    [Produces("text/html")]
    public async Task<IActionResult> Selector([FromQuery] string? terms = null)
    {
        var baseUrl = baseUrlService.GetBaseUrl();
        var contexts = await configurationContextService.GetAllAsync(HttpContext.RequestAborted).ConfigureAwait(false);
        var results = contexts
            .Select(context => ConfigurationResourceFactory.CreateConfiguration(context, baseUrl))
            .Where(configuration => string.IsNullOrWhiteSpace(terms) ||
                $"{configuration.Identifier}\n{configuration.Title}".Contains(terms, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return View("ConfigurationSelector", new ConfigurationSelectionViewModel
        {
            SelectorUri = $"{baseUrl}/oslc_config/configurations/selector",
            Terms = terms,
            Results = results
        });
    }

    [HttpGet("shapes/configuration")]
    public ResourceShape GetConfigurationShape() => ResourceShapeFactory.CreateResourceShape(
        baseUrlService.GetBaseUrl(),
        "oslc_config/shapes",
        "configuration",
        typeof(GenericConfiguration));

    private static IDictionary<string, object> BuildSelectedProperties(string? select)
    {
        var prefixes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["dcterms"] = "http://purl.org/dc/terms/",
            ["oslc"] = "http://open-services.net/ns/core#",
            ["oslc_config"] = ConfigurationVocabulary.Namespace,
            ["rdf"] = OslcConstants.RDF_NAMESPACE
        };
        var parsed = QueryUtils.ParseSelect(string.IsNullOrWhiteSpace(select) ? "*" : select, prefixes);
        return QueryUtils.InvertSelectedProperties(parsed);
    }

    private static Uri? FindPreviousBaseline(
        ConfigurationContext context,
        IReadOnlyList<ConfigurationContext> contexts,
        string baseUrl)
    {
        if (!context.IsMutable)
        {
            return null;
        }

        var previousBaseline = contexts
            .Where(candidate =>
                string.Equals(candidate.Branch, context.Branch, StringComparison.Ordinal) && !candidate.IsMutable)
            .OrderByDescending(candidate => System.IO.File.GetLastWriteTimeUtc(candidate.StrictDocPath))
            .FirstOrDefault();
        return previousBaseline is null
            ? null
            : new Uri(ConfigurationResourceFactory.ConfigurationUri(baseUrl, previousBaseline));
    }

    private static async Task<string> ReadBaselineTagAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var explicitTag = request.Query["tag"].ToString();
        if (!string.IsNullOrWhiteSpace(explicitTag))
        {
            return explicitTag.Trim();
        }

        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var rdfBody = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(rdfBody))
        {
            throw new BaselineRequestException(
                "A baseline POST must provide a dcterms:identifier or dcterms:title, or a tag query parameter.");
        }

        var graph = new Graph();
        var parser = CreateRdfParser(request.ContentType);
        using var rdfReader = new StringReader(rdfBody);
        parser.Load(graph, rdfReader);

        var identifier = FindLiteral(graph, "http://purl.org/dc/terms/identifier");
        if (!string.IsNullOrWhiteSpace(identifier))
        {
            return identifier;
        }

        var title = FindLiteral(graph, "http://purl.org/dc/terms/title");
        if (!string.IsNullOrWhiteSpace(title))
        {
            return TagFromTitle(title);
        }

        throw new BaselineRequestException(
            "The baseline RDF must provide dcterms:identifier or dcterms:title.");
    }

    private static IRdfReader CreateRdfParser(string? contentType)
    {
        var mediaType = contentType?.Split(';', 2)[0].Trim().ToLowerInvariant();
        return mediaType switch
        {
            null or "" or "application/rdf+xml" or "application/xml" or "text/xml" => new RdfXmlParser(),
            "text/turtle" or "application/x-turtle" => new TurtleParser(TurtleSyntax.Rdf11Star, false),
            "application/n-triples" or "application/n-triples+text" => new NTriplesParser(),
            _ => throw new NotSupportedException($"Unsupported RDF content type for baseline creation: {contentType}")
        };
    }

    private static string? FindLiteral(IGraph graph, string predicateUri) =>
        graph.Triples
            .Where(triple => triple.Predicate is IUriNode predicate && predicate.Uri.AbsoluteUri == predicateUri)
            .Select(triple => (triple.Object as ILiteralNode)?.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string TagFromTitle(string title)
    {
        var tag = Regex.Replace(title.Trim(), "[^A-Za-z0-9._-]+", "-").Trim('-', '.', '_');
        tag = tag.Length > 80 ? tag[..80].TrimEnd('-', '.', '_') : tag;
        if (string.IsNullOrWhiteSpace(tag) || !char.IsLetterOrDigit(tag[0]))
        {
            throw new BaselineRequestException(
                "The baseline title cannot be converted to a safe publication tag; provide dcterms:identifier instead.");
        }

        return tag;
    }

    private sealed class BaselineRequestException(string message) : Exception(message);
}
