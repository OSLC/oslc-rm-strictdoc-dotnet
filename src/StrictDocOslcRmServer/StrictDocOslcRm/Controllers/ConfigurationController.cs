using Microsoft.AspNetCore.Mvc;
using OSLC4Net.Core;
using OSLC4Net.Core.Model;
using OSLC4Net.Core.Query;
using StrictDocOslcRm.Models;
using StrictDocOslcRm.Services;

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
            return Ok(ConfigurationResourceFactory.CreateConfiguration(context, baseUrlService.GetBaseUrl()));
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

    /// <summary>
    /// Publishes an immutable local baseline by snapshotting the branch's current HEAD export.
    /// </summary>
    [HttpPost("configurations/{branch}/baselines/{tag}")]
    public async Task<IActionResult> CreateBaseline(string branch, string tag)
    {
        try
        {
            var result = await configurationBaselineService.CreateAsync(branch, tag, HttpContext.RequestAborted)
                .ConfigureAwait(false);
            var resource = ConfigurationResourceFactory.CreateConfiguration(result.Baseline, baseUrlService.GetBaseUrl());
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
}
