using OSLC4Net.Core.Model;
using OSLC4Net.Domains.ConfigurationManagement;
using StrictDocOslcRm.Models;
using ConfigurationBaseline = OSLC4Net.Domains.ConfigurationManagement.Baseline;
using ConfigurationStream = OSLC4Net.Domains.ConfigurationManagement.Stream;

namespace StrictDocOslcRm.Services;

public static class ConfigurationResourceFactory
{
    public const string ComponentId = "strictdoc";

    public static GenericConfiguration CreateConfiguration(
        ConfigurationContext context,
        string baseUrl)
    {
        var providerUri = new Uri($"{baseUrl}/oslc_config/service_provider");
        var componentUri = new Uri($"{baseUrl}/oslc_config/components/{ComponentId}");
        var configurationUri = new Uri(ConfigurationUri(baseUrl, context));
        return new GenericConfiguration(configurationUri)
        {
            Identifier = context.Identifier,
            ShortId = context.Identifier,
            Title = context.IsMutable
                ? $"StrictDoc {context.Branch} stream"
                : $"StrictDoc {context.Branch} baseline {context.Tag}",
            Component = componentUri,
            ServiceProvider = providerUri,
            AcceptedBy = [new Uri(ConfigurationVocabulary.Configuration)]
        };
    }

    /// <summary>
    /// Creates the concrete local-configuration representation Jazz uses to distinguish a
    /// mutable stream from an immutable baseline. The generic Configuration type is retained
    /// as an explicit secondary RDF type because both subtypes are Configurations.
    /// </summary>
    public static IResource CreateTypedConfiguration(
        ConfigurationContext context,
        string baseUrl,
        Uri? previousBaseline = null) =>
        context.IsMutable
            ? CreateStream(context, baseUrl, previousBaseline)
            : CreateBaseline(context, baseUrl);

    public static GenericConfigurationContainer CreateBaselinesContainer(
        string branch,
        IEnumerable<ConfigurationContext> baselines,
        string baseUrl) => new(
        new Uri(BaselinesContainerUri(baseUrl, branch)))
    {
        Members = baselines
            .Select(context => new Uri(ConfigurationUri(baseUrl, context)))
            .ToList()
    };

    public static GenericComponent CreateComponent(string baseUrl) => new(
        new Uri($"{baseUrl}/oslc_config/components/{ComponentId}"))
    {
        Identifier = ComponentId,
        Title = "StrictDoc requirements component",
        Configurations = new Uri($"{baseUrl}/oslc_config/configurations"),
        ServiceProvider = new Uri($"{baseUrl}/oslc_config/service_provider")
    };

    public static string ConfigurationUri(string baseUrl, ConfigurationContext context) =>
        $"{baseUrl}/oslc_config/configurations/{Uri.EscapeDataString(context.Branch)}/{Uri.EscapeDataString(context.Tag)}";

    public static string BaselinesContainerUri(string baseUrl, string branch) =>
        $"{baseUrl}/oslc_config/configurations/{Uri.EscapeDataString(branch)}/HEAD/baselines";

    private static ConfigurationStream CreateStream(
        ConfigurationContext context,
        string baseUrl,
        Uri? previousBaseline)
    {
        var providerUri = new Uri($"{baseUrl}/oslc_config/service_provider");
        var stream = new ConfigurationStream(new Uri(ConfigurationUri(baseUrl, context)))
        {
            Identifier = context.Identifier,
            ShortId = context.Identifier,
            Title = $"StrictDoc {context.Branch} stream",
            Component = new Uri($"{baseUrl}/oslc_config/components/{ComponentId}"),
            Baselines = new Uri(BaselinesContainerUri(baseUrl, context.Branch)),
            ServiceProvider = [providerUri],
            AcceptedBy = [new Uri(ConfigurationVocabulary.Configuration)],
            Types = [new Uri(ConfigurationVocabulary.Configuration)]
        };

        if (previousBaseline is not null)
        {
            stream.PreviousBaseline.Add(previousBaseline);
        }

        return stream;
    }

    private static ConfigurationBaseline CreateBaseline(ConfigurationContext context, string baseUrl)
    {
        var providerUri = new Uri($"{baseUrl}/oslc_config/service_provider");
        return new ConfigurationBaseline(new Uri(ConfigurationUri(baseUrl, context)))
        {
            Identifier = context.Identifier,
            ShortId = context.Identifier,
            Title = $"StrictDoc {context.Branch} baseline {context.Tag}",
            Component = new Uri($"{baseUrl}/oslc_config/components/{ComponentId}"),
            BaselineOfStream = new Uri($"{baseUrl}/oslc_config/configurations/{Uri.EscapeDataString(context.Branch)}/HEAD"),
            ServiceProvider = [providerUri],
            AcceptedBy = [new Uri(ConfigurationVocabulary.Configuration)],
            Types = [new Uri(ConfigurationVocabulary.Configuration)]
        };
    }
}
