using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using OSLC4Net.Domains.RequirementsManagement;
using StrictDocOslcRm.Services;

namespace StrictDocOslcRm.Controllers;

[ApiController]
[Route("/.well-known/oslc/rootservices.xml")]
public class RootServicesController(
    ILogger<RootServicesController> logger,
    IBaseUrlService baseUrlService,
    IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    [Produces("application/rdf+xml")]
    public IActionResult GetRootServices()
    {
        logger.LogDebug("Received request for OSLC Root Services document");
        var appName = "OSLC RM for StrictDoc";
        var baseUrl = baseUrlService.GetBaseUrl().TrimEnd('/');
        var escapedBaseUrl = WebUtility.HtmlEncode(baseUrl);
        var serviceTitle = WebUtility.HtmlEncode(configuration["OSLC:ServiceTitle"] ?? "OSLC Requirements Management server for StrictDoc");
        var oauthRequestConsumerKeyUrl = $"{baseUrl}/oauth/request_consumer_key";
        var oauthApprovalModuleUrl = $"{baseUrl}/oauth/approve_consumer_key";
        var oauthRequestTokenUrl = $"{baseUrl}/oauth/request_token";
        var oauthUserAuthorizationUrl = $"{baseUrl}/oauth/authorize";
        var oauthAccessTokenUrl = $"{baseUrl}/oauth/access_token";

        var rootServicesBody = $$"""
            <?xml version="1.0" encoding="UTF-8"?>
            <rdf:Description
                    xmlns:oslc_rm="http://open-services.net/xmlns/rm/1.0/"
                    xmlns:oslc="http://open-services.net/ns/core#"
                    xmlns:dc="http://purl.org/dc/terms/"
                    xmlns:jfs="http://jazz.net/xmlns/prod/jazz/jfs/1.0/"
                    xmlns:jd="http://jazz.net/xmlns/prod/jazz/discovery/1.0/"
                    xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"
                    rdf:about="{{escapedBaseUrl}}/.well-known/oslc/rootservices.xml">
                <dc:title>{{serviceTitle}}</dc:title>
                <oslc_rm:rmServiceProviders rdf:resource="{{escapedBaseUrl}}/oslc/catalog" />
                <jd:oslcCatalogs>
                    <oslc:ServiceProviderCatalog rdf:about="{{escapedBaseUrl}}/oslc/catalog">
                        <oslc:domain rdf:resource="{{RM.NS}}" />
                    </oslc:ServiceProviderCatalog>
                </jd:oslcCatalogs>
                <jd:jsaSsoEnabled>false</jd:jsaSsoEnabled>
                <jfs:oauthRealmName>{{appName}}</jfs:oauthRealmName>
                <jfs:oauthDomain>{{escapedBaseUrl}}</jfs:oauthDomain>
                <jfs:oauthRequestConsumerKeyUrl rdf:resource="{{oauthRequestConsumerKeyUrl}}"/>
                <jfs:oauthApprovalModuleUrl rdf:resource="{{oauthApprovalModuleUrl}}"/>
                <jfs:oauthRequestTokenUrl rdf:resource="{{oauthRequestTokenUrl}}"/>
                <jfs:oauthUserAuthorizationUrl rdf:resource="{{oauthUserAuthorizationUrl}}"/>
                <jfs:oauthAccessTokenUrl rdf:resource="{{oauthAccessTokenUrl}}"/>
            </rdf:Description>
            """;

        return Content(rootServicesBody, "application/rdf+xml", Encoding.UTF8);
    }
}
