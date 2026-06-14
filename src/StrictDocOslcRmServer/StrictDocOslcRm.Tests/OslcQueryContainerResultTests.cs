using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OSLC4Net.Domains.RequirementsManagement;
using StrictDocOslcRm.Services;
using VDS.RDF;
using VDS.RDF.Parsing;

namespace StrictDocOslcRm.Tests;

/// <summary>
/// Serialization tests for the OSLC Query container. These also guard against the OSLC4Net
/// output-formatter ResponseInfo path, which coerces a missing next page to an empty string
/// and then calls new Uri(""), throwing for every non-paged query.
/// </summary>
public class OslcQueryContainerResultTests
{
    private const string Container = "http://localhost/oslc/service_provider/doc/requirements";

    private static List<Requirement> TwoRequirements()
    {
        var a = new Requirement { Identifier = "A", Title = "Alpha" };
        a.SetAbout(new Uri("http://localhost/?a=A"));
        var b = new Requirement { Identifier = "B", Title = "Beta" };
        b.SetAbout(new Uri("http://localhost/?a=B"));
        return [a, b];
    }

    private static async Task<IGraph> ExecuteAsync(OslcQueryContainerResult result, string accept)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Accept = accept;
        var body = new MemoryStream();
        httpContext.Response.Body = body;

        var actionContext = new ActionContext { HttpContext = httpContext };
        await result.ExecuteResultAsync(actionContext).ConfigureAwait(false);

        body.Position = 0;
        var payload = Encoding.UTF8.GetString(body.ToArray());
        var graph = new Graph();
        if (accept.Contains("turtle", StringComparison.Ordinal))
        {
            new TurtleParser().Load(graph, new StringReader(payload));
        }
        else
        {
            new RdfXmlParser().Load(graph, new StringReader(payload));
        }

        return graph;
    }

    [Test]
    public async Task WithoutNextPage_SerializesContainerWithTotalCountAndMembers()
    {
        var members = TwoRequirements();
        var result = new OslcQueryContainerResult(
            Container,
            Container + "?oslc.select=*",
            nextPageAbout: null,
            totalCount: 2,
            members,
            new Dictionary<string, object>());

        var graph = await ExecuteAsync(result, "application/rdf+xml").ConfigureAwait(false);

        var totalCount = graph.GetTriplesWithPredicate(
                graph.CreateUriNode(new Uri("http://open-services.net/ns/core#totalCount")))
            .Select(t => ((ILiteralNode)t.Object).Value)
            .Single();
        await Assert.That(totalCount).IsEqualTo("2");

        var memberCount = graph.GetTriplesWithPredicate(
            graph.CreateUriNode(new Uri("http://www.w3.org/2000/01/rdf-schema#member"))).Count();
        await Assert.That(memberCount).IsEqualTo(2);
    }

    [Test]
    public async Task WithNextPage_EmitsNextPageLink()
    {
        var members = TwoRequirements().Take(1).ToList();
        var result = new OslcQueryContainerResult(
            Container,
            Container + "?oslc.pageSize=1",
            nextPageAbout: Container + "?oslc.pageSize=1&page=2",
            totalCount: 2,
            members,
            new Dictionary<string, object>());

        var graph = await ExecuteAsync(result, "text/turtle").ConfigureAwait(false);

        var nextPage = graph.GetTriplesWithPredicate(
            graph.CreateUriNode(new Uri("http://open-services.net/ns/core#nextPage"))).Count();
        await Assert.That(nextPage).IsEqualTo(1);
    }
}
