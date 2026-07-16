namespace OSLC4Net.Server.Diagnostics;

/// <summary>
/// Controls capture of HTTP exchanges that are useful when diagnosing an OSLC integration.
/// </summary>
public sealed class OslcIntegrationDiagnosticsOptions
{
    public const string SectionName = "OslcIntegrationDiagnostics";

    /// <summary>
    /// Writes request and response payload files for 4xx and 5xx responses.
    /// </summary>
    public bool CapturePayloads { get; set; }

    /// <summary>
    /// Extends payload capture to 2xx responses. Redirects are always excluded.
    /// </summary>
    public bool CaptureSuccessfulResponses { get; set; }

    /// <summary>
    /// Directory where paired <c>_req.log</c> and <c>_resp.log</c> files are written.
    /// </summary>
    public string CaptureDirectory { get; set; } = "oslc-integration-diagnostics";

    /// <summary>
    /// Includes values such as Authorization and Cookie in captured header files.
    /// This is disabled by default because the files are intended for local troubleshooting.
    /// </summary>
    public bool IncludeSensitiveHeaders { get; set; }
}
