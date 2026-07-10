using Microsoft.AspNetCore.Mvc;
using OSLC4Net.Core.Model;
using StrictDocOslcRm.Models;
using StrictDocOslcRm.Services;

namespace StrictDocOslcRm.Controllers;

[ApiController]
[Route("/oslc_config/service_provider")]
[Produces("application/rdf+xml", "text/turtle", "application/ld+json")]
public sealed class ConfigurationServiceProviderController(IBaseUrlService baseUrlService) : ControllerBase
{
    private const string JazzProcessNamespace = "http://jazz.net/xmlns/prod/jazz/process/1.0/";

    [HttpGet]
    public ActionResult<OSLC4Net.Core.Model.ServiceProvider> Get()
    {
        var baseUrl = baseUrlService.GetBaseUrl();
        var providerUri = new Uri($"{baseUrl}/oslc_config/service_provider");
        var provider = new OSLC4Net.Core.Model.ServiceProvider();
        provider.SetAbout(providerUri);
        provider.SetDetails([providerUri]);
        provider.SetIdentifier("strictdoc-configuration");
        provider.SetTitle("StrictDoc Configuration Management");
        provider.SetDescription("Generic local configurations backed by StrictDoc branch/tag snapshots.");
        provider.SetExtendedProperties(new Dictionary<QName, object>
        {
            [new QName(JazzProcessNamespace, "globalConfigurationAware", "jfs_proc")] = "yes"
        });

        var service = new Service();
        service.SetDomain(new Uri(ConfigurationVocabulary.Namespace));

        var queryCapability = new QueryCapability();
        queryCapability.SetTitle("StrictDoc Configurations Query Capability");
        queryCapability.SetLabel("StrictDoc Configurations Query Capability");
        queryCapability.SetResourceTypes([new Uri(ConfigurationVocabulary.Configuration)]);
        queryCapability.SetResourceShape(new Uri($"{baseUrl}/oslc_config/shapes/configuration"));
        queryCapability.SetQueryBase(new Uri($"{baseUrl}/oslc_config/configurations/query"));
        service.AddQueryCapability(queryCapability);

        var dialog = new Dialog();
        dialog.SetTitle("StrictDoc Configuration Selection Dialog");
        dialog.SetLabel("Select StrictDoc configuration");
        dialog.SetDialog(new Uri($"{baseUrl}/oslc_config/configurations/selector"));
        dialog.SetHintWidth("750px");
        dialog.SetHintHeight("750px");
        dialog.SetResourceTypes([new Uri(ConfigurationVocabulary.Configuration)]);
        service.SetSelectionDialogs([dialog]);

        provider.SetServices([service]);
        return provider;
    }
}
