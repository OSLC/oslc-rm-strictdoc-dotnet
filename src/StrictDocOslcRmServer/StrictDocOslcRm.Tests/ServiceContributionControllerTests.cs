using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using StrictDocOslcRm.Controllers;
using StrictDocOslcRm.Services;

namespace StrictDocOslcRm.Tests;

public sealed class ServiceContributionControllerTests
{
    [Test]
    public async Task Scr_UsesJazzServiceContributionResourceNodes()
    {
        var baseUrlService = Substitute.For<IBaseUrlService>();
        baseUrlService.GetBaseUrl().Returns("https://strictdoc.example.test");
        var logger = Substitute.For<ILogger<ServiceContributionController>>();
        var controller = new ServiceContributionController(logger, baseUrlService);

        var result = controller.GetServiceContributionResource() as ContentResult;

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.ContentType).Contains("application/rdf+xml");
        await Assert.That(result.Content).Contains("<oslc_rm:RmServiceProviders>");
        await Assert.That(result.Content).Contains("<oslc_config:CmServiceProviders>");
        await Assert.That(result.Content).DoesNotContain("<oslc_rm:rmServiceProviders rdf:resource=");
        await Assert.That(result.Content).DoesNotContain("<oslc_config:cmServiceProviders rdf:resource=");

        var document = XDocument.Parse(result.Content!, LoadOptions.PreserveWhitespace);
        XNamespace rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        XNamespace dcterms = "http://purl.org/dc/terms/";
        XNamespace oslcRm = "http://open-services.net/xmlns/rm/1.0/";
        XNamespace oslcConfig = "http://open-services.net/ns/config#";
        XNamespace jazzDiscovery = "http://jazz.net/xmlns/prod/jazz/discovery/1.0/";

        var root = document.Root!;
        await Assert.That(root.Name).IsEqualTo(rdf + "RDF");

        var application = root.Elements(jazzDiscovery + "Application").Single();
        await Assert.That(application.Element(jazzDiscovery + "contextRoot")!.Value).IsEqualTo(
            "https://strictdoc.example.test"
        );
        await Assert.That(application.Element(jazzDiscovery + "rootServices")!.Attribute(rdf + "resource")!.Value)
            .IsEqualTo("https://strictdoc.example.test/rootservices");

        var domains = application
            .Elements(jazzDiscovery + "domain")
            .Select(domain => domain.Element(dcterms + "identifier")!.Value)
            .ToArray();
        await Assert.That(domains.Length).IsEqualTo(1);
        await Assert.That(domains[0]).IsEqualTo("http://open-services.net/ns/rm#");

        var rmContribution = root.Elements(oslcRm + "RmServiceProviders").Single();
        await Assert.That(rmContribution.Element(jazzDiscovery + "service")!.Attribute(rdf + "resource")!.Value)
            .IsEqualTo("https://strictdoc.example.test/oslc/catalog");

        var configContribution = root.Elements(oslcConfig + "CmServiceProviders").Single();
        await Assert.That(configContribution.Element(jazzDiscovery + "service")!.Attribute(rdf + "resource")!.Value)
            .IsEqualTo("https://strictdoc.example.test/oslc_config/catalog");
    }
}
