namespace StrictDocOslcRm.Services;

/// <summary>
/// Service for determining the base URL for generating OSLC URIs.
/// Supports both automatic detection from HTTP requests and explicit configuration for reverse proxy scenarios.
/// </summary>
public interface IBaseUrlService
{
    /// <summary>
    /// Gets the base URL for the application, either from configuration or from the current HTTP request.
    /// </summary>
    /// <returns>The base URL (e.g., "https://example.com" or "https://example.com/app")</returns>
    string GetBaseUrl();
}
