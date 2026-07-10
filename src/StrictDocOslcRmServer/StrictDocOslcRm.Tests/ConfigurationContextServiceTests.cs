using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using StrictDocOslcRm.Services;

namespace StrictDocOslcRm.Tests;

public sealed class ConfigurationContextServiceTests : IAsyncDisposable
{
    private readonly string _dataRoot = Path.Combine(Path.GetTempPath(), "strictdoc-oslc-rm-tests", Guid.NewGuid().ToString("N"));

    [Test]
    public async Task ResolveAsync_UsesQueryContextBeforeConfigurationContextHeader()
    {
        await WriteSnapshotAsync("main", "HEAD");
        await WriteSnapshotAsync("main", "v0.1.0");
        await WriteSnapshotAsync("argicultural", "HEAD");
        var service = CreateService();
        var request = new DefaultHttpContext().Request;
        request.Headers["Configuration-Context"] = "<https://strictdoc.example/oslc_config/configurations/main/HEAD>";
        request.QueryString = new QueryString(
            "?oslc_config.context=https%3A%2F%2Fstrictdoc.example%2Foslc_config%2Fconfigurations%2Fmain%2Fv0.1.0");

        var context = await service.ResolveAsync(request, "https://strictdoc.example");

        await Assert.That(context.Identifier).IsEqualTo("main/v0.1.0");
        await Assert.That(context.IsMutable).IsFalse();
    }

    [Test]
    public async Task GetAllAsync_EnumeratesOnlyPublishedSnapshots()
    {
        await WriteSnapshotAsync("main", "HEAD");
        await WriteSnapshotAsync("main", "v0.1.0");
        await WriteSnapshotAsync("argicultural", "HEAD");
        Directory.CreateDirectory(Path.Combine(_dataRoot, "ignored", "HEAD"));
        var service = CreateService();

        var contexts = await service.GetAllAsync();

        await Assert.That(contexts.Select(context => context.Identifier)).IsEquivalentTo(
            ["argicultural/HEAD", "main/HEAD", "main/v0.1.0"]);
    }

    public async ValueTask DisposeAsync()
    {
        if (Directory.Exists(_dataRoot))
        {
            await Task.Run(() => Directory.Delete(_dataRoot, recursive: true));
        }
    }

    private FileConfigurationContextService CreateService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConfigurationManagement:DataRootPath"] = _dataRoot,
                ["ConfigurationManagement:DefaultBranch"] = "main",
                ["ConfigurationManagement:DefaultTag"] = "HEAD"
            })
            .Build();
        return new FileConfigurationContextService(configuration);
    }

    private async Task WriteSnapshotAsync(string branch, string tag)
    {
        var directory = Path.Combine(_dataRoot, branch, tag);
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "strictdoc.json"), "{\"DOCUMENTS\":[]}");
        await File.WriteAllTextAsync(Path.Combine(directory, "sidecar.json"), "{}");
    }
}
