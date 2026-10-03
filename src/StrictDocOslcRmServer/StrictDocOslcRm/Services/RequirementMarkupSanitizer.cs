using Ganss.Xss;
using Microsoft.AspNetCore.Html;

namespace StrictDocOslcRm.Services;

public interface IRequirementMarkupSanitizer
{
    IHtmlContent SanitizeTitle(string? markup);
    IHtmlContent SanitizeDescription(string? markup);
}

/// <summary>
/// Sanitizes OSLC rich-text fragments for insertion into title and description elements.
/// </summary>
public sealed class RequirementMarkupSanitizer : IRequirementMarkupSanitizer
{
    private static readonly HtmlSanitizer TitleSanitizer = CreateSanitizer(
    [
        "a", "abbr", "b", "br", "cite", "code", "del", "em", "i", "ins", "kbd", "mark", "q", "s",
        "samp", "small", "span", "strong", "sub", "sup", "u", "var", "wbr"
    ]);

    private static readonly HtmlSanitizer DescriptionSanitizer = CreateSanitizer(
    [
        "a", "abbr", "b", "br", "cite", "code", "del", "em", "i", "ins", "kbd", "mark", "q", "s",
        "samp", "small", "span", "strong", "sub", "sup", "u", "var", "wbr",
        "blockquote", "div", "h1", "h2", "h3", "h4", "h5", "h6", "hr", "li", "ol", "p", "pre",
        "table", "caption", "col", "colgroup", "tbody", "td", "tfoot", "th", "thead", "tr", "ul"
    ]);

    public IHtmlContent SanitizeTitle(string? markup) =>
        new HtmlString(TitleSanitizer.Sanitize(markup ?? string.Empty));

    public IHtmlContent SanitizeDescription(string? markup) =>
        new HtmlString(DescriptionSanitizer.Sanitize(markup ?? string.Empty));

    private static HtmlSanitizer CreateSanitizer(IEnumerable<string> allowedTags)
    {
        var sanitizer = new HtmlSanitizer();

        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedTags.UnionWith(allowedTags);

        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.UnionWith(["href", "title", "lang", "dir"]);

        sanitizer.UriAttributes.Clear();
        sanitizer.UriAttributes.Add("href");
        sanitizer.UriListAttributes.Clear();

        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.UnionWith(["http", "https"]);

        sanitizer.AllowedCssProperties.Clear();
        sanitizer.AllowedAtRules.Clear();
        sanitizer.AllowedClasses.Clear();
        sanitizer.AllowDataAttributes = false;

        return sanitizer;
    }
}
