using Microsoft.AspNetCore.Mvc;
using OSLC4Net.Core.Model;
using OSLC4Net.Domains.RequirementsManagement;
using StrictDocOslcRm.Services;

namespace StrictDocOslcRm.Controllers;

/// <summary>
/// Provider-owned OSLC ResourceShape for StrictDoc requirements.
/// </summary>
[ApiController]
[Route("/oslc/shapes/requirement")]
[Produces("application/rdf+xml", "text/turtle", "application/ld+json", "application/json")]
public class RequirementShapeController(IBaseUrlService baseUrlService) : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var shape = ResourceShapeFactory.CreateResourceShape(
            baseUrlService.GetBaseUrl().TrimEnd('/'),
            "oslc/shapes",
            "requirement",
            typeof(Requirement));

        return Ok(shape);
    }
}
