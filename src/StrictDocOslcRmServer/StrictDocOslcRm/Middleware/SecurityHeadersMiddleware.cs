using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace StrictDocOslcRm.Middleware;

public class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // X-Content-Type-Options
        headers["X-Content-Type-Options"] = "nosniff";

        // Referrer-Policy
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Permissions-Policy
        headers["Permissions-Policy"] = "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";

        // Determine if the endpoint should be embeddable
        var path = context.Request.Path;
        var isEmbeddable = IsEmbeddable(path);

        if (isEmbeddable)
        {
            // Remove X-Frame-Options if present
            headers.Remove("X-Frame-Options");
        }
        else
        {
            headers["X-Frame-Options"] = "DENY";
        }

        // Content-Security-Policy
        var csp = BuildCsp(isEmbeddable);
        headers["Content-Security-Policy"] = csp;

        await next(context);
    }

    private static bool IsEmbeddable(PathString path)
    {
        if (!path.HasValue) return false;

        var pathValue = path.Value!;

        // RequirementController.GetRequirementResource -> /
        if (pathValue == "/") return true;

        // ServiceProviderController.RequirementSelector -> /oslc/service_provider/{documentMid}/requirements/selector
        if (pathValue.StartsWith("/oslc/service_provider", StringComparison.OrdinalIgnoreCase) &&
            pathValue.EndsWith("/requirements/selector", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static string BuildCsp(bool isEmbeddable)
    {
        var frameAncestors = isEmbeddable ? "*" : "'none'";

        return "default-src 'self'; " +
               "script-src 'self' 'unsafe-inline' https://unpkg.com; " +
               "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
               "font-src 'self' https://cdn.jsdelivr.net; " +
               "img-src 'self' data:; " +
               $"frame-ancestors {frameAncestors}; " +
               "object-src 'none'; " +
               "base-uri 'self'; " +
               "form-action 'self'; " +
               "upgrade-insecure-requests;";
    }
}
