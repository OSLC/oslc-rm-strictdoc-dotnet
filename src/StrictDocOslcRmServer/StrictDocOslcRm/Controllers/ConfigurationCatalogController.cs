using Microsoft.AspNetCore.Mvc;
using OSLC4Net.Core.Model;
using StrictDocOslcRm.Models;
using StrictDocOslcRm.Services;

namespace StrictDocOslcRm.Controllers;

[ApiController]
[Route("/oslc_config/catalog")]
[Produces("application/rdf+xml", "text/turtle", "application/ld+json")]
public sealed class ConfigurationCatalogController(IBaseUrlService baseUrlService) : ControllerBase
{
    [HttpGet]
    public ServiceProviderCatalog Get()
    {
        var baseUrl = baseUrlService.GetBaseUrl();
        var providerUri = new Uri($"{baseUrl}/oslc_config/service_provider");
        var provider = new OSLC4Net.Core.Model.ServiceProvider();
        provider.SetAbout(providerUri);
        provider.SetDetails([providerUri]);
        provider.SetIdentifier("strictdoc-configuration");
        provider.SetTitle("StrictDoc Configuration Management Service Provider");
        provider.SetDescription("Local OSLC Configuration Management provider for StrictDoc snapshots.");

        var catalog = new ServiceProviderCatalog();
        catalog.SetAbout(new Uri($"{baseUrl}/oslc_config/catalog"));
        catalog.SetTitle("StrictDoc Configuration Management Service Provider Catalog");
        catalog.SetDescription("Catalog of StrictDoc local configuration services.");
        catalog.AddDomain(new Uri(ConfigurationVocabulary.Namespace));
        catalog.AddServiceProvider(provider);
        return catalog;
    }
}
