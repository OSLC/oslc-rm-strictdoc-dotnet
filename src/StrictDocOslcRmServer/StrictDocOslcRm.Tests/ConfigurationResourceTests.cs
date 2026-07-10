using StrictDocOslcRm.Services;

namespace StrictDocOslcRm.Tests;

public sealed class ConfigurationResourceTests
{
    [Test]
    public async Task GenericConfiguration_SerializesAsConfigurationWithMatchingContributionType()
    {
        var resource = ConfigurationResourceFactory.CreateConfiguration(
            new ConfigurationContext("main", "HEAD", "/data/main/HEAD"),
            "https://strictdoc.example");

        await Verify(resource);
    }
}
