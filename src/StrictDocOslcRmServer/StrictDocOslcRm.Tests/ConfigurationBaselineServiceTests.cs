using Microsoft.Extensions.Configuration;
using StrictDocOslcRm.Services;

namespace StrictDocOslcRm.Tests;

public sealed class ConfigurationBaselineServiceTests : IAsyncDisposable
{
    private readonly string _dataRoot = Path.Combine(Path.GetTempPath(), "strictdoc-oslc-rm-tests", Guid.NewGuid().ToString("N"));

    [Test]
    public async Task CreateAsync_CopiesHeadSnapshotAndKeepsBaselineIndependent()
    {
        await WriteStreamSnapshotAsync("main", "original strictdoc", "original sidecar");
        var service = CreateService();

        var result = await service.CreateAsync("main", "v0.2.0");

        await Assert.That(result.Stream.Identifier).IsEqualTo("main/HEAD");
        await Assert.That(result.Baseline.Identifier).IsEqualTo("main/v0.2.0");
        await Assert.That(result.Baseline.IsMutable).IsFalse();
        await Assert.That(await File.ReadAllTextAsync(result.Baseline.StrictDocPath)).IsEqualTo("original strictdoc");
        await Assert.That(await File.ReadAllTextAsync(result.Baseline.SidecarPath)).IsEqualTo("original sidecar");

        await File.WriteAllTextAsync(result.Stream.StrictDocPath, "changed strictdoc");
        await File.WriteAllTextAsync(result.Stream.SidecarPath, "changed sidecar");

        await Assert.That(await File.ReadAllTextAsync(result.Baseline.StrictDocPath)).IsEqualTo("original strictdoc");
        await Assert.That(await File.ReadAllTextAsync(result.Baseline.SidecarPath)).IsEqualTo("original sidecar");
    }

    [Test]
    public async Task CreateAsync_RefusesToOverwriteAnExistingBaseline()
    {
        await WriteStreamSnapshotAsync("main", "strictdoc", "sidecar");
        await WriteBaselineSnapshotAsync("main", "v0.2.0", "published strictdoc", "published sidecar");
        var service = CreateService();

        var exception = await Assert.ThrowsAsync<BaselineAlreadyExistsException>(() => service.CreateAsync("main", "v0.2.0"));

        await Assert.That(exception!.Message).Contains("cannot be overwritten");
        await Assert.That(await File.ReadAllTextAsync(Path.Combine(_dataRoot, "main", "v0.2.0", "strictdoc.json")))
            .IsEqualTo("published strictdoc");
    }

    [Test]
    public async Task CreateAsync_RefusesHeadAsABaselineTag()
    {
        await WriteStreamSnapshotAsync("main", "strictdoc", "sidecar");
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync("main", "HEAD"));
    }

    public async ValueTask DisposeAsync()
    {
        if (Directory.Exists(_dataRoot))
        {
            await Task.Run(() => Directory.Delete(_dataRoot, recursive: true));
        }
    }

    private ConfigurationBaselineStandalone CreateService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConfigurationManagement:DataRootPath"] = _dataRoot,
                ["ConfigurationManagement:DefaultBranch"] = "main",
                ["ConfigurationManagement:DefaultTag"] = "HEAD"
            })
            .Build();
        return new ConfigurationBaselineStandalone(configuration, new FileConfigurationContextService(configuration));
    }

    private async Task WriteStreamSnapshotAsync(string branch, string strictDoc, string sidecar)
    {
        var directory = Path.Combine(_dataRoot, branch, "HEAD");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "strictdoc.json"), strictDoc);
        await File.WriteAllTextAsync(Path.Combine(directory, "sidecar.json"), sidecar);
    }

    private async Task WriteBaselineSnapshotAsync(string branch, string tag, string strictDoc, string sidecar)
    {
        var directory = Path.Combine(_dataRoot, branch, tag);
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "strictdoc.json"), strictDoc);
        await File.WriteAllTextAsync(Path.Combine(directory, "sidecar.json"), sidecar);
    }
}
