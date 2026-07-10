using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using StrictDocOslcRm.Services;

namespace StrictDocOslcRm.Controllers;

[ApiController]
[Route("/application-about")]
public sealed class PublisherController(IBaseUrlService baseUrlService) : ControllerBase
{
    [HttpGet]
    [Produces("application/rdf+xml")]
    public IActionResult Get()
    {
        var baseUrl = WebUtility.HtmlEncode(baseUrlService.GetBaseUrl());
        var body = $$"""
            <?xml version="1.0" encoding="UTF-8"?>
            <rdf:RDF
                xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"
                xmlns:oslc="http://open-services.net/ns/core#"
                xmlns:dcterms="http://purl.org/dc/terms/">
              <oslc:Publisher rdf:about="{{baseUrl}}/application-about">
                <dcterms:title>OSLC Requirements Management server for StrictDoc</dcterms:title>
                <dcterms:identifier>strictdoc-oslc-rm</dcterms:identifier>
              </oslc:Publisher>
            </rdf:RDF>
            """;
        return Content(body, "application/rdf+xml", Encoding.UTF8);
    }
}
