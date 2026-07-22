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
    IStrictDocService strictDocService) : ControllerBase
{
    [HttpGet]
    public async Task<OSLC4Net.Core.Model.ServiceProviderCatalog> Get()
    {
        var catalog = new OSLC4Net.Core.Model.ServiceProviderCatalog();
        var baseUrl = baseUrlService.GetBaseUrl().TrimEnd('/');
        catalog.SetAbout(new Uri($"{baseUrl}/oslc/catalog"));
        catalog.SetTitle("StrictDoc Requirements Management Service Provider Catalog");
        catalog.SetDescription(
            "Service provider catalog for the StrictDoc Requirements Management server");
        catalog.AddDomain(new Uri(RM.NS));

        try
        {
            var documents = await strictDocService.GetDocumentsAsync();

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
        var baseUrl = baseUrlService.GetBaseUrl().TrimEnd('/');
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
        service.SetDomain(new Uri(RM.NS));

        var queryCapability = new QueryCapability();
        queryCapability.SetTitle("StrictDoc Requirements Query Capability");
        queryCapability.SetLabel("StrictDoc Requirements Query Capability");
        queryCapability.SetResourceTypes(
            [new Uri(RM.Requirement)]);
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
        selectionDialog.SetHintWidth("650px");
        selectionDialog.SetHintHeight("500px");
        selectionDialog.SetResourceTypes(
            [new Uri(RM.Requirement)]);
        service.SetSelectionDialogs([selectionDialog]);

        serviceProvider.SetServices([service]);

        return serviceProvider;
    }
}
