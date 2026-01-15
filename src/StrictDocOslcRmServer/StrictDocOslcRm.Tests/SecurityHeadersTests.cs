using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Playwright;

namespace StrictDocOslcRm.Tests;

[ClassDataSource<WebApplicationFactory<Program>>(Shared = SharedType.PerAssembly)]
public class SecurityHeadersTests(WebApplicationFactory<Program> factory)
{
    private readonly WebApplicationFactory<Program> _factory = factory;

    [Test]
    public async Task SecurityHeaders_ArePresent_OnEmbeddableResource()
    {
        // Use HttpClient for checking headers directly
        var client = _factory.CreateClient();
        // Since we don't have real data, root might 404 but middleware still runs.
        // Wait, root "/" maps to RequirementController.GetRequirementResource but requires query param 'a'.
        // Without 'a', it returns 400 Bad Request. Middleware runs on 400 too.
        var response = await client.GetAsync("/?a=123");

        var headers = response.Headers;

        // Assertions
        await Assert.That(headers.Contains("X-Content-Type-Options")).IsTrue();
        await Assert.That(headers.GetValues("X-Content-Type-Options").FirstOrDefault()).IsEqualTo("nosniff");

        await Assert.That(headers.Contains("Referrer-Policy")).IsTrue();
        await Assert.That(headers.GetValues("Referrer-Policy").FirstOrDefault()).IsEqualTo("strict-origin-when-cross-origin");

        await Assert.That(headers.Contains("Permissions-Policy")).IsTrue();

        // X-Frame-Options should be absent for embeddable
        await Assert.That(headers.Contains("X-Frame-Options")).IsFalse();

        // CSP should allow framing (frame-ancestors *)
        await Assert.That(headers.Contains("Content-Security-Policy")).IsTrue();
        var csp = headers.GetValues("Content-Security-Policy").FirstOrDefault();
        await Assert.That(csp).IsNotNull();
        await Assert.That(csp).Contains("frame-ancestors *");
    }

    [Test]
    public async Task HstsHeader_IsPresent_InProduction()
    {
        var client = _factory
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
            })
            .CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        var response = await client.GetAsync("/?a=123");
        var headers = response.Headers;

        // Verify Strict-Transport-Security
        await Assert.That(headers.Contains("Strict-Transport-Security")).IsTrue();
        var hsts = headers.GetValues("Strict-Transport-Security").FirstOrDefault();
        await Assert.That(hsts).IsNotNull();
        await Assert.That(hsts).Contains("max-age=");
    }

    [Test]
    public async Task SecurityHeaders_ArePresent_OnSelector()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/oslc/service_provider/123/requirements/selector");

        var headers = response.Headers;
        // Even if 404 (document not found), the middleware checks PATH.
        // However, if the controller returns 404, the response is generated.
        // The middleware runs AFTER the controller (on the way back)? No, it wraps 'next'.
        // So it adds headers to the response object.
        // The middleware logic:
        // await next(context);
        // headers["..."] = ...
        // So it adds headers AFTER the inner pipeline.

        await Assert.That(headers.Contains("X-Frame-Options")).IsFalse();

        var csp = headers.GetValues("Content-Security-Policy").FirstOrDefault();
        await Assert.That(csp).IsNotNull();
        await Assert.That(csp).Contains("frame-ancestors *");
    }

    [Test]
    public async Task SecurityHeaders_ArePresent_OnNonEmbeddableResource()
    {
        var client = _factory.CreateClient();
        // A route that is not whitelisted
        var response = await client.GetAsync("/oslc/catalog/123");

        var headers = response.Headers;

        await Assert.That(headers.Contains("X-Frame-Options")).IsTrue();
        await Assert.That(headers.GetValues("X-Frame-Options").FirstOrDefault()).IsEqualTo("DENY");

        await Assert.That(headers.Contains("Content-Security-Policy")).IsTrue();
        var csp = headers.GetValues("Content-Security-Policy").FirstOrDefault();
        await Assert.That(csp).IsNotNull();
        await Assert.That(csp).Contains("frame-ancestors 'none'");
    }
}
