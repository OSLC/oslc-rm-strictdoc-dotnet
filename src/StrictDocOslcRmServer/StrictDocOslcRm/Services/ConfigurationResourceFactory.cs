using StrictDocOslcRm.Models;

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
}
