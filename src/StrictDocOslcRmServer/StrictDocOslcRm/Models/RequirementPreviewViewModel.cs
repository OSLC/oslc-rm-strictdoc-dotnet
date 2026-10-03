using Microsoft.AspNetCore.Html;
using OSLC4Net.Domains.RequirementsManagement;

namespace StrictDocOslcRm.Models;

/// <summary>
/// View model for requirement preview pages
/// </summary>
public class RequirementPreviewViewModel
{
    public Requirement Requirement { get; set; } = null!;
    public string RequirementUri { get; set; } = string.Empty;
    public IHtmlContent SanitizedTitle { get; set; } = new HtmlString(string.Empty);
    public IHtmlContent SanitizedDescription { get; set; } = new HtmlString(string.Empty);
}
