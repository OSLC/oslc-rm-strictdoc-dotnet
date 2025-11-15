using Microsoft.AspNetCore.Mvc;
using OSLC4Net.Core.Model;
using StrictDocOslcRm.Models;
using StrictDocOslcRm.Services;
using Compact = StrictDocOslcRm.Models.Compact;
using Preview = StrictDocOslcRm.Models.Preview;

namespace StrictDocOslcRm.Controllers;

/// <summary>
/// Controller for individual requirement access using the new URI format
/// </summary>
[ApiController]
public class RequirementController(
    ILogger<RequirementController> logger,
    IBaseUrlService baseUrlService,
    IStrictDocService strictDocService) : Controller
{
    /// <summary>
    /// Unified endpoint for requirement resources, compact resources, and HTML previews.
    /// Handles: /?a={uid}, /?a={uid}&compact, /?a={uid}&preview=small, /?a={uid}&preview=large
    /// </summary>
    [HttpGet]
    [Route("/")]
    public async Task<IActionResult> GetRequirementResource(
        [FromQuery] string a,
        [FromQuery] string? compact,
        [FromQuery] string? preview)
    {
        if (string.IsNullOrEmpty(a))
        {
            return BadRequest("Parameter 'a' (requirement UID) is required.");
        }

        var baseUrl = baseUrlService.GetBaseUrl();
        var allRequirements = await strictDocService.GetAllRequirementsAsync(baseUrl);
        var requirement = allRequirements.FirstOrDefault(r => string.Equals(r.Identifier, a, StringComparison.Ordinal));

        if (requirement == null)
        {
            return NotFound($"No requirement found with UID '{a}'.");
        }

        var requirementUri = $"{baseUrl}/?a={a}";

        // Handle HTML preview requests - require text/html Accept header
        if (!string.IsNullOrEmpty(preview))
        {
            // Check if client accepts HTML
            var acceptHeader = Request.Headers.Accept.ToString();
            if (!acceptHeader.Contains("text/html") && !acceptHeader.Contains("*/*"))
            {
                return StatusCode(406, "Not Acceptable: text/html is required for preview requests");
            }

            var model = new RequirementPreviewViewModel
            {
                Requirement = requirement,
                RequirementUri = requirementUri
            };

            return preview.ToLower() switch
            {
                "small" => View("SmallPreview", model),
                "large" => View("LargePreview", model),
                _ => BadRequest($"Invalid preview type: {preview}. Use 'small' or 'large'.")
            };
        }

        // Handle Compact resource request
        if (compact != null)
        {
            var compactResource = new Compact();
            compactResource.SetAbout(new Uri($"{requirementUri}&compact"));
            compactResource.Title = requirement.Title ?? requirement.Identifier;
            compactResource.ShortTitle = requirement.Identifier;
            compactResource.Icon = new Uri($"{baseUrl}/icons/requirement.svg");
            compactResource.IconTitle = "Requirement";
            compactResource.IconAltLabel = "Requirement";

            compactResource.SmallPreview = new Preview
            {
                Document = new Uri($"{requirementUri}&preview=small"),
                HintWidth = "320px",
                HintHeight = "200px"
            };

            compactResource.LargePreview = new Preview
            {
                Document = new Uri($"{requirementUri}&preview=large"),
                HintWidth = "600px",
                HintHeight = "400px"
            };

            return Ok(compactResource);
        }

        // Handle regular Requirement resource request
        requirement.SetAbout(new Uri(requirementUri));

        // Add Link header for Compact resource (OSLC Resource Preview spec)
        Response.Headers.Append("Link",
            $"<{requirementUri}&compact>; rel=\"{OslcConstants.OSLC_CORE_NAMESPACE}Compact\"");

        return Ok(requirement);
    }
}
