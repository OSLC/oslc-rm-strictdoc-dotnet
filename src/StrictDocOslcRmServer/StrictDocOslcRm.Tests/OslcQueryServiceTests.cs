using OSLC4Net.Domains.RequirementsManagement;
using StrictDocOslcRm.Services;

namespace StrictDocOslcRm.Tests;

/// <summary>
/// Verifies server-side OSLC Query evaluation against the same criteria the
/// oslc4net-query QuerySupportTester applies: a filtered result must be non-empty,
/// strictly smaller than the unfiltered container total, and include or exclude the
/// sampled member according to the operator.
/// </summary>
public class OslcQueryServiceTests
{
    private const string Mgmt = "SDOC-HIGH-REQS-MANAGEMENT";
    private const string Decomp = "SDOC-HIGH-REQS-DECOMP";
    private const string Base = "http://localhost/?a=";
    private const string Dcterms = "http://purl.org/dc/terms/";

    private readonly OslcQueryService _service = new();

    private static List<Requirement> Fixture()
    {
        var management = new Requirement
        {
            Identifier = Mgmt,
            Title = "Requirements management",
            Description = "StrictDoc shall enable requirements management.",
        };
        management.SetAbout(new Uri(Base + Mgmt));

        var decomposition = new Requirement
        {
            Identifier = Decomp,
            Title = "Requirements decomposition",
            Description = "StrictDoc shall support requirement decomposition.",
            Decomposes = [new Uri(Base + Mgmt)],
        };
        decomposition.SetAbout(new Uri(Base + Decomp));

        return [management, decomposition];
    }

    private OslcQueryOutcome Run(string? where, string? select = "*", string? searchTerms = null)
    {
        return _service.Apply(
            Fixture(),
            prefix: null,
            where: where,
            select: select,
            orderBy: null,
            searchTerms: searchTerms,
            pageSize: null,
            page: 1,
            nextPageUriFactory: page => $"http://localhost/q?page={page}");
    }

    private static IReadOnlyList<string> Ids(OslcQueryOutcome outcome) =>
        outcome.Members.Select(member => member.Identifier).OrderBy(id => id, StringComparer.Ordinal).ToList();

    [Test]
    public async Task Baseline_ReturnsAllMembers()
    {
        var outcome = Run(where: null);
        await Assert.That(outcome.TotalCount).IsEqualTo(2);
        await Assert.That(outcome.Members.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Equals_String_SelectsSingleMember()
    {
        var outcome = Run($"dcterms:identifier=\"{Decomp}\"");
        await Assert.That(outcome.TotalCount).IsEqualTo(1);
        await Assert.That(Ids(outcome)).Contains(Decomp);
    }

    [Test]
    public async Task NotEquals_String_ExcludesSampleAndKeepsRest()
    {
        var outcome = Run($"dcterms:identifier!=\"{Decomp}\"");
        await Assert.That(outcome.TotalCount).IsEqualTo(1);
        await Assert.That(Ids(outcome)).Contains(Mgmt);
        await Assert.That(Ids(outcome)).DoesNotContain(Decomp);
    }

    [Test]
    public async Task In_String_SelectsListedMember()
    {
        var outcome = Run($"dcterms:identifier in [\"{Decomp}\"]");
        await Assert.That(outcome.TotalCount).IsEqualTo(1);
        await Assert.That(Ids(outcome)).Contains(Decomp);
    }

    [Test]
    public async Task LessThan_String_OrdersLexically()
    {
        // "Requirements decomposition" < "Requirements management"
        var outcome = Run("dcterms:title<\"Requirements management\"");
        await Assert.That(outcome.TotalCount).IsEqualTo(1);
        await Assert.That(Ids(outcome)).Contains(Decomp);
    }

    [Test]
    public async Task GreaterEquals_String_IncludesBoundary()
    {
        var outcome = Run("dcterms:title>=\"Requirements management\"");
        await Assert.That(outcome.TotalCount).IsEqualTo(1);
        await Assert.That(Ids(outcome)).Contains(Mgmt);
    }

    [Test]
    public async Task GreaterThan_AboveMax_ReturnsEmpty()
    {
        var outcome = Run("dcterms:title>\"Requirements management\"");
        await Assert.That(outcome.TotalCount).IsEqualTo(0);
    }

    [Test]
    public async Task Equals_UriReference_SelectsMemberWithRelation()
    {
        var outcome = Run($"oslc_rm:decomposes=<{Base}{Mgmt}>");
        await Assert.That(outcome.TotalCount).IsEqualTo(1);
        await Assert.That(Ids(outcome)).Contains(Decomp);
    }

    [Test]
    public async Task NotEquals_UriReference_IncludesMemberWithoutValue()
    {
        // MANAGEMENT has no decomposes value, so it is not equal to the operand.
        var outcome = Run($"oslc_rm:decomposes!=<{Base}{Mgmt}>");
        await Assert.That(outcome.TotalCount).IsEqualTo(1);
        await Assert.That(Ids(outcome)).Contains(Mgmt);
    }

    [Test]
    public async Task SearchTerms_MatchesTitleSubstring()
    {
        var outcome = Run(where: null, searchTerms: "\"decomposition\"");
        await Assert.That(outcome.TotalCount).IsEqualTo(1);
        await Assert.That(Ids(outcome)).Contains(Decomp);
    }

    [Test]
    public async Task Select_NarrowsProjectionToRequestedProperty()
    {
        var outcome = Run(where: null, select: "dcterms:title");
        await Assert.That(outcome.SelectedProperties.ContainsKey(Dcterms + "title")).IsTrue();
        await Assert.That(outcome.SelectedProperties.ContainsKey(Dcterms + "identifier")).IsFalse();
    }

    [Test]
    public async Task InvalidWhere_ThrowsBadRequest()
    {
        await Assert.That(() => Run("not a valid expression #"))
            .Throws<OslcQueryBadRequestException>();
    }

    [Test]
    public async Task Paging_AdvertisesNextPageAndSlices()
    {
        var outcome = _service.Apply(
            Fixture(), prefix: null, where: null, select: "*", orderBy: null,
            searchTerms: null, pageSize: 1, page: 1,
            nextPageUriFactory: page => $"http://localhost/q?page={page}");

        await Assert.That(outcome.TotalCount).IsEqualTo(2);
        await Assert.That(outcome.Members.Count).IsEqualTo(1);
        await Assert.That(outcome.NextPage).IsEqualTo("http://localhost/q?page=2");
    }
}
