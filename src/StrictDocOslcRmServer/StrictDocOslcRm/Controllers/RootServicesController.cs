using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text;

namespace StrictDocOslcRm.Controllers;

[ApiController]
[Route("/.well-known/oslc/rootservices.xml")]
public class RootServicesController(ILogger<RootServicesController> logger, IHttpContextAccessor httpContextAccessor, IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    [Produces("application/rdf+xml")]
    public IActionResult GetRootServices()
    {
        var request = httpContextAccessor.HttpContext?.Request;
        var baseUrl = $"{request?.Scheme}://{request?.Host}{request?.PathBase}";
        var serviceTitle = WebUtility.HtmlEncode(configuration["OSLC:ServiceTitle"] ?? "OSLC Requirements Management server for StrictDoc");

        var rootServicesBody = $$"""
            <?xml version="1.0" encoding="UTF-8"?>
            <rdf:Description
                    xmlns:oslc_rm="http://open-services.net/xmlns/rm/1.0/"
                    xmlns:dc="http://purl.org/dc/terms/"
                    xmlns:jfs="http://jazz.net/xmlns/prod/jazz/jfs/1.0/"
                    xmlns:jd="http://jazz.net/xmlns/prod/jazz/discovery/1.0/"
                    xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"
                    rdf:about="{{baseUrl}}/.well-known/oslc/rootservices.xml">
                <dc:title>{{serviceTitle}}</dc:title>
                <oslc_rm:rmServiceProviders rdf:resource="{{baseUrl}}/oslc/catalog" />
                <jd:jsaSsoEnabled>false</jd:jsaSsoEnabled>
            </rdf:Description>
            """;

        return Content(rootServicesBody, "application/rdf+xml", Encoding.UTF8);
    }
}