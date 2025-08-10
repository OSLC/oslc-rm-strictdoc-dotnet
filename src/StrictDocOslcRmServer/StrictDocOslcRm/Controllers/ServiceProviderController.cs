using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using OSLC4Net.Core.Model;
using OSLC4Net.Domains.RequirementsManagement;
using StrictDocOslcRm.Services;

namespace StrictDocOslcRm.Controllers;
/// <summary>
/// OSLC Service Provider for StrictDoc documents providing Requirements Management capabilities.
/// </summary>
[ApiController]
[Route("/oslc/service_provider")]
[Produces("application/rdf+xml", "text/turtle", "application/ld+json")]
public class ServiceProviderController(ILogger<ServiceProviderController> logger,
    IHttpContextAccessor httpContextAccessor,
    IStrictDocService strictDocService) : ControllerBase
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
        serviceProvider.SetDescription($"OSLC Requirements Management service for StrictDoc document: {document.Title}");

        var svc = new OSLC4Net.Core.Model.Service();
        svc.SetDomain(new Uri("http://open-services.net/ns/rm#"));

        var queryCap = new QueryCapability();
        queryCap.SetTitle("StrictDoc Requirements Query Capability");
        queryCap.SetLabel("StrictDoc Requirements Query Capability");
        queryCap.SetResourceTypes([new Uri("http://open-services.net/ns/rm#Requirement")]);
        queryCap.SetResourceShape(new Uri("http://open-services.net/ns/rm/shapes/3.0#RequirementShape"));

        var request = httpContextAccessor.HttpContext?.Request;
        var baseUrl = $"{request?.Scheme}://{request?.Host}{request?.PathBase}";
        queryCap.SetQueryBase(new Uri($"{baseUrl}/oslc/service_provider/{documentMid}/requirements"));

        svc.AddQueryCapability(queryCap);
        serviceProvider.SetServices([svc]);

        return serviceProvider;
    }

    [HttpGet]
    [Route("{documentMid}/requirements")]
    public async Task<ActionResult<IEnumerable<Requirement>>> GetRequirements(string documentMid)
    {
        var request = httpContextAccessor.HttpContext?.Request;
        var baseUrl = $"{request?.Scheme}://{request?.Host}{request?.PathBase}";

        var requirements = await strictDocService.GetRequirementsForDocumentAsync(documentMid, baseUrl);

        if (!requirements.Any())
        {
            return NotFound($"No requirements found for document '{documentMid}'.");
        }

        // Set the About URI for each requirement using new format
        foreach (var requirement in requirements)
        {
            if (!string.IsNullOrEmpty(requirement.Identifier))
            {
                requirement.SetAbout(new Uri($"{baseUrl}/?a={requirement.Identifier}"));
            }
        }

        return Ok(requirements);
    }

    [HttpGet]
    [Route("{documentMid}/requirements/{requirementUid}")]
    public async Task<ActionResult<Requirement>> GetRequirement(string documentMid, string requirementUid)
    {
        var requirement = await strictDocService.GetRequirementByUidAsync(requirementUid);

        if (requirement == null)
        {
            return NotFound($"No requirement found with UID '{requirementUid}'.");
        }

        // Set the About URI using new format
        var request = httpContextAccessor.HttpContext?.Request;
        var baseUrl = $"{request?.Scheme}://{request?.Host}{request?.PathBase}";
        requirement.SetAbout(new Uri($"{baseUrl}/?a={requirementUid}"));

        return Ok(requirement);
    }
}
