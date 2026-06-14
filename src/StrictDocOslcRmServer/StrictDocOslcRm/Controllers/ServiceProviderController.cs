using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using OSLC4Net.Core.Model;
using OSLC4Net.Domains.RequirementsManagement;
using StrictDocOslcRm.Models;
using StrictDocOslcRm.Services;

namespace StrictDocOslcRm.Controllers;

/// <summary>
/// OSLC Service Provider for StrictDoc documents providing Requirements Management capabilities.
/// </summary>
[ApiController]
[Route("/oslc/service_provider")]
[Produces("application/rdf+xml", "text/turtle", "application/ld+json")]
public class ServiceProviderController(
    ILogger<ServiceProviderController> logger,
    IBaseUrlService baseUrlService,
    IStrictDocService strictDocService,
    IOslcQueryService oslcQueryService) : Controller
{
    [HttpGet]
    [Route("{documentMid}")]
    public async Task<ActionResult<OSLC4Net.Core.Model.ServiceProvider>> Get(string documentMid)
    {
        var documents = await strictDocService.GetDocumentsAsync();
        var document = documents.FirstOrDefault(d => string.Equals(d.Mid, documentMid, StringComparison.Ordinal));

        if (document == null)
        {
            return NotFound($"Document with MID '{documentMid}' not found.");
        }

        var serviceProvider = new OSLC4Net.Core.Model.ServiceProvider();
        serviceProvider.SetAbout(new Uri(Request.GetEncodedUrl()));
        serviceProvider.SetIdentifier(document.Mid);
        serviceProvider.SetTitle(document.Title);
        serviceProvider.SetDescription(
            $"OSLC Requirements Management service for StrictDoc document: {document.Title}");

        var svc = new OSLC4Net.Core.Model.Service();
        svc.SetDomain(new Uri("http://open-services.net/ns/rm#"));

        var queryCap = new QueryCapability();
        queryCap.SetTitle("StrictDoc Requirements Query Capability");
        queryCap.SetLabel("StrictDoc Requirements Query Capability");
        queryCap.SetResourceTypes([new Uri("http://open-services.net/ns/rm#Requirement")]);
        queryCap.SetResourceShape(
            new Uri("http://open-services.net/ns/rm/shapes/3.0#RequirementShape"));

        var baseUrl = baseUrlService.GetBaseUrl();
        queryCap.SetQueryBase(
            new Uri($"{baseUrl}/oslc/service_provider/{documentMid}/requirements"));

        svc.AddQueryCapability(queryCap);

        // Selection dialog (delegated UI)
        var selectionDialog = new Dialog();
        selectionDialog.SetTitle("Requirement Selection Dialog");
        selectionDialog.SetLabel("Select Requirement");
        selectionDialog.SetDialog(
            new Uri($"{baseUrl}/oslc/service_provider/{documentMid}/requirements/selector"));
        selectionDialog.SetHintWidth("500px");
        selectionDialog.SetHintHeight("500px");
        selectionDialog.SetResourceTypes([new Uri("http://open-services.net/ns/rm#Requirement")]);
        svc.SetSelectionDialogs([selectionDialog]);

        serviceProvider.SetServices([svc]);

        return serviceProvider;
    }

    /// <summary>
    /// OSLC Query capability for a document's requirements.
    /// Supports oslc.prefix, oslc.where, oslc.select, oslc.orderBy, oslc.searchTerms and
    /// oslc.pageSize, returning an oslc:ResponseInfo container with oslc:totalCount and
    /// rdfs:member links.
    /// </summary>
    [HttpGet]
    [Route("{documentMid}/requirements")]
    public async Task<IActionResult> GetRequirements(string documentMid)
    {
        var baseUrl = baseUrlService.GetBaseUrl();

        var documents = await strictDocService.GetDocumentsAsync();
        if (documents.All(d => !string.Equals(d.Mid, documentMid, StringComparison.Ordinal)))
        {
            return NotFound($"Document with MID '{documentMid}' not found.");
        }

        var requirements = await strictDocService.GetRequirementsForDocumentAsync(documentMid, baseUrl);

        // Set the About URI for each requirement using new format
        foreach (var requirement in requirements)
        {
            if (!string.IsNullOrEmpty(requirement.Identifier))
            {
                requirement.SetAbout(new Uri($"{baseUrl}/?a={requirement.Identifier}"));
            }
        }

        var queryBase = $"{baseUrl}/oslc/service_provider/{documentMid}/requirements";
        var pageSize = ParseIntParameter(Request.Query["oslc.pageSize"]);
        var page = ParseIntParameter(Request.Query["page"]) ?? 1;

        OslcQueryOutcome outcome;
        try
        {
            outcome = oslcQueryService.Apply(
                requirements,
                Request.Query["oslc.prefix"],
                Request.Query["oslc.where"],
                Request.Query["oslc.select"],
                Request.Query["oslc.orderBy"],
                Request.Query["oslc.searchTerms"],
                pageSize,
                page,
                nextPage => BuildPageUri(queryBase, Request.Query, nextPage));
        }
        catch (OslcQueryBadRequestException exception)
        {
            logger.LogInformation("Rejected OSLC query for {DocumentMid}: {Message}", documentMid, exception.Message);
            return BadRequest(exception.Message);
        }
        catch (OslcQueryNotImplementedException exception)
        {
            logger.LogInformation("Unsupported OSLC query for {DocumentMid}: {Message}", documentMid, exception.Message);
            return StatusCode(501, exception.Message);
        }

        var responseInfoAbout = queryBase +
            (Request.QueryString.HasValue ? Request.QueryString.Value : string.Empty);

        return new OslcQueryContainerResult(
            queryBase,
            responseInfoAbout,
            outcome.NextPage,
            outcome.TotalCount,
            outcome.Members,
            outcome.SelectedProperties);
    }

    private static int? ParseIntParameter(string? value) =>
        int.TryParse(value, out var parsed) && parsed > 0 ? parsed : null;

    // Repeat the current query parameters on the next page so the filter, projection and ordering
    // are preserved, overriding only the page marker.
    private static string BuildPageUri(string queryBase, IQueryCollection query, int page)
    {
        var pairs = query
            .Where(pair => !string.Equals(pair.Key, "page", StringComparison.OrdinalIgnoreCase))
            .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value.ToString())}")
            .Append($"page={page}");
        return $"{queryBase}?{string.Join('&', pairs)}";
    }

    [HttpGet]
    [Route("{documentMid}/requirements/{requirementUid}")]
    public async Task<ActionResult<Requirement>> GetRequirement(string documentMid,
        string requirementUid)
    {
        var requirement = await strictDocService.GetRequirementByUidAsync(requirementUid);

        if (requirement == null)
        {
            return NotFound($"No requirement found with UID '{requirementUid}'.");
        }

        // Set the About URI using new format
        var baseUrl = baseUrlService.GetBaseUrl();
        requirement.SetAbout(new Uri($"{baseUrl}/?a={requirementUid}"));

        return Ok(requirement);
    }

    /// <summary>
    /// OSLC Delegated Selection Dialog for Requirements.
    /// Returns HTML (full page or HTMX fragment) or JSON (oslc:results) depending on headers.
    /// </summary>
    [HttpGet]
    [Route("{documentMid}/requirements/selector")]
    [Produces("text/html", "application/json")]
    public async Task<IActionResult> RequirementSelector(string documentMid,
        [FromQuery] string? terms = null)
    {
        var baseUrl = baseUrlService.GetBaseUrl();
        var selectorUri = $"{baseUrl}/oslc/service_provider/{documentMid}/requirements/selector";

        // Load all requirements (reuse same sourcing logic as GetRequirements)
        var requirements =
            await strictDocService.GetRequirementsForDocumentAsync(documentMid, baseUrl);
        foreach (var r in requirements)
        {
            if (!string.IsNullOrEmpty(r.Identifier))
            {
                r.SetAbout(new Uri($"{baseUrl}/?a={r.Identifier}"));
            }
        }

        IEnumerable<Requirement> filtered = requirements;
        if (!string.IsNullOrWhiteSpace(terms))
        {
            var termLower = terms.Trim().ToLowerInvariant();
            filtered = requirements.Where(r =>
                (r.Title ?? string.Empty).ToLowerInvariant().Contains(termLower) ||
                (r.Identifier ?? string.Empty).ToLowerInvariant().Contains(termLower));
        }

        var isHtmx = Request.Headers.ContainsKey("HX-Request") || Request.Headers.ContainsKey("hx-request");

        var model = new RequirementSelectionViewModel
        {
            SelectorUri = selectorUri,
            Terms = terms,
            Results = filtered
        };

        if (isHtmx)
        {
            return PartialView("_RequirementSelectorResults", model);
        }

        return View("RequirementSelector", model);
    }
}
