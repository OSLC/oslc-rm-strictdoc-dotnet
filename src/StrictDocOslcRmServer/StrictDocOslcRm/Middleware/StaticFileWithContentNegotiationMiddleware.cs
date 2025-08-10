using System.Net.Mime;
using Microsoft.AspNetCore.StaticFiles;

namespace StrictDocOslcRm.Middleware;

/// <summary>
/// Middleware that serves static HTML files when OSLC content types are not requested
/// </summary>
public class StaticFileWithContentNegotiationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<StaticFileWithContentNegotiationMiddleware> _logger;
    private readonly string _staticFilesPath;
    private readonly string[] _oslcContentTypes = {
        "application/rdf+xml",
        "text/turtle",
        "application/ld+json"
    };

    public StaticFileWithContentNegotiationMiddleware(
        RequestDelegate next,
        IWebHostEnvironment environment,
        ILogger<StaticFileWithContentNegotiationMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _environment = environment;
        _logger = logger;
        _staticFilesPath = configuration["StrictDoc:StaticFilesPath"] ?? throw new ArgumentNullException(nameof(configuration), "StrictDoc:StaticFilesPath configuration is required");
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Check Accept header for content negotiation
        var acceptHeader = context.Request.Headers.Accept.ToString();
        var requestsOslcContent = _oslcContentTypes.Any(ct =>
            acceptHeader.Contains(ct, StringComparison.OrdinalIgnoreCase));

        if (!requestsOslcContent)
        {
            // Try to serve static file instead
            if (await TryServeStaticFileAsync(context).ConfigureAwait(false))
            {
                return;
            }
        }

        // Continue to next middleware
        await _next(context).ConfigureAwait(false);
    }

    private async Task<bool> TryServeStaticFileAsync(HttpContext context)
    {
        try
        {
            if (!Directory.Exists(_staticFilesPath))
            {
                _logger.LogWarning("Static files directory does not exist: {Path}", _staticFilesPath);
                return false;
            }

            // Map the request path to a static file path
            var requestPath = context.Request.Path.Value?.TrimStart('/') ?? "";
            var staticFilePath = Path.Combine(_staticFilesPath, requestPath);

            // Default to index.html if path is directory or empty
            if (string.IsNullOrEmpty(requestPath) || requestPath.EndsWith('/'))
            {
                staticFilePath = Path.Combine(_staticFilesPath, "index.html");
            }
            else if (!Path.HasExtension(staticFilePath))
            {
                staticFilePath += ".html";
            }

            // Security check - ensure the file is within the static files directory
            var fullStaticPath = Path.GetFullPath(staticFilePath);
            var fullBasePath = Path.GetFullPath(_staticFilesPath);

            if (!fullStaticPath.StartsWith(fullBasePath, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Attempted path traversal: {RequestPath}", requestPath);
                return false;
            }

            if (File.Exists(fullStaticPath))
            {
                // Determine content type
                var provider = new FileExtensionContentTypeProvider();
                if (!provider.TryGetContentType(fullStaticPath, out var contentType))
                {
                    contentType = MediaTypeNames.Application.Octet;
                }

                context.Response.ContentType = contentType;

                await context.Response.SendFileAsync(fullStaticPath).ConfigureAwait(false);

                _logger.LogDebug("Served static file: {FilePath} for request: {RequestPath}",
                    fullStaticPath, context.Request.Path);

                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serving static file for request: {RequestPath}", context.Request.Path);
        }

        return false;
    }
}
