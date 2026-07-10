using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using StrictDocOslcRm.Controllers;
using StrictDocOslcRm.Services;

namespace StrictDocOslcRm.Tests;

public sealed class RootServicesControllerTests
{
    [Test]
    public async Task RootServices_UsesSingleDomainXmlLayoutWithSimpleConfigurationCatalogPointer()
    {
        var baseUrlService = Substitute.For<IBaseUrlService>();
        baseUrlService.GetBaseUrl().Returns("https://strictdoc.example.test");
        var logger = Substitute.For<ILogger<RootServicesController>>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OSLC:ServiceTitle"] = "StrictDoc test server"
            })
            .Build();
        var controller = new RootServicesController(logger, baseUrlService, configuration);

        var result = controller.GetRootServices() as ContentResult;

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Content).Contains("xmlns:dcterms=\"http://purl.org/dc/terms/\"");
        await Assert.That(result.Content).Contains("<dcterms:title>StrictDoc test server</dcterms:title>");
        await Assert.That(result.Content).Contains("xmlns:oslc_config=\"http://open-services.net/ns/config#\"");
        await Assert.That(result.Content).Contains(
            "<oslc_config:cmServiceProviders rdf:resource=\"https://strictdoc.example.test/oslc_config/catalog\" />"
        );
        await Assert.That(result.Content).DoesNotContain(
            "<oslc:ServiceProviderCatalog rdf:about=\"https://strictdoc.example.test/oslc_config/catalog\">"
        );
    }
}
