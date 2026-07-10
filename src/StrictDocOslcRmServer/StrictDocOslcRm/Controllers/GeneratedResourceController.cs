using Microsoft.AspNetCore.Mvc;
using StrictDocOslcRm.Services;

namespace StrictDocOslcRm.Controllers;

[ApiController]
[Route("/.well-known/genid/{id}")]
[Produces("application/n-triples")]
public class GeneratedResourceController(
    IBaseUrlService baseUrlService,
    ILinkSidecarService linkSidecarService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest("Generated resource id is required.");
        }

        var resourceUri = new Uri($"{baseUrlService.GetBaseUrl()}/.well-known/genid/{Uri.EscapeDataString(id)}");
        var ntriples = await linkSidecarService
            .GetResourceNTriplesAsync(resourceUri, HttpContext.RequestAborted)
            .ConfigureAwait(false);

        return ntriples == null
            ? NotFound()
            : Content(ntriples, "application/n-triples; charset=utf-8");
    }
}
