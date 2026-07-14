using System.Xml.Linq;
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

        var document = XDocument.Parse(result.Content!, LoadOptions.PreserveWhitespace);
        XNamespace rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        XNamespace oslc = "http://open-services.net/ns/core#";
        XNamespace oslcRm = "http://open-services.net/xmlns/rm/1.0/";
        XNamespace oslcConfig = "http://open-services.net/ns/config#";
        XNamespace jazzDiscovery = "http://jazz.net/xmlns/prod/jazz/discovery/1.0/";

        var root = document.Root!;
        await Assert.That(root.Name).IsEqualTo(rdf + "Description");
        await Assert.That(root.Attribute(rdf + "about")!.Value).IsEqualTo(
            "https://strictdoc.example.test/.well-known/oslc/rootservices.xml"
        );
        await Assert.That(root.Element(oslcRm + "rmServiceProviders")!.Attribute(rdf + "resource")!.Value)
            .IsEqualTo("https://strictdoc.example.test/oslc/catalog");
        await Assert.That(root.Element(oslcConfig + "cmServiceProviders")!.Attribute(rdf + "resource")!.Value)
            .IsEqualTo("https://strictdoc.example.test/oslc_config/catalog");
        await Assert.That(root.Elements(oslcConfig + "CmServiceProviders").Count()).IsEqualTo(0);

        var nestedCatalogs = root
            .Element(jazzDiscovery + "oslcCatalogs")!
            .Elements(oslc + "ServiceProviderCatalog")
            .ToArray();
        await Assert.That(nestedCatalogs.Length).IsEqualTo(1);
        await Assert.That(nestedCatalogs[0].Attribute(rdf + "about")!.Value).IsEqualTo(
            "https://strictdoc.example.test/oslc/catalog"
        );
    }
}
