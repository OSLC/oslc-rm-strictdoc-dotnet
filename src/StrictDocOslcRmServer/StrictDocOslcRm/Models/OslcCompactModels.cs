using System.Text.Json.Serialization;
using OSLC4Net.Core.Attribute;
using OSLC4Net.Core.Model;

namespace StrictDocOslcRm.Models;

/// <summary>
/// OSLC Compact resource describing how to display a resource preview.
/// See https://docs.oasis-open-projects.org/oslc-op/core/v3.0/ps01/resource-preview.html
/// </summary>
[OslcResourceShape(title = "Compact Resource Shape", describes = new[] { OslcConstants.TYPE_COMPACT })]
[OslcNamespace(OslcConstants.OSLC_CORE_NAMESPACE)]
public class Compact : AbstractResource
{
    private string? _title;
    private string? _shortTitle;
    private Uri? _icon;
    private string? _iconTitle;
    private string? _iconAltLabel;
    private string? _iconSrcSet;
    private Preview? _smallPreview;
    private Preview? _largePreview;

    /// <summary>
    /// Title that may be used in the display of a link to the resource.
    /// The value should include only content that is valid inside an HTML &lt;span&gt; element.
    /// </summary>
    [OslcDescription("Title that may be used in the display of a link to the resource")]
    [OslcPropertyDefinition(OslcConstants.Domains.DCTerms.P.Title)]
    [OslcTitle("Title")]
    [OslcValueType(OSLC4Net.Core.Model.ValueType.XMLLiteral)]
    [JsonPropertyName("title")]
    public string? Title
    {
        get => _title;
        set => _title = value;
    }

    /// <summary>
    /// Abbreviated title which may be used in the display of a link to the resource.
    /// The value should include only content that is valid inside an HTML &lt;span&gt; element.
    /// </summary>
    [OslcDescription("Abbreviated title for display in limited space")]
    [OslcPropertyDefinition(OslcConstants.OSLC_CORE_NAMESPACE + "shortTitle")]
    [OslcTitle("Short Title")]
    [OslcValueType(OSLC4Net.Core.Model.ValueType.XMLLiteral)]
    [JsonPropertyName("shortTitle")]
    public string? ShortTitle
    {
        get => _shortTitle;
        set => _shortTitle = value;
    }

    /// <summary>
    /// URI of an image which may be used in the display of a link to the resource.
    /// </summary>
    [OslcDescription("URI of an icon for the resource")]
    [OslcPropertyDefinition(OslcConstants.OSLC_CORE_NAMESPACE + "icon")]
    [OslcTitle("Icon")]
    [JsonPropertyName("icon")]
    public Uri? Icon
    {
        get => _icon;
        set => _icon = value;
    }

    /// <summary>
    /// Title used in association with the icon, such as HTML img tag's title attribute.
    /// </summary>
    [OslcDescription("Title for the icon")]
    [OslcPropertyDefinition(OslcConstants.OSLC_CORE_NAMESPACE + "iconTitle")]
    [OslcTitle("Icon Title")]
    [JsonPropertyName("iconTitle")]
    public string? IconTitle
    {
        get => _iconTitle;
        set => _iconTitle = value;
    }

    /// <summary>
    /// Alternative label used in association with the icon, such as HTML img tag's alt attribute.
    /// </summary>
    [OslcDescription("Alternative label for the icon")]
    [OslcPropertyDefinition(OslcConstants.OSLC_CORE_NAMESPACE + "iconAltLabel")]
    [OslcTitle("Icon Alt Label")]
    [JsonPropertyName("iconAltLabel")]
    public string? IconAltLabel
    {
        get => _iconAltLabel;
        set => _iconAltLabel = value;
    }

    /// <summary>
    /// Specification of a set of images of different sizes based on HTML img element srcset attribute.
    /// </summary>
    [OslcDescription("Specification of a set of images of different sizes")]
    [OslcPropertyDefinition(OslcConstants.OSLC_CORE_NAMESPACE + "iconSrcSet")]
    [OslcTitle("Icon Source Set")]
    [JsonPropertyName("iconSrcSet")]
    public string? IconSrcSet
    {
        get => _iconSrcSet;
        set => _iconSrcSet = value;
    }

    /// <summary>
    /// URI and sizing properties for an HTML document to be used for a small preview.
    /// </summary>
    [OslcDescription("Small preview representation")]
    [OslcPropertyDefinition(OslcConstants.OSLC_CORE_NAMESPACE + "smallPreview")]
    [OslcTitle("Small Preview")]
    [OslcValueType(OSLC4Net.Core.Model.ValueType.LocalResource)]
    [JsonPropertyName("smallPreview")]
    public Preview? SmallPreview
    {
        get => _smallPreview;
        set => _smallPreview = value;
    }

    /// <summary>
    /// URI and sizing properties for an HTML document to be used for a large preview.
    /// </summary>
    [OslcDescription("Large preview representation")]
    [OslcPropertyDefinition(OslcConstants.OSLC_CORE_NAMESPACE + "largePreview")]
    [OslcTitle("Large Preview")]
    [OslcValueType(OSLC4Net.Core.Model.ValueType.LocalResource)]
    [JsonPropertyName("largePreview")]
    public Preview? LargePreview
    {
        get => _largePreview;
        set => _largePreview = value;
    }
}

/// <summary>
/// An HTML representation of a resource that can be embedded in another user interface.
/// See https://docs.oasis-open-projects.org/oslc-op/core/v3.0/ps01/resource-preview.html
/// </summary>
[OslcResourceShape(title = "Preview Resource Shape", describes = new[] { OslcConstants.TYPE_PREVIEW })]
[OslcNamespace(OslcConstants.OSLC_CORE_NAMESPACE)]
public class Preview
{
    private Uri? _document;
    private string? _hintWidth;
    private string? _hintHeight;

    /// <summary>
    /// The URI of an HTML document to be used for the preview.
    /// </summary>
    [OslcDescription("URI of an HTML document for the preview")]
    [OslcPropertyDefinition(OslcConstants.OSLC_CORE_NAMESPACE + "document")]
    [OslcTitle("Document")]
    [OslcOccurs(Occurs.ExactlyOne)]
    [JsonPropertyName("document")]
    public Uri? Document
    {
        get => _document;
        set => _document = value;
    }

    /// <summary>
    /// Recommended width of the preview. Values are expressed using length units as specified in CSS21.
    /// </summary>
    [OslcDescription("Recommended width of the preview (CSS units)")]
    [OslcPropertyDefinition(OslcConstants.OSLC_CORE_NAMESPACE + "hintWidth")]
    [OslcTitle("Hint Width")]
    [JsonPropertyName("hintWidth")]
    public string? HintWidth
    {
        get => _hintWidth;
        set => _hintWidth = value;
    }

    /// <summary>
    /// Recommended height of the preview. Values are expressed using length units as specified in CSS21.
    /// </summary>
    [OslcDescription("Recommended height of the preview (CSS units)")]
    [OslcPropertyDefinition(OslcConstants.OSLC_CORE_NAMESPACE + "hintHeight")]
    [OslcTitle("Hint Height")]
    [JsonPropertyName("hintHeight")]
    public string? HintHeight
    {
        get => _hintHeight;
        set => _hintHeight = value;
    }
}
