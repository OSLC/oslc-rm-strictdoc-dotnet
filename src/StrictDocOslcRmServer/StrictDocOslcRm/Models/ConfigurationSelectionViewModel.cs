namespace StrictDocOslcRm.Models;

public sealed class ConfigurationSelectionViewModel
{
    public string SelectorUri { get; init; } = string.Empty;
    public string? Terms { get; init; }
    public IReadOnlyList<GenericConfiguration> Results { get; init; } = [];
}
