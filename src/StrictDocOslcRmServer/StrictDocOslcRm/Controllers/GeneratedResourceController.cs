using Microsoft.AspNetCore.Mvc;
using StrictDocOslcRm.Services;

namespace StrictDocOslcRm.Controllers;

[ApiController]
[Route("/.well-known/genid/{id}")]
[Produces("application/n-triples")]
public class GeneratedResourceController(
    IBaseUrlService baseUrlService,
    ILinkSidecarService linkSidecarService,
    IConfigurationContextService configurationContextService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest("Generated resource id is required.");
        }

        var baseUrl = baseUrlService.GetBaseUrl();
        ConfigurationContext context;
        try
        {
            context = await configurationContextService
                .ResolveAsync(Request, baseUrl, HttpContext.RequestAborted)
                .ConfigureAwait(false);
        }
        catch (ConfigurationContextNotFoundException exception)
        {
            return BadRequest(exception.Message);
        }

        var resourceUri = new Uri($"{baseUrl}/.well-known/genid/{Uri.EscapeDataString(id)}");
        var ntriples = await linkSidecarService
            .GetResourceNTriplesAsync(context, resourceUri, HttpContext.RequestAborted)
            .ConfigureAwait(false);

        return ntriples == null
            ? NotFound()
            : Content(ntriples, "application/n-triples; charset=utf-8");
    }
}
