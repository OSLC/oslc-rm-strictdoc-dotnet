using Microsoft.AspNetCore.Http;

namespace StrictDocOslcRm.Services;

/// <summary>
/// Resolves the externally visible OSLC base URI and applies it to a request before
/// request-derived OSLC serialisation occurs.
/// </summary>
public static class PublicBaseUri
{
    public static bool TryGetConfigured(IConfiguration configuration, out Uri publicBaseUri)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var configuredValue = configuration["OSLC:PublicBaseUri"]
                              ?? configuration["OSLC:PublicBaseUrl"];
        if (Uri.TryCreate(configuredValue, UriKind.Absolute, out var candidate) &&
            (candidate.Scheme == Uri.UriSchemeHttp || candidate.Scheme == Uri.UriSchemeHttps) &&
            string.IsNullOrEmpty(candidate.Query) &&
            string.IsNullOrEmpty(candidate.Fragment) &&
            string.IsNullOrEmpty(candidate.UserInfo))
        {
            publicBaseUri = candidate;
            return true;
        }

        publicBaseUri = null!;
        return false;
    }

    public static void ApplyTo(HttpRequest request, Uri publicBaseUri)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(publicBaseUri);

        request.Scheme = publicBaseUri.Scheme;
        request.Host = new HostString(publicBaseUri.Authority);
        request.PathBase = publicBaseUri.AbsolutePath == "/"
            ? PathString.Empty
            : new PathString(publicBaseUri.AbsolutePath.TrimEnd('/'));
    }
}
