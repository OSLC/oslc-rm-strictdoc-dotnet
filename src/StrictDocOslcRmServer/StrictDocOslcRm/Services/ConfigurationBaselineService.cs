using System.Text.RegularExpressions;

namespace StrictDocOslcRm.Services;

public sealed record BaselineCreationResult(
    ConfigurationContext Stream,
    ConfigurationContext Baseline);

public sealed class BaselineAlreadyExistsException(string message) : Exception(message);

/// <summary>
/// Publishes an immutable baseline from the current state of a StrictDoc stream.
/// </summary>
public interface IConfigurationBaselineService
{
    Task<BaselineCreationResult> CreateAsync(
        string branch,
        string tag,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Standalone publication implementation. It snapshots the local HEAD JSON and sidecar files.
/// </summary>
public sealed partial class ConfigurationBaselineStandalone(
    IConfiguration configuration,
    IConfigurationContextService configurationContextService) : IConfigurationBaselineService
{
    private readonly string _dataRootPath = configuration["ConfigurationManagement:DataRootPath"]
        ?? throw new ArgumentNullException(nameof(configuration), "ConfigurationManagement:DataRootPath is required");

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SafePathSegment();

    public async Task<BaselineCreationResult> CreateAsync(
        string branch,
        string tag,
        CancellationToken cancellationToken = default)
    {
        if (!IsSafeTag(tag) || string.Equals(tag, "HEAD", StringComparison.Ordinal))
        {
            throw new ArgumentException("A baseline tag must be a safe path segment other than HEAD.", nameof(tag));
        }

        var stream = await configurationContextService.GetAsync(branch, "HEAD", cancellationToken)
            .ConfigureAwait(false);
        var branchDirectory = Path.Combine(_dataRootPath, branch);
        var destinationDirectory = Path.Combine(branchDirectory, tag);
        if (Directory.Exists(destinationDirectory))
        {
            throw new BaselineAlreadyExistsException(
                $"StrictDoc baseline '{branch}/{tag}' already exists and cannot be overwritten.");
        }

        var stagingDirectory = Path.Combine(branchDirectory, $".{tag}.staging-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(stagingDirectory);
            await CopyFileAsync(stream.StrictDocPath, Path.Combine(stagingDirectory, "strictdoc.json"), cancellationToken)
                .ConfigureAwait(false);

            if (File.Exists(stream.SidecarPath))
            {
                await CopyFileAsync(stream.SidecarPath, Path.Combine(stagingDirectory, "sidecar.json"), cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await File.WriteAllTextAsync(Path.Combine(stagingDirectory, "sidecar.json"), "{}\n", cancellationToken)
                    .ConfigureAwait(false);
            }

            try
            {
                Directory.Move(stagingDirectory, destinationDirectory);
            }
            catch (IOException) when (Directory.Exists(destinationDirectory))
            {
                throw new BaselineAlreadyExistsException(
                    $"StrictDoc baseline '{branch}/{tag}' already exists and cannot be overwritten.");
            }

            var baseline = await configurationContextService.GetAsync(branch, tag, cancellationToken)
                .ConfigureAwait(false);
            return new BaselineCreationResult(stream, baseline);
        }
        finally
        {
            if (Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, recursive: true);
            }
        }
    }

    private static bool IsSafeTag(string value) => !string.IsNullOrWhiteSpace(value) && SafePathSegment().IsMatch(value);

    private static async Task CopyFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        await using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var destination = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
    }
}
