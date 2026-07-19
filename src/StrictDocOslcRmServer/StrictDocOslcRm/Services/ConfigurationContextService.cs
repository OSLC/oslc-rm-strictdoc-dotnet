using System.Text.RegularExpressions;

namespace StrictDocOslcRm.Services;

public sealed record ConfigurationContext(
    string Branch,
    string Tag,
    string DataDirectory)
{
    public string Identifier => $"{Branch}/{Tag}";
    public bool IsMutable => string.Equals(Tag, "HEAD", StringComparison.Ordinal);
    public string StrictDocPath => Path.Combine(DataDirectory, "strictdoc.json");
    public string SidecarPath => Path.Combine(DataDirectory, "sidecar.json");
}

public sealed class ConfigurationContextNotFoundException(string message) : Exception(message);

public interface IConfigurationContextService
{
    Task<IReadOnlyList<ConfigurationContext>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ConfigurationContext> GetDefaultAsync(CancellationToken cancellationToken = default);
    Task<ConfigurationContext> GetAsync(string branch, string tag, CancellationToken cancellationToken = default);
    Task<ConfigurationContext> ResolveAsync(
        HttpRequest request,
        string publicBaseUrl,
        CancellationToken cancellationToken = default);
}

public sealed partial class FileConfigurationContextService(IConfiguration configuration)
    : IConfigurationContextService
{
    private const string ContextQueryParameter = "oslc_config.context";
    private const string ContextHeader = "Configuration-Context";
    private readonly string _dataRootPath = configuration["ConfigurationManagement:DataRootPath"]
        ?? throw new ArgumentNullException(nameof(configuration), "ConfigurationManagement:DataRootPath is required");
    private readonly string _defaultBranch = configuration["ConfigurationManagement:DefaultBranch"] ?? "main";
    private readonly string _defaultTag = configuration["ConfigurationManagement:DefaultTag"] ?? "HEAD";

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SafePathSegment();

    public Task<IReadOnlyList<ConfigurationContext>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(_dataRootPath))
        {
            return Task.FromResult<IReadOnlyList<ConfigurationContext>>([]);
        }

        var contexts = new List<ConfigurationContext>();
        foreach (var branchDirectory in Directory.EnumerateDirectories(_dataRootPath))
        {
            var branch = Path.GetFileName(branchDirectory);
            if (!IsSafeSegment(branch))
            {
                continue;
            }

            foreach (var tagDirectory in Directory.EnumerateDirectories(branchDirectory))
            {
                var tag = Path.GetFileName(tagDirectory);
                if (!IsSafeSegment(tag) || !File.Exists(Path.Combine(tagDirectory, "strictdoc.json")))
                {
                    continue;
                }

                contexts.Add(new ConfigurationContext(branch, tag, tagDirectory));
            }
        }

        IReadOnlyList<ConfigurationContext> ordered = contexts
            .OrderBy(context => context.Branch, StringComparer.Ordinal)
            .ThenBy(context => context.Tag.Equals("HEAD", StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(context => context.Tag, StringComparer.Ordinal)
            .ToList();
        return Task.FromResult(ordered);
    }

    public async Task<ConfigurationContext> GetDefaultAsync(CancellationToken cancellationToken = default) =>
        await GetAsync(_defaultBranch, _defaultTag, cancellationToken).ConfigureAwait(false);

    public async Task<ConfigurationContext> GetAsync(
        string branch,
        string tag,
        CancellationToken cancellationToken = default)
    {
        if (!IsSafeSegment(branch) || !IsSafeSegment(tag))
        {
            throw new ConfigurationContextNotFoundException("Configuration branch and tag must be simple path segments.");
        }

        var context = (await GetAllAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault(candidate =>
            string.Equals(candidate.Branch, branch, StringComparison.Ordinal) &&
            string.Equals(candidate.Tag, tag, StringComparison.Ordinal));
        return context ?? throw new ConfigurationContextNotFoundException(
            $"StrictDoc configuration '{branch}/{tag}' was not found.");
    }

    public async Task<ConfigurationContext> ResolveAsync(
        HttpRequest request,
        string publicBaseUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rawContext = request.Query.TryGetValue(ContextQueryParameter, out var queryValue) &&
            !string.IsNullOrWhiteSpace(queryValue.ToString())
            ? queryValue.ToString()
            : request.Headers[ContextHeader].ToString();

        if (string.IsNullOrWhiteSpace(rawContext))
        {
            return await GetDefaultAsync(cancellationToken).ConfigureAwait(false);
        }

        var contextUriText = rawContext.Trim().Trim('<', '>');
        if (!Uri.TryCreate(contextUriText, UriKind.Absolute, out var contextUri))
        {
            throw new ConfigurationContextNotFoundException("Configuration context must be an absolute local configuration URI.");
        }

        var prefix = publicBaseUrl.TrimEnd('/') + "/oslc_config/configurations/";
        var contextPath = contextUri.GetLeftPart(UriPartial.Path);
        if (!contextPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConfigurationContextNotFoundException(
                "The selected configuration context is not owned by this StrictDoc provider.");
        }

        var segments = contextPath[prefix.Length..]
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Uri.UnescapeDataString)
            .ToArray();
        if (segments.Length != 2)
        {
            throw new ConfigurationContextNotFoundException("Configuration context must identify exactly one branch and tag.");
        }

        return await GetAsync(segments[0], segments[1], cancellationToken).ConfigureAwait(false);
    }

    private static bool IsSafeSegment(string value) => SafePathSegment().IsMatch(value);
}
