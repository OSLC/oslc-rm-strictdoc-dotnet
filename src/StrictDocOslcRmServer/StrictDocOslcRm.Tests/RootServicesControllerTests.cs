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
    public async Task RootServices_AdvertisesJazzRmCatalogDescriptor()
    {
        var baseUrlService = Substitute.For<IBaseUrlService>();
        baseUrlService.GetBaseUrl().Returns("https://strictdoc.example.test/");
        var logger = Substitute.For<ILogger<RootServicesController>>();
        var configuration = new ConfigurationBuilder().Build();
        var controller = new RootServicesController(logger, baseUrlService, configuration);

        var result = controller.GetRootServices() as ContentResult;

        await Assert.That(result).IsNotNull();
        var document = XDocument.Parse(result!.Content!);
        XNamespace rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        XNamespace oslc = "http://open-services.net/ns/core#";
        XNamespace jazzDiscovery = "http://jazz.net/xmlns/prod/jazz/discovery/1.0/";
        var catalog = document.Root!
            .Element(jazzDiscovery + "oslcCatalogs")!
            .Element(oslc + "ServiceProviderCatalog");

        await Assert.That(catalog?.Attribute(rdf + "about")?.Value)
            .IsEqualTo("https://strictdoc.example.test/oslc/catalog");
        await Assert.That(catalog?.Element(oslc + "domain")?.Attribute(rdf + "resource")?.Value)
            .IsEqualTo("http://open-services.net/ns/rm#");
    }

    [Test]
    public async Task RootServices_AdvertisesJazzOAuth1Endpoints()
    {
        var baseUrlService = Substitute.For<IBaseUrlService>();
        baseUrlService.GetBaseUrl().Returns("https://strictdoc.example.test/");
        var logger = Substitute.For<ILogger<RootServicesController>>();
        var configuration = new ConfigurationBuilder().Build();
        var controller = new RootServicesController(logger, baseUrlService, configuration);

        var result = controller.GetRootServices() as ContentResult;

        await Assert.That(result).IsNotNull();
        var document = XDocument.Parse(result!.Content!);
        XNamespace rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        XNamespace jazzFoundation = "http://jazz.net/xmlns/prod/jazz/jfs/1.0/";
        var root = document.Root!;

        await Assert.That(root.Element(jazzFoundation + "oauthRealmName")?.Value)
            .IsEqualTo("OSLC RM for StrictDoc");
        await Assert.That(root.Element(jazzFoundation + "oauthDomain")?.Value)
            .IsEqualTo("https://strictdoc.example.test");
        await Assert.That(root.Element(jazzFoundation + "oauthRequestConsumerKeyUrl")?
                .Attribute(rdf + "resource")?.Value)
            .IsEqualTo("https://strictdoc.example.test/oauth/request_consumer_key");
        await Assert.That(root.Element(jazzFoundation + "oauthApprovalModuleUrl")?
                .Attribute(rdf + "resource")?.Value)
            .IsEqualTo("https://strictdoc.example.test/oauth/approve_consumer_key");
        await Assert.That(root.Element(jazzFoundation + "oauthRequestTokenUrl")?
                .Attribute(rdf + "resource")?.Value)
            .IsEqualTo("https://strictdoc.example.test/oauth/request_token");
        await Assert.That(root.Element(jazzFoundation + "oauthUserAuthorizationUrl")?
                .Attribute(rdf + "resource")?.Value)
            .IsEqualTo("https://strictdoc.example.test/oauth/authorize");
        await Assert.That(root.Element(jazzFoundation + "oauthAccessTokenUrl")?
                .Attribute(rdf + "resource")?.Value)
            .IsEqualTo("https://strictdoc.example.test/oauth/access_token");
    }
}
