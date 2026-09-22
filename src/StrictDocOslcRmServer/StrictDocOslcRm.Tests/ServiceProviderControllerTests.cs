using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using OSLC4Net.Domains.RequirementsManagement;
using StrictDocOslcRm.Controllers;
using StrictDocOslcRm.Models;
using StrictDocOslcRm.Services;

namespace StrictDocOslcRm.Tests;

public class ServiceProviderControllerTests : IAsyncDisposable
{
    private readonly ServiceProviderController _controller;
    private readonly IStrictDocService _strictDocService;
    private readonly IBaseUrlService _baseUrlService;
    private readonly IOslcQueryService _oslcQueryService;
    private readonly ILogger<ServiceProviderController> _logger;

    public ServiceProviderControllerTests()
    {
        _strictDocService = Substitute.For<IStrictDocService>();
        _baseUrlService = Substitute.For<IBaseUrlService>();
        _oslcQueryService = Substitute.For<IOslcQueryService>();
        _logger = Substitute.For<ILogger<ServiceProviderController>>();

        _controller = new ServiceProviderController(_logger, _baseUrlService, _strictDocService, _oslcQueryService);
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
    public async Task GetRequirements_WhenDocumentNotFound_ReturnsNotFound()
    {
        // Arrange
        var documentMid = "non-existent-doc";
        _strictDocService.GetDocumentsAsync().Returns(new List<StrictDocDocument>());

        // Act
        var result = await _controller.GetRequirements(documentMid);

        // Assert
        var notFoundResult = await Assert.That(result).IsTypeOf<NotFoundObjectResult>();
        await Assert.That(notFoundResult!.Value).IsEqualTo($"Document with MID '{documentMid}' not found.");
    }

    [Test]
    public async Task GetRequirements_WhenQueryThrowsBadRequestException_ReturnsBadRequest()
    {
        // Arrange
        var documentMid = "doc-1";
        var baseUrl = "http://localhost:8080";
        _baseUrlService.GetBaseUrl().Returns(baseUrl);
        _strictDocService.GetDocumentsAsync().Returns(new List<StrictDocDocument>
        {
            new StrictDocDocument { Mid = documentMid, Title = "Doc 1" }
        });
        _strictDocService.GetRequirementsForDocumentAsync(documentMid, baseUrl).Returns(new List<Requirement>());

        var exceptionMessage = "Invalid oslc.where query parameter";
        _oslcQueryService.Apply(
            Arg.Any<IReadOnlyList<Requirement>>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<int?>(),
            Arg.Any<int>(),
            Arg.Any<Func<int, string>>())
            .Throws(new OslcQueryBadRequestException(exceptionMessage));

        // Act
        var result = await _controller.GetRequirements(documentMid);

        // Assert
        var badRequestResult = await Assert.That(result).IsTypeOf<BadRequestObjectResult>();
        await Assert.That(badRequestResult!.Value).IsEqualTo(exceptionMessage);
    }

    [Test]
    public async Task GetRequirements_WhenQueryThrowsNotImplementedException_Returns501StatusCode()
    {
        // Arrange
        var documentMid = "doc-1";
        var baseUrl = "http://localhost:8080";
        _baseUrlService.GetBaseUrl().Returns(baseUrl);
        _strictDocService.GetDocumentsAsync().Returns(new List<StrictDocDocument>
        {
            new StrictDocDocument { Mid = documentMid, Title = "Doc 1" }
        });
        _strictDocService.GetRequirementsForDocumentAsync(documentMid, baseUrl).Returns(new List<Requirement>());

        var exceptionMessage = "Query feature not supported";
        _oslcQueryService.Apply(
            Arg.Any<IReadOnlyList<Requirement>>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<int?>(),
            Arg.Any<int>(),
            Arg.Any<Func<int, string>>())
            .Throws(new OslcQueryNotImplementedException(exceptionMessage));

        // Act
        var result = await _controller.GetRequirements(documentMid);

        // Assert
        var objectResult = await Assert.That(result).IsTypeOf<ObjectResult>();
        await Assert.That(objectResult!.StatusCode).IsEqualTo(501);
        await Assert.That(objectResult.Value).IsEqualTo(exceptionMessage);
    }

    [Test]
    public async Task GetRequirements_WhenQuerySucceeds_ReturnsOkWithResponseInfo()
    {
        // Arrange
        var documentMid = "doc-1";
        var baseUrl = "http://localhost:8080";
        _baseUrlService.GetBaseUrl().Returns(baseUrl);
        _strictDocService.GetDocumentsAsync().Returns(new List<StrictDocDocument>
        {
            new StrictDocDocument { Mid = documentMid, Title = "Doc 1" }
        });

        var req1 = new Requirement { Identifier = "REQ-1", Title = "Req 1" };
        _strictDocService.GetRequirementsForDocumentAsync(documentMid, baseUrl).Returns(new List<Requirement> { req1 });

        var outcome = new OslcQueryOutcome(
            Members: new[] { req1 },
            TotalCount: 1,
            SelectedProperties: new Dictionary<string, object>(),
            NextPage: null);

        _oslcQueryService.Apply(
            Arg.Any<IReadOnlyList<Requirement>>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<int?>(),
            Arg.Any<int>(),
            Arg.Any<Func<int, string>>())
            .Returns(outcome);

        // Act
        var result = await _controller.GetRequirements(documentMid);

        // Assert
        var okResult = await Assert.That(result).IsTypeOf<OkObjectResult>();
        await Assert.That(okResult!.Value).IsNotNull();
    }
}
