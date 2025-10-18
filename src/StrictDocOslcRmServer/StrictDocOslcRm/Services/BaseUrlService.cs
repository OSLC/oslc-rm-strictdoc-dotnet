namespace StrictDocOslcRm.Services;

/// <summary>
/// Implementation of IBaseUrlService that provides base URL resolution.
/// When PublicBaseUri is configured, it uses that value; otherwise, it derives the URL from the HTTP request.
/// This is essential for reverse proxy scenarios where the external URL differs from the internal one.
/// </summary>
public class BaseUrlService : IBaseUrlService
{
    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<BaseUrlService> _logger;

    public BaseUrlService(
        IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor,
        ILogger<BaseUrlService> logger)
    {
        _configuration = configuration;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <summary>
    /// Gets the base URL for the application.
    /// Priority: 1) OSLC:PublicBaseUri from configuration, 2) Request-derived URL
    /// </summary>
    public string GetBaseUrl()
    {
        // First, check if a public base URI is configured (for reverse proxy scenarios)
        var configuredBaseUri = _configuration["OSLC:PublicBaseUri"];
        
        if (!string.IsNullOrWhiteSpace(configuredBaseUri))
        {
            // Remove trailing slash for consistency
            var baseUri = configuredBaseUri.TrimEnd('/');
            _logger.LogDebug("Using configured PublicBaseUri: {BaseUri}", baseUri);
            return baseUri;
        }

        // Fall back to deriving from the HTTP request
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request == null)
        {
            _logger.LogWarning("No HTTP context available and no PublicBaseUri configured");
            return string.Empty;
        }

        var baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}";
        _logger.LogDebug("Using request-derived base URL: {BaseUrl}", baseUrl);
        return baseUrl;
    }
}
