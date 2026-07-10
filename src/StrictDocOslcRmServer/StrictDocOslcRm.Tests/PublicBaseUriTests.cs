using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using StrictDocOslcRm.Services;

namespace StrictDocOslcRm.Tests;

public sealed class PublicBaseUriTests
{
    [Test]
    public async Task ConfiguredPublicBaseUri_IsUsedByBaseUrlServiceAndRequestDerivedUris()
    {
        const string publicBaseUri = "https://strictdoc.example.test/rm";
        var configuration = CreateConfiguration(publicBaseUri);
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost", 51025);
        context.Request.Path = "/oslc_config/configurations/query";
        context.Request.QueryString = new QueryString("?oslc.searchTerms=main");

        var accessor = new HttpContextAccessor { HttpContext = context };
        var baseUrlService = new BaseUrlService(
            configuration,
            accessor,
            NullLogger<BaseUrlService>.Instance);

        await Assert.That(baseUrlService.GetBaseUrl()).IsEqualTo(publicBaseUri);
        await Assert.That(PublicBaseUri.TryGetConfigured(configuration, out var configuredUri)).IsTrue();

        PublicBaseUri.ApplyTo(context.Request, configuredUri);

        await Assert.That(context.Request.GetEncodedUrl()).IsEqualTo(
            "https://strictdoc.example.test/rm/oslc_config/configurations/query?oslc.searchTerms=main");
    }

    [Test]
    public async Task PublicBaseUri_RejectsValuesThatAreNotPublicHttpOrigins()
    {
        var configuration = CreateConfiguration("https://strictdoc.example.test/?unexpected=query");

        await Assert.That(PublicBaseUri.TryGetConfigured(configuration, out _)).IsFalse();
    }

    private static IConfiguration CreateConfiguration(string publicBaseUri) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OSLC:PublicBaseUri"] = publicBaseUri
            })
            .Build();
}
