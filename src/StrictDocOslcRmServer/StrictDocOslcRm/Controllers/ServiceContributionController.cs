using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using StrictDocOslcRm.Services;

namespace StrictDocOslcRm.Controllers;

/// <summary>
/// Jazz Service Contribution Resource used for application and catalog discovery.
/// </summary>
[ApiController]
[Route("/.well-known/oslc/scr")]
public class ServiceContributionController(
    ILogger<ServiceContributionController> logger,
    IBaseUrlService baseUrlService) : ControllerBase
{
    [HttpGet]
    [Produces("application/rdf+xml")]
    public IActionResult GetServiceContributionResource()
    {
        logger.LogDebug("Received request for the Jazz Service Contribution Resource");
        var baseUrl = WebUtility.HtmlEncode(baseUrlService.GetBaseUrl());

        var body = $$"""
            <?xml version="1.0" encoding="UTF-8"?>
            <rdf:RDF
                    xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"
                    xmlns:dcterms="http://purl.org/dc/terms/"
                    xmlns:oslc_rm="http://open-services.net/xmlns/rm/1.0/"
                    xmlns:jd="http://jazz.net/xmlns/prod/jazz/discovery/1.0/">
                <jd:Application>
                    <jd:contextRoot>{{baseUrl}}</jd:contextRoot>
                    <jd:rootServices rdf:resource="{{baseUrl}}/.well-known/oslc/rootservices.xml" />
                    <jd:domain rdf:parseType="Resource">
                        <dcterms:identifier>http://open-services.net/ns/rm#</dcterms:identifier>
                    </jd:domain>
                    <jd:jsaSsoEnabled>false</jd:jsaSsoEnabled>
                </jd:Application>
                <oslc_rm:RmServiceProviders>
                    <jd:service rdf:resource="{{baseUrl}}/oslc/catalog" />
                </oslc_rm:RmServiceProviders>
            </rdf:RDF>
            """;

        return Content(body, "application/rdf+xml", Encoding.UTF8);
    }
}
