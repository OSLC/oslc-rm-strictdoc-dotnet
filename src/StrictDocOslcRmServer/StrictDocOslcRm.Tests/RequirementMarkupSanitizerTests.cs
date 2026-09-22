using StrictDocOslcRm.Services;

namespace StrictDocOslcRm.Tests;

public class RequirementMarkupSanitizerTests
{
    private readonly RequirementMarkupSanitizer _sanitizer = new();

    [Test]
    public async Task SanitizeTitle_PreservesInlineFormattingAndRemovesActiveContent()
    {
        const string markup = "<span>Use <em>formatted</em> text</span>" +
                              "<script>alert('xss')</script><img src=x onerror=alert(1)>";

        var sanitized = _sanitizer.SanitizeTitle(markup).ToString();

        await Assert.That(sanitized).Contains("<em>formatted</em>");
        await Assert.That(sanitized).DoesNotContain("<script");
        await Assert.That(sanitized).DoesNotContain("<img");
        await Assert.That(sanitized).DoesNotContain("onerror");
    }

    [Test]
    public async Task SanitizeDescription_PreservesSafeXhtmlAndStripsUnsafeLinksAndAttributes()
    {
        const string markup = "<div><p>See <strong>details</strong> at " +
                              "<a href=\"https://example.test/spec\" onclick=\"alert(1)\">spec</a>. " +
                              "<a href=\"javascript:alert(2)\">unsafe link</a></p>" +
                              "<svg onload=\"alert(3)\"></svg></div>";

        var sanitized = _sanitizer.SanitizeDescription(markup).ToString();

        await Assert.That(sanitized).Contains("<div>");
        await Assert.That(sanitized).Contains("<strong>details</strong>");
        await Assert.That(sanitized).Contains("href=\"https://example.test/spec\"");
        await Assert.That(sanitized).DoesNotContain("javascript:");
        await Assert.That(sanitized).DoesNotContain("onclick");
        await Assert.That(sanitized).DoesNotContain("onload");
        await Assert.That(sanitized).DoesNotContain("<svg");
    }

    [Test]
    public async Task SanitizeDescription_HandlesMathMlHtmlIntegrationPointPayload()
    {
        const string markup = "<math><annotation-xml encoding=\"text/html\">" +
                              "<title><a encoding=\"</title><img src=x onerror=alert()>\">" +
                              "</annotation-xml></math>";

        var sanitized = _sanitizer.SanitizeDescription(markup).ToString();

        await Assert.That(sanitized).DoesNotContain("<img");
        await Assert.That(sanitized).DoesNotContain("onerror");
    }
}
