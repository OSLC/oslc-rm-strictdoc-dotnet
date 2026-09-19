using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using NSubstitute;
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
        _controller.TempData = Substitute.For<ITempDataDictionary>();
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
    public async Task RequirementSelector_WithTerms_FiltersRequirementsByTitleAndIdentifierCaseInsensitively()
    {
        // Arrange
        var baseUrl = "http://localhost:8080";
        var docMid = "doc-123";
        _baseUrlService.GetBaseUrl().Returns(baseUrl);

        var req1 = new Requirement { Identifier = "REQ-101", Title = "System Safety Requirement" };
        var req2 = new Requirement { Identifier = "REQ-102", Title = "User Authentication" };
        var req3 = new Requirement { Identifier = "REQ-201", Title = "Performance Monitoring" };

        _strictDocService.GetRequirementsForDocumentAsync(docMid, baseUrl)
            .Returns(new List<Requirement> { req1, req2, req3 });

        // Act
        var actionResult = await _controller.RequirementSelector(docMid, "  safety  ");

        // Assert
        var viewResult = actionResult as ViewResult;
        await Assert.That(viewResult).IsNotNull();
        var model = viewResult?.Model as RequirementSelectionViewModel;
        await Assert.That(model).IsNotNull();

        var results = model!.Results.ToList();
        await Assert.That(results.Count).IsEqualTo(1);
        await Assert.That(results[0].Identifier).IsEqualTo("REQ-101");
    }

    [Test]
    public async Task RequirementSelector_WithTermsMatchingIdentifier_ReturnsMatchingRequirements()
    {
        // Arrange
        var baseUrl = "http://localhost:8080";
        var docMid = "doc-123";
        _baseUrlService.GetBaseUrl().Returns(baseUrl);

        var req1 = new Requirement { Identifier = "REQ-101", Title = "System Safety Requirement" };
        var req2 = new Requirement { Identifier = "REQ-102", Title = "User Authentication" };

        _strictDocService.GetRequirementsForDocumentAsync(docMid, baseUrl)
            .Returns(new List<Requirement> { req1, req2 });

        // Act
        var actionResult = await _controller.RequirementSelector(docMid, "102");

        // Assert
        var viewResult = actionResult as ViewResult;
        await Assert.That(viewResult).IsNotNull();
        var model = viewResult?.Model as RequirementSelectionViewModel;
        await Assert.That(model).IsNotNull();

        var results = model!.Results.ToList();
        await Assert.That(results.Count).IsEqualTo(1);
        await Assert.That(results[0].Identifier).IsEqualTo("REQ-102");
    }
}
