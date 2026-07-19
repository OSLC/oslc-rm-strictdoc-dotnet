using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using StrictDocOslcRm.Controllers;
using StrictDocOslcRm.Models;
using StrictDocOslcRm.Services;

namespace StrictDocOslcRm.Tests;

public sealed class ConfigurationControllerTests : IDisposable
{
    private readonly IBaseUrlService _baseUrlService = Substitute.For<IBaseUrlService>();
    private readonly IConfigurationContextService _configurationContexts = Substitute.For<IConfigurationContextService>();
    private readonly IConfigurationBaselineService _baselineService = Substitute.For<IConfigurationBaselineService>();
    private readonly ConfigurationController _controller;

    public ConfigurationControllerTests()
    {
        _controller = new ConfigurationController(_baseUrlService, _configurationContexts, _baselineService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        _baseUrlService.GetBaseUrl().Returns("https://strictdoc.example");
    }

    public void Dispose() => _controller.Dispose();

    [Test]
    public async Task CreateBaselineFromStream_UsesRdfIdentifierAndReturnsCreatedBaseline()
    {
        const string body = """
                            <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"
                                     xmlns:dcterms="http://purl.org/dc/terms/">
                              <rdf:Description>
                                <dcterms:identifier>v0.2.0</dcterms:identifier>
                                <dcterms:title>Release 0.2</dcterms:title>
                              </rdf:Description>
                            </rdf:RDF>
                            """;
        var stream = new ConfigurationContext("main", "HEAD", "/data/main/HEAD");
        var baseline = new ConfigurationContext("main", "v0.2.0", "/data/main/v0.2.0");
        _controller.Request.ContentType = "application/rdf+xml";
        _controller.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        _baselineService
            .CreateAsync("main", "v0.2.0", Arg.Any<CancellationToken>())
            .Returns(new BaselineCreationResult(stream, baseline));

        var result = await _controller.CreateBaselineFromStream("main");

        var created = result as CreatedAtActionResult;
        await Assert.That(created).IsNotNull();
        await Assert.That(created!.ActionName).IsEqualTo(nameof(ConfigurationController.GetConfiguration));
        await Assert.That(created.RouteValues!["branch"]).IsEqualTo("main");
        await Assert.That(created.RouteValues["tag"]).IsEqualTo("v0.2.0");
        await _baselineService.Received(1).CreateAsync("main", "v0.2.0", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetBaselines_ReturnsLdpContainerForBranchBaselines()
    {
        var stream = new ConfigurationContext("main", "HEAD", "/data/main/HEAD");
        var baseline = new ConfigurationContext("main", "v0.1.0", "/data/main/v0.1.0");
        _configurationContexts.GetAsync("main", "HEAD", Arg.Any<CancellationToken>()).Returns(stream);
        _configurationContexts.GetAllAsync(Arg.Any<CancellationToken>()).Returns(
            (IReadOnlyList<ConfigurationContext>)[stream, baseline]);

        var result = await _controller.GetBaselines("main");

        var ok = result as OkObjectResult;
        var container = ok?.Value as GenericConfigurationContainer;
        await Assert.That(container).IsNotNull();
        await Assert.That(container!.About).IsEqualTo(
            new Uri("https://strictdoc.example/oslc_config/configurations/main/HEAD/baselines"));
        await Assert.That(container.Members).IsEquivalentTo(
            [new Uri("https://strictdoc.example/oslc_config/configurations/main/v0.1.0")]);
    }
}
