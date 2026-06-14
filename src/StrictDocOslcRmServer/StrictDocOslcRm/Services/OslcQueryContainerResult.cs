using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using OSLC4Net.Core.DotNetRdfProvider;
using OSLC4Net.Core.Model;
using OSLC4Net.Domains.RequirementsManagement;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Writing;

namespace StrictDocOslcRm.Services;

/// <summary>
/// An <see cref="IActionResult"/> that serializes an OSLC Query response container
/// (<c>oslc:ResponseInfo</c> with <c>oslc:totalCount</c>, <c>oslc:nextPage</c> and
/// <c>rdfs:member</c> links) for a page of <see cref="Requirement"/> members.
/// </summary>
/// <remarks>
/// This builds the container graph directly with
/// <see cref="DotNetRdfHelper.CreateDotNetRdfGraph(string?, string?, string?, long?, IEnumerable{object}?, IDictionary{string, object}?)"/>
/// rather than returning a <see cref="ResponseInfoArray{T}"/> through the OSLC4Net output
/// formatter: that formatter coerces a missing next page to an empty string and then calls
/// <c>new Uri("")</c>, which throws for every non-paged query.
/// </remarks>
public sealed class OslcQueryContainerResult : IActionResult
{
    private readonly string _containerAbout;
    private readonly string _responseInfoAbout;
    private readonly string? _nextPageAbout;
    private readonly int _totalCount;
    private readonly IReadOnlyList<Requirement> _members;
    private readonly IDictionary<string, object> _properties;

    public OslcQueryContainerResult(
        string containerAbout,
        string responseInfoAbout,
        string? nextPageAbout,
        int totalCount,
        IReadOnlyList<Requirement> members,
        IDictionary<string, object> properties)
    {
        _containerAbout = containerAbout;
        _responseInfoAbout = responseInfoAbout;
        _nextPageAbout = nextPageAbout;
        _totalCount = totalCount;
        _members = members;
        _properties = properties;
    }

    public async Task ExecuteResultAsync(ActionContext context)
    {
        var graph = DotNetRdfHelper.CreateDotNetRdfGraph(
            _containerAbout,
            _responseInfoAbout,
            _nextPageAbout,
            _totalCount,
            _members,
            _properties);

        var (mediaType, format) = Negotiate(context.HttpContext.Request.Headers.Accept);

        var response = context.HttpContext.Response;
        response.ContentType = mediaType + "; charset=utf-8";
        response.Headers[HeaderNames.Vary] = "Accept";

        var payload = Serialize(graph, format);
        await response.WriteAsync(payload, Encoding.UTF8).ConfigureAwait(false);
    }

    private static (string MediaType, RdfFormat Format) Negotiate(string? accept)
    {
        accept ??= string.Empty;

        if (accept.Contains(OslcMediaType.TEXT_TURTLE, StringComparison.OrdinalIgnoreCase))
        {
            return (OslcMediaType.TEXT_TURTLE, RdfFormat.Turtle);
        }

        if (accept.Contains(OslcMediaType.APPLICATION_JSON_LD, StringComparison.OrdinalIgnoreCase))
        {
            return (OslcMediaType.APPLICATION_JSON_LD, RdfFormat.JsonLd);
        }

        if (accept.Contains(OslcMediaType.APPLICATION_NTRIPLES, StringComparison.OrdinalIgnoreCase))
        {
            return (OslcMediaType.APPLICATION_NTRIPLES, RdfFormat.NTriples);
        }

        // RDF/XML is the OSLC default and what the OSLC4Net client requests.
        return (OslcMediaType.APPLICATION_RDF_XML, RdfFormat.RdfXml);
    }

    private static string Serialize(IGraph graph, RdfFormat format)
    {
        using var writer = new System.IO.StringWriter();

        if (format == RdfFormat.JsonLd)
        {
            var store = new TripleStore();
            store.Add(graph);
            new JsonLdWriter(new JsonLdWriterOptions { JsonFormatting = Formatting.Indented })
                .Save(store, writer);
            return writer.ToString();
        }

        IRdfWriter rdfWriter = format switch
        {
            RdfFormat.RdfXml => new RdfXmlWriter { PrettyPrintMode = true },
            RdfFormat.Turtle => new CompressingTurtleWriter(TurtleSyntax.W3C) { PrettyPrintMode = true },
            RdfFormat.NTriples => new NTriplesWriter(NTriplesSyntax.Rdf11),
            _ => new RdfXmlWriter { PrettyPrintMode = true },
        };

        rdfWriter.Save(graph, writer);
        return writer.ToString();
    }

    private enum RdfFormat
    {
        RdfXml,
        Turtle,
        NTriples,
        JsonLd,
    }
}
