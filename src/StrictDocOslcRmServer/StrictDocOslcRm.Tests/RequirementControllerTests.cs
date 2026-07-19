using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OSLC4Net.Domains.RequirementsManagement;
using StrictDocOslcRm.Controllers;
using StrictDocOslcRm.Services;

namespace StrictDocOslcRm.Tests;

public class RequirementControllerTests : IAsyncDisposable
{
    private readonly RequirementController _controller;
    private readonly IStrictDocService _strictDocService;
    private readonly IBaseUrlService _baseUrlService;
    private readonly ILinkSidecarService _linkSidecarService;
    private readonly IConfigurationContextService _configurationContextService;
    private readonly ILogger<RequirementController> _logger;
    private readonly ConfigurationContext _context = new("main", "HEAD", Path.GetTempPath());

    public RequirementControllerTests()
    {
        _strictDocService = Substitute.For<IStrictDocService>();
        _baseUrlService = Substitute.For<IBaseUrlService>();
        _linkSidecarService = Substitute.For<ILinkSidecarService>();
        _configurationContextService = Substitute.For<IConfigurationContextService>();
        _logger = Substitute.For<ILogger<RequirementController>>();

        _controller = new RequirementController(
            _logger,
            _baseUrlService,
            _strictDocService,
            _linkSidecarService,
            _configurationContextService);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    public ValueTask DisposeAsync()
    {
        try
        {
            _controller.Dispose();
        }
        catch
        {
            // Ignore any exceptions during disposal
        }
        return ValueTask.CompletedTask;
    }

    [Test]
    public async Task GetRequirementResource_ReturnsOk()
    {
        // Arrange
        var uid = "REQ-001";
        var baseUrl = "http://localhost:8080";
        _baseUrlService.GetBaseUrl().Returns(baseUrl);
        _configurationContextService
            .ResolveAsync(Arg.Any<HttpRequest>(), baseUrl, Arg.Any<CancellationToken>())
            .Returns(_context);

        var requirement = new Requirement
        {
            Identifier = uid,
            Title = "Test Requirement",
            Description = "This is a test requirement"
        };
        _strictDocService.GetAllRequirementsAsync(_context, baseUrl, Arg.Any<CancellationToken>())
            .Returns(new List<Requirement> { requirement });

        // Act
        var result = await _controller.GetRequirementResource(uid, null, null).ConfigureAwait(false);

        // Assert
        var okResult = result as OkObjectResult;
        var returnedRequirement = okResult?.Value as Requirement;
        await Assert.That(returnedRequirement?.InstanceShape).IsEqualTo(new Uri($"{baseUrl}/oslc/shapes/requirement"));
        await Assert.That(_controller.Response.Headers["Link"].ToString())
            .Contains("<http://localhost:8080/oslc/shapes/requirement>; rel=\"http://open-services.net/ns/core#instanceShape\"");
        await Verify(okResult?.Value).ConfigureAwait(false);
    }

    [Test]
    public async Task PutRequirementResource_StoresSidecarLinks()
    {
        // Arrange
        var uid = "REQ-001";
        var baseUrl = "http://localhost:8080";
        var body = """
                   <?xml version="1.0" encoding="utf-8"?>
                   <rdf:RDF
                       xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"
                       xmlns:oslc_rm="http://open-services.net/ns/rm#">
                     <rdf:Description rdf:about="http://localhost:8080/?a=REQ-001">
                       <oslc_rm:affectedBy rdf:resource="https://jazz.example/ccm/resource/itemName/com.ibm.team.workitem.WorkItem/1" />
                     </rdf:Description>
                   </rdf:RDF>
                   """;

        _baseUrlService.GetBaseUrl().Returns(baseUrl);
        _configurationContextService
            .ResolveAsync(Arg.Any<HttpRequest>(), baseUrl, Arg.Any<CancellationToken>())
            .Returns(_context);
        _strictDocService.GetAllRequirementsAsync(_context, baseUrl, Arg.Any<CancellationToken>()).Returns(new List<Requirement>
        {
            new()
            {
                Identifier = uid,
                Title = "Test Requirement",
                Description = "This is a test requirement"
            }
        });

        _controller.Request.ContentType = "application/rdf+xml";
        _controller.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));

        // Act
        var result = await _controller.PutRequirementResource(uid).ConfigureAwait(false);

        // Assert
        var okResult = result as OkObjectResult;
        var returnedRequirement = okResult?.Value as Requirement;
        await Assert.That(returnedRequirement?.InstanceShape).IsEqualTo(new Uri($"{baseUrl}/oslc/shapes/requirement"));
        await _linkSidecarService.Received(1).ReplaceLinksAsync(
            _context,
            new Uri($"{baseUrl}/?a={uid}"),
            "application/rdf+xml",
            body,
            new Uri(baseUrl),
            Arg.Any<CancellationToken>());
        await _linkSidecarService.Received(1).ApplyLinksAsync(
            _context,
            Arg.Is<Requirement>(requirement => requirement.Identifier == uid),
            new Uri($"{baseUrl}/?a={uid}"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task PutRequirementResource_RejectsImmutableConfigurationBeforeReadingTheRequestBody()
    {
        const string uid = "REQ-001";
        const string baseUrl = "http://localhost:8080";
        var baseline = new ConfigurationContext("main", "v0.1.0", Path.GetTempPath());
        _baseUrlService.GetBaseUrl().Returns(baseUrl);
        _configurationContextService
            .ResolveAsync(Arg.Any<HttpRequest>(), baseUrl, Arg.Any<CancellationToken>())
            .Returns(baseline);

        var result = await _controller.PutRequirementResource(uid);

        await Assert.That(result).IsTypeOf<ConflictObjectResult>();
        await _linkSidecarService.DidNotReceive().ReplaceLinksAsync(
            Arg.Any<ConfigurationContext>(),
            Arg.Any<Uri>(),
            Arg.Any<string?>(),
            Arg.Any<string>(),
            Arg.Any<Uri>(),
            Arg.Any<CancellationToken>());
    }
}
