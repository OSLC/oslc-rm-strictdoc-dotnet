namespace StrictDocOslcRm.Models;

using OSLC4Net.Domains.RequirementsManagement;

public class RequirementSelectionViewModel
{
    public string SelectorUri { get; set; } = string.Empty;
    public string? Terms { get; set; }
    public IEnumerable<Requirement> Results { get; set; } = Enumerable.Empty<Requirement>();
}
