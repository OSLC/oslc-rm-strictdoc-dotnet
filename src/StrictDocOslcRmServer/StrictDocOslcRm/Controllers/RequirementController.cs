using Microsoft.AspNetCore.Mvc;
using OSLC4Net.Domains.RequirementsManagement;
using StrictDocOslcRm.Services;

namespace StrictDocOslcRm.Controllers;

/// <summary>
/// Controller for individual requirement access using the new URI format
/// </summary>
[ApiController]
[Produces("application/rdf+xml", "text/turtle", "application/ld+json")]
public class RequirementController(
    ILogger<RequirementController> logger,
    IBaseUrlService baseUrlService,
    IStrictDocService strictDocService) : ControllerBase
{
    [HttpGet]
    [Route("/")]
    public async Task<ActionResult<Requirement>> GetRequirement([FromQuery] string a)
    {
        if (string.IsNullOrEmpty(a))
        {
            return BadRequest("Parameter 'a' (requirement UID) is required.");
        }

        var baseUrl = baseUrlService.GetBaseUrl();

        // Get all requirements with correct baseUrl to ensure decomposes URIs are correct
        var allRequirements = await strictDocService.GetAllRequirementsAsync(baseUrl);
        var requirement = allRequirements.FirstOrDefault(r => string.Equals(r.Identifier, a, StringComparison.Ordinal));

        if (requirement == null)
        {
            return NotFound($"No requirement found with UID '{a}'.");
        }

        // Set the About URI using the new format
        requirement.SetAbout(new Uri($"{baseUrl}/?a={a}"));

        return Ok(requirement);
    }
}
