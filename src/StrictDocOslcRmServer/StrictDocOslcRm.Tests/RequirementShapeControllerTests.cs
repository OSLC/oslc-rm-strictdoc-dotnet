using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OSLC4Net.Core.Model;
using OSLC4Net.Domains.RequirementsManagement;
using StrictDocOslcRm.Controllers;
using StrictDocOslcRm.Services;
using ValueType = OSLC4Net.Core.Model.ValueType;

namespace StrictDocOslcRm.Tests;

public class RequirementShapeControllerTests
{
    [Test]
    public async Task Get_ReturnsWritableAffectedByShape()
    {
        var baseUrlService = Substitute.For<IBaseUrlService>();
        baseUrlService.GetBaseUrl().Returns("http://localhost:8080");
        var controller = new RequirementShapeController(baseUrlService);

        var result = controller.Get();

        var okResult = result as OkObjectResult;
        var shape = okResult?.Value as ResourceShape;
        var affectedBy = shape?.GetProperties()
            .FirstOrDefault(property => property.GetName() == "affectedBy");

        await Assert.That(shape).IsNotNull();
        await Assert.That(shape!.GetAbout()).IsEqualTo(new Uri("http://localhost:8080/oslc/shapes/requirement"));
        await Assert.That(shape.GetDescribes()).Contains(new Uri(Constants.Domains.RM.Requirement));
        await Assert.That(affectedBy).IsNotNull();
        await Assert.That(affectedBy!.GetPropertyDefinition()).IsEqualTo(new Uri(Constants.Domains.RM.P.AffectedBy));
        await Assert.That(affectedBy.GetOccurs()).IsEqualTo(new Uri(OccursExtension.ToString(Occurs.ZeroOrMany)));
        await Assert.That(affectedBy.GetRepresentation()).IsEqualTo(new Uri(RepresentationExtension.ToString(Representation.Reference)));
        await Assert.That(affectedBy.GetValueType()).IsEqualTo(new Uri(ValueTypeExtension.ToString(ValueType.Resource)));
        await Assert.That(affectedBy.IsReadOnly()).IsFalse();
    }
}
