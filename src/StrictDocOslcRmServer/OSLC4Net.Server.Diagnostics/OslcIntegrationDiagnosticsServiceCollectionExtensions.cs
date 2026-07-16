using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OSLC4Net.Server.Diagnostics;

public static class OslcIntegrationDiagnosticsServiceCollectionExtensions
{
    /// <summary>
    /// Registers options for OSLC integration request/response diagnostics.
    /// </summary>
    public static IServiceCollection AddOslcIntegrationDiagnostics(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<OslcIntegrationDiagnosticsOptions>(
            configuration.GetSection(OslcIntegrationDiagnosticsOptions.SectionName));
        return services;
    }

    /// <summary>
    /// Logs every request and response at Trace level, and conditionally writes paired raw
    /// request/response files when configured by <see cref="OslcIntegrationDiagnosticsOptions"/>.
    /// </summary>
    public static IApplicationBuilder UseOslcIntegrationDiagnostics(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<OslcIntegrationDiagnosticsMiddleware>();
    }
}
