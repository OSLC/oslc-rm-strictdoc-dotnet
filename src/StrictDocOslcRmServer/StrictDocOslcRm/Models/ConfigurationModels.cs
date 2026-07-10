using OSLC4Net.Core.Attribute;
using OSLC4Net.Core.Model;
using ValueType = OSLC4Net.Core.Model.ValueType;

namespace StrictDocOslcRm.Models;

public static class ConfigurationVocabulary
{
    public const string Namespace = "http://open-services.net/ns/config#";
    public const string Configuration = Namespace + "Configuration";
    public const string Component = Namespace + "Component";
    public const string ComponentProperty = Namespace + "component";
    public const string ConfigurationsProperty = Namespace + "configurations";
    public const string AcceptedByProperty = Namespace + "acceptedBy";
}

public static class LdpVocabulary
{
    public const string Namespace = "http://www.w3.org/ns/ldp#";
    public const string Container = Namespace + "Container";
    public const string Member = "http://www.w3.org/2000/01/rdf-schema#member";
}

[OslcNamespace(ConfigurationVocabulary.Namespace)]
[OslcName("Configuration")]
[OslcResourceShape(
    title = "StrictDoc generic configuration resource",
    describes = new[] { ConfigurationVocabulary.Configuration })]
public sealed record GenericConfiguration : AbstractResourceRecord
{
    public GenericConfiguration(Uri about)
        : base(about) { }

    public GenericConfiguration() { }

    [OslcPropertyDefinition("http://purl.org/dc/terms/title")]
    [OslcName("title")]
    [OslcTitle("Title")]
    [OslcValueType(ValueType.XMLLiteral)]
    public string? Title { get; set; }

    [OslcPropertyDefinition("http://purl.org/dc/terms/identifier")]
    [OslcName("identifier")]
    [OslcTitle("Identifier")]
    [OslcValueType(ValueType.String)]
    public string? Identifier { get; set; }

    [OslcPropertyDefinition("http://open-services.net/ns/core#shortId")]
    [OslcName("shortId")]
    [OslcTitle("Short identifier")]
    [OslcValueType(ValueType.String)]
    public string? ShortId { get; set; }

    [OslcPropertyDefinition(ConfigurationVocabulary.ComponentProperty)]
    [OslcName("component")]
    [OslcTitle("Component")]
    [OslcValueType(ValueType.Resource)]
    [OslcRepresentation(Representation.Reference)]
    public Uri Component { get; set; } = null!;

    [OslcPropertyDefinition(ConfigurationVocabulary.AcceptedByProperty)]
    [OslcName("acceptedBy")]
    [OslcTitle("Accepted by")]
    [OslcValueType(ValueType.Resource)]
    [OslcRepresentation(Representation.Reference)]
    public List<Uri> AcceptedBy { get; set; } = [];

    [OslcPropertyDefinition("http://open-services.net/ns/core#serviceProvider")]
    [OslcName("serviceProvider")]
    [OslcTitle("Service provider")]
    [OslcValueType(ValueType.Resource)]
    [OslcRepresentation(Representation.Reference)]
    public Uri ServiceProvider { get; set; } = null!;
}

[OslcNamespace(ConfigurationVocabulary.Namespace)]
[OslcName("Component")]
[OslcResourceShape(
    title = "StrictDoc configuration component",
    describes = new[] { ConfigurationVocabulary.Component })]
public sealed record GenericComponent : AbstractResourceRecord
{
    public GenericComponent(Uri about)
        : base(about) { }

    public GenericComponent() { }

    [OslcPropertyDefinition("http://purl.org/dc/terms/title")]
    [OslcName("title")]
    [OslcTitle("Title")]
    [OslcValueType(ValueType.XMLLiteral)]
    public string? Title { get; set; }

    [OslcPropertyDefinition("http://purl.org/dc/terms/identifier")]
    [OslcName("identifier")]
    [OslcTitle("Identifier")]
    [OslcValueType(ValueType.String)]
    public string? Identifier { get; set; }

    [OslcPropertyDefinition(ConfigurationVocabulary.ConfigurationsProperty)]
    [OslcName("configurations")]
    [OslcTitle("Configurations")]
    [OslcValueType(ValueType.Resource)]
    [OslcRepresentation(Representation.Reference)]
    public Uri Configurations { get; set; } = null!;

    [OslcPropertyDefinition("http://open-services.net/ns/core#serviceProvider")]
    [OslcName("serviceProvider")]
    [OslcTitle("Service provider")]
    [OslcValueType(ValueType.Resource)]
    [OslcRepresentation(Representation.Reference)]
    public Uri ServiceProvider { get; set; } = null!;
}

[OslcNamespace(LdpVocabulary.Namespace)]
[OslcName("Container")]
[OslcResourceShape(title = "StrictDoc configuration container", describes = new[] { LdpVocabulary.Container })]
public sealed record GenericConfigurationContainer : AbstractResourceRecord
{
    public GenericConfigurationContainer(Uri about)
        : base(about) { }

    public GenericConfigurationContainer() { }

    [OslcPropertyDefinition(LdpVocabulary.Member)]
    [OslcName("member")]
    [OslcTitle("Member")]
    [OslcValueType(ValueType.Resource)]
    [OslcRepresentation(Representation.Reference)]
    public List<Uri> Members { get; set; } = [];
}
