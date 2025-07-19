using System.Text.Json.Serialization;

namespace StrictDocOslcRm.Models;

/// <summary>
/// Root model for the StrictDoc JSON format
/// </summary>
public class StrictDocData
{
    [JsonPropertyName("DOCUMENTS")]
    public List<StrictDocDocument> Documents { get; set; } = new();
}

/// <summary>
/// Represents a document in the StrictDoc format
/// </summary>
public class StrictDocDocument
{
    [JsonPropertyName("_NODE_TYPE")]
    public string NodeType { get; set; } = string.Empty;

    [JsonPropertyName("MID")]
    public string Mid { get; set; } = string.Empty;

    [JsonPropertyName("TITLE")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("NODES")]
    public List<StrictDocNode> Nodes { get; set; } = new();
}

/// <summary>
/// Base class for all StrictDoc nodes
/// </summary>
public class StrictDocNode
{
    [JsonPropertyName("_NODE_TYPE")]
    public string NodeType { get; set; } = string.Empty;

    [JsonPropertyName("MID")]
    public string? Mid { get; set; }

    [JsonPropertyName("UID")]
    public string? Uid { get; set; }

    [JsonPropertyName("TITLE")]
    public string? Title { get; set; }

    [JsonPropertyName("STATEMENT")]
    public string? Statement { get; set; }

    [JsonPropertyName("RELATIONS")]
    public List<StrictDocRelation>? Relations { get; set; }

    [JsonPropertyName("NODES")]
    public List<StrictDocNode>? Nodes { get; set; }
}

/// <summary>
/// Represents a relation in StrictDoc
/// </summary>
public class StrictDocRelation
{
    [JsonPropertyName("TYPE")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("VALUE")]
    public string? Value { get; set; }
}

/// <summary>
/// Constants for StrictDoc node types
/// </summary>
public static class StrictDocNodeTypes
{
    public const string Document = "DOCUMENT";
    public const string Requirement = "REQUIREMENT";
    public const string CompositeRequirement = "COMPOSITE_REQUIREMENT";
    public const string Section = "SECTION";
    public const string Text = "TEXT";
}

/// <summary>
/// Constants for StrictDoc relation types
/// </summary>
public static class StrictDocRelationTypes
{
    public const string Parent = "Parent";
    public const string File = "File";
}
