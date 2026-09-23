using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OSLC4Net.Domains.RequirementsManagement;
using StrictDocOslcRm.Controllers;
using StrictDocOslcRm.Models;
using StrictDocOslcRm.Services;

namespace StrictDocOslcRm.Tests;

public class RequirementControllerTests : IAsyncDisposable
{
    private readonly RequirementController _controller;
    private readonly IStrictDocService _strictDocService;
    private readonly IBaseUrlService _baseUrlService;
    private readonly ILogger<RequirementController> _logger;
    private readonly IRequirementMarkupSanitizer _requirementMarkupSanitizer;

    public RequirementControllerTests()
    {
        _strictDocService = Substitute.For<IStrictDocService>();
        _baseUrlService = Substitute.For<IBaseUrlService>();
        _logger = Substitute.For<ILogger<RequirementController>>();
        _requirementMarkupSanitizer = new RequirementMarkupSanitizer();

        _controller = new RequirementController(_logger, _baseUrlService, _strictDocService, _requirementMarkupSanitizer);
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

        var requirement = new Requirement
        {
            Identifier = uid,
            Title = "Test Requirement",
            Description = "This is a test requirement"
        };
        _strictDocService.GetRequirementByUidAsync(uid).Returns(requirement);

        // Act
        var result = await _controller.GetRequirementResource(uid, null, null).ConfigureAwait(false);

        // Assert
        var okResult = result as OkObjectResult;
        var returnedRequirement = okResult?.Value as Requirement;
        await Assert.That(returnedRequirement?.InstanceShape)
            .IsEqualTo(new Uri($"{baseUrl}/oslc/shapes/requirement"));
        await Assert.That(_controller.Response.Headers.Link.ToString())
            .Contains($"<{baseUrl}/oslc/shapes/requirement>; rel=\"http://open-services.net/ns/core#instanceShape\"");
        await Verify(okResult?.Value).ConfigureAwait(false);
    }

    [Test]
    [Arguments("small")]
    [Arguments("large")]
    public async Task GetRequirementResource_PreviewType_SanitizesXssPayloads(string previewType)
    {
        // Arrange
        var uid = "REQ-XSS";
        var baseUrl = "http://localhost:8080";
        _baseUrlService.GetBaseUrl().Returns(baseUrl);
        _controller.Request.Headers.Accept = "text/html";

        var xssPayloadTitle = "<script>alert('xss-title')</script>";
        var xssPayloadDesc = "<img src=x onerror=alert('xss-desc')>";

        var requirement = new Requirement
        {
            Identifier = uid,
            Title = xssPayloadTitle,
            Description = xssPayloadDesc
        };
        _strictDocService.GetRequirementByUidAsync(uid).Returns(requirement);

        // Act
        var result = await _controller.GetRequirementResource(uid, null, previewType).ConfigureAwait(false);

        // Assert
        var viewResult = result as ViewResult;
        await Assert.That(viewResult).IsNotNull();
        await Assert.That(viewResult!.Model).IsTypeOf<RequirementPreviewViewModel>();

        var model = (RequirementPreviewViewModel)viewResult.Model!;
        await Assert.That(model.Requirement.Title).IsEqualTo(xssPayloadTitle);
        await Assert.That(model.Requirement.Description).IsEqualTo(xssPayloadDesc);

        await Assert.That(model.SanitizedTitle.ToString()).DoesNotContain("<script");
        await Assert.That(model.SanitizedDescription.ToString()).DoesNotContain("<img");
        await Assert.That(model.SanitizedDescription.ToString()).DoesNotContain("onerror");
    }
}
