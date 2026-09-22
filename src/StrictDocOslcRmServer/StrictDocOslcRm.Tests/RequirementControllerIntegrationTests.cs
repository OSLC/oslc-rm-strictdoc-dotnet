using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using OSLC4Net.Domains.RequirementsManagement;
using OSLC4Net.Server.Providers;
using StrictDocOslcRm.Services;

namespace StrictDocOslcRm.Tests;

public class RequirementControllerIntegrationTests
{
    private static IHost CreateHost(
        IStrictDocService strictDocService,
        IBaseUrlService baseUrlService,
        ILinkSidecarService linkSidecarService,
        IOslcQueryService oslcQueryService)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers(options =>
                    {
                        options.OutputFormatters.Insert(0, new OslcRdfOutputFormatter());
                        options.InputFormatters.Insert(0, new OslcRdfInputFormatter());
                    })
                    .AddApplicationPart(typeof(StrictDocOslcRm.Controllers.RequirementController).Assembly);

                    services.AddSingleton(strictDocService);
                    services.AddSingleton(baseUrlService);
                    services.AddSingleton(linkSidecarService);
                    services.AddSingleton(oslcQueryService);
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });
            })
            .Start();

        return host;
    }

    [Test]
    public async Task GetRequirement_ReturnsTurtle()
    {
        // Arrange
        var strictDocService = Substitute.For<IStrictDocService>();
        var baseUrlService = Substitute.For<IBaseUrlService>();
        var linkSidecarService = Substitute.For<ILinkSidecarService>();
        var oslcQueryService = Substitute.For<IOslcQueryService>();

        var uid = "REQ-001";
        var baseUrl = "http://localhost";
        baseUrlService.GetBaseUrl().Returns(baseUrl);

        var requirement = new Requirement
        {
            Identifier = uid,
            Title = "Test Integration Requirement",
            Description = "This is a test integration requirement"
        };
        strictDocService.GetAllRequirementsAsync(baseUrl).Returns(new List<Requirement> { requirement });

        using var host = CreateHost(strictDocService, baseUrlService, linkSidecarService, oslcQueryService);
        using var client = host.GetTestClient();

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/?a={uid}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/turtle"));
        using var response = await client.SendAsync(request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();

        await Assert.That(content).Contains("Test Integration Requirement");
        await Assert.That(content).Contains("This is a test integration requirement");
        await Assert.That(content).Contains("http://open-services.net/ns/rm#Requirement");
    }

    [Test]
    public async Task GetRequirement_ReturnsRdfXml()
    {
        // Arrange
        var strictDocService = Substitute.For<IStrictDocService>();
        var baseUrlService = Substitute.For<IBaseUrlService>();
        var linkSidecarService = Substitute.For<ILinkSidecarService>();
        var oslcQueryService = Substitute.For<IOslcQueryService>();

        var uid = "REQ-002";
        var baseUrl = "http://localhost";
        baseUrlService.GetBaseUrl().Returns(baseUrl);

        var requirement = new Requirement
        {
            Identifier = uid,
            Title = "XML Integration Requirement",
            Description = "This requirement is retrieved in XML format"
        };
        strictDocService.GetAllRequirementsAsync(baseUrl).Returns(new List<Requirement> { requirement });

        using var host = CreateHost(strictDocService, baseUrlService, linkSidecarService, oslcQueryService);
        using var client = host.GetTestClient();

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/?a={uid}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/rdf+xml"));
        using var response = await client.SendAsync(request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();

        await Assert.That(content).Contains("XML Integration Requirement");
        await Assert.That(content).Contains("This requirement is retrieved in XML format");
        await Assert.That(content).Contains("<rdf:RDF");
    }
}
