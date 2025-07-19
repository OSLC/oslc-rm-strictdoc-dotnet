using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using StrictDocOslcRm.Services;

namespace StrictDocOslcRm.Controllers;
/// <summary>
/// OSLC Service Provider Catalog for StrictDoc documents.
/// Returns a service provider for each StrictDoc document.
/// </summary>
[ApiController]
[Route("/oslc/catalog")]
[Produces("application/rdf+xml", "text/turtle", "application/ld+json")]
public class CatalogController(ILogger<CatalogController> logger, IStrictDocService strictDocService) : ControllerBase
{
    [HttpGet]
    public async Task<OSLC4Net.Core.Model.ServiceProviderCatalog> Get()
    {
        var catalog = new OSLC4Net.Core.Model.ServiceProviderCatalog();
        catalog.SetAbout(new Uri(Request.GetEncodedUrl()));

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
        var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
        var serviceProviderUri = new Uri($"{baseUrl}/oslc/service_provider/{document.Mid}");
        serviceProvider.SetAbout(serviceProviderUri);

        // Set identifier using the MID
        serviceProvider.SetIdentifier(document.Mid);

        // Set title using the TITLE
        serviceProvider.SetTitle(document.Title);

        // Set description
        serviceProvider.SetDescription($"OSLC Requirements Management service for StrictDoc document: {document.Title}");

        return serviceProvider;
    }
}