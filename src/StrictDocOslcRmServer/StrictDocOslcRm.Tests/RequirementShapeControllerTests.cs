using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OSLC4Net.Core.Model;
using StrictDocOslcRm.Controllers;
using StrictDocOslcRm.Services;

namespace StrictDocOslcRm.Tests;

public sealed class RequirementShapeControllerTests
{
    [Test]
    public async Task Get_ReturnsProviderOwnedRequirementShape()
    {
        var baseUrlService = Substitute.For<IBaseUrlService>();
        baseUrlService.GetBaseUrl().Returns("https://strictdoc.example.test");
        var controller = new RequirementShapeController(baseUrlService);

        var result = controller.Get() as OkObjectResult;

        await Assert.That(result?.Value).IsTypeOf<ResourceShape>();
        var shape = (ResourceShape)result!.Value!;
        await Assert.That(shape.GetAbout()).IsEqualTo(
            new Uri("https://strictdoc.example.test/oslc/shapes/requirement"));
    }
}
