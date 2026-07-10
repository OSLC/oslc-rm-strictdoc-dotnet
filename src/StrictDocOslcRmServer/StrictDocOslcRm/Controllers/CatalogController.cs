using Microsoft.AspNetCore.Mvc;
using OSLC4Net.Core.Model;
using OSLC4Net.Domains.RequirementsManagement;
using StrictDocOslcRm.Services;

namespace StrictDocOslcRm.Controllers;
/// <summary>
/// OSLC Service Provider Catalog for StrictDoc documents.
/// Returns a service provider for each StrictDoc document.
/// </summary>
[ApiController]
[Route("/oslc/catalog")]
[Produces("application/rdf+xml", "text/turtle", "application/ld+json")]
public class CatalogController(
    ILogger<CatalogController> logger,
    IBaseUrlService baseUrlService,
    IStrictDocService strictDocService,
    IConfigurationContextService configurationContextService) : ControllerBase
{
    [HttpGet]
    public async Task<OSLC4Net.Core.Model.ServiceProviderCatalog> Get()
    {
        var catalog = new OSLC4Net.Core.Model.ServiceProviderCatalog();
        catalog.SetAbout(new Uri($"{baseUrlService.GetBaseUrl()}/oslc/catalog"));
        catalog.SetTitle("StrictDoc Requirements Management Service Provider Catalog");
        catalog.SetDescription(
            "Service provider catalog for the StrictDoc Requirements Management server");
        catalog.AddDomain(new Uri("http://open-services.net/ns/rm#"));

        try
        {
            var defaultContext = await configurationContextService
                .GetDefaultAsync(HttpContext.RequestAborted)
                .ConfigureAwait(false);
            var documents = await strictDocService.GetDocumentsAsync(defaultContext, HttpContext.RequestAborted);

            foreach (var document in documents)
            {
                var serviceProvider = CreateServiceProvider(document);
                catalog.AddServiceProvider(serviceProvider);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating service provider catalog");
        }

        return catalog;
    }

    private OSLC4Net.Core.Model.ServiceProvider CreateServiceProvider(StrictDocOslcRm.Models.StrictDocDocument document)
    {
        var serviceProvider = new OSLC4Net.Core.Model.ServiceProvider();

        // Set the about URI for this service provider using the document MID
        var baseUrl = baseUrlService.GetBaseUrl();
        var serviceProviderUri = new Uri($"{baseUrl}/oslc/service_provider/{document.Mid}");
        serviceProvider.SetAbout(serviceProviderUri);
        serviceProvider.SetDetails([serviceProviderUri]);

        // Set identifier using the MID
        serviceProvider.SetIdentifier(document.Mid);

        // Set title using the TITLE
        serviceProvider.SetTitle(document.Title);

        // Set description
        serviceProvider.SetDescription($"OSLC Requirements Management service for StrictDoc document: {document.Title}");

        var service = new Service();
        service.SetDomain(new Uri("http://open-services.net/ns/rm#"));

        var queryCapability = new QueryCapability();
        queryCapability.SetTitle("StrictDoc Requirements Query Capability");
        queryCapability.SetLabel("StrictDoc Requirements Query Capability");
        queryCapability.SetResourceTypes(
            [new Uri("http://open-services.net/ns/rm#Requirement")]);
        queryCapability.SetResourceShape(
            new Uri($"{baseUrl}/oslc/shapes/requirement"));
        queryCapability.SetQueryBase(
            new Uri($"{serviceProviderUri}/requirements"));
        service.AddQueryCapability(queryCapability);

        var selectionDialog = new Dialog();
        selectionDialog.SetTitle("Requirement Selection Dialog");
        selectionDialog.SetLabel("Select Requirement");
        selectionDialog.SetDialog(
            new Uri($"{serviceProviderUri}/requirements/selector"));
        selectionDialog.SetHintWidth("500px");
        selectionDialog.SetHintHeight("500px");
        selectionDialog.SetResourceTypes(
            [new Uri("http://open-services.net/ns/rm#Requirement")]);
        service.SetSelectionDialogs([selectionDialog]);

        serviceProvider.SetServices([service]);

        return serviceProvider;
    }
}
