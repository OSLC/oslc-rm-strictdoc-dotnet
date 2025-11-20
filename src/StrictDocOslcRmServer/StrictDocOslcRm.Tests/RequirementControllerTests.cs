using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using StrictDocOslcRm.Controllers;
using StrictDocOslcRm.Models;
using StrictDocOslcRm.Services;
using OSLC4Net.Domains.RequirementsManagement;

namespace StrictDocOslcRm.Tests;

public class RequirementControllerTests
{
    private readonly RequirementController _controller;
    private readonly IStrictDocService _strictDocService;
    private readonly IBaseUrlService _baseUrlService;
    private readonly ILogger<RequirementController> _logger;

    public RequirementControllerTests()
    {
        _strictDocService = Substitute.For<IStrictDocService>();
        _baseUrlService = Substitute.For<IBaseUrlService>();
        _logger = Substitute.For<ILogger<RequirementController>>();

        _controller = new RequirementController(_logger, _baseUrlService, _strictDocService);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
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
        _strictDocService.GetAllRequirementsAsync(baseUrl).Returns(new List<Requirement> { requirement });

        // Act
        var result = await _controller.GetRequirementResource(uid, null, null);

        // Assert
        var okResult = result as OkObjectResult;
        await Verify(okResult?.Value);
    }
}
