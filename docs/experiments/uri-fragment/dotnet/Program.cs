using VDS.RDF;
using System.Diagnostics;

var affectedBy = "http://open-services.net/ns/rm#affectedBy";
var trackedBy = "http://open-services.net/ns/rm#trackedBy";

var uriA = new Uri(affectedBy);
var uriB = new Uri(trackedBy);

Console.WriteLine(".NET System.Uri");
Console.WriteLine($"type={uriA.GetType().FullName}");
Console.WriteLine($"a={uriA.AbsoluteUri} fragment={uriA.Fragment}");
Console.WriteLine($"b={uriB.AbsoluteUri} fragment={uriB.Fragment}");
Console.WriteLine($"a.Equals(b)={uriA.Equals(uriB)}");
Console.WriteLine($"a == b={uriA == uriB}");
Console.WriteLine($"hash(a)={uriA.GetHashCode()} hash(b)={uriB.GetHashCode()}");
Console.WriteLine($"dotNetRDF EqualityHelper.AreUrisEqual(a,b)={EqualityHelper.AreUrisEqual(uriA, uriB)}");
Console.WriteLine($"HashSet<Uri>.Count={new HashSet<Uri> { uriA, uriB }.Count}");
Console.WriteLine($"HashSet<string>.Count={new HashSet<string>(StringComparer.Ordinal) { affectedBy, trackedBy }.Count}");

var graph = new Graph();
Console.WriteLine($"graph.UriFactory type={graph.UriFactory.GetType().FullName}");
Console.WriteLine($"graph.UriFactory.InternUris={graph.UriFactory.InternUris}");
Console.WriteLine($"UriFactory.Root type={UriFactory.Root.GetType().FullName}");
Console.WriteLine($"UriFactory.InternUris={UriFactory.InternUris}");
var rootUri1 = UriFactory.Create(affectedBy);
var rootUri2 = UriFactory.Create(affectedBy);
var graphUri1 = graph.UriFactory.Create(affectedBy);
var graphUri2 = graph.UriFactory.Create(affectedBy);
Console.WriteLine($"ReferenceEquals(UriFactory.Create same string)={ReferenceEquals(rootUri1, rootUri2)}");
Console.WriteLine($"ReferenceEquals(graph.UriFactory.Create same string)={ReferenceEquals(graphUri1, graphUri2)}");
Console.WriteLine($"ReferenceEquals(rootUri, graphUri)={ReferenceEquals(rootUri1, graphUri1)}");
var subject = graph.CreateUriNode(new Uri("http://example.com/s"));
var objectNode = graph.CreateUriNode(new Uri("http://example.com/o"));
var nodeA = graph.CreateUriNode(uriA);
var nodeB = graph.CreateUriNode(uriB);
var nodeAFromFactoryString = graph.CreateUriNode(graph.UriFactory.Create(affectedBy));
var nodeBFromFactoryString = graph.CreateUriNode(graph.UriFactory.Create(trackedBy));

Console.WriteLine();
Console.WriteLine("dotNetRDF IUriNode");
Console.WriteLine($"node type={nodeA.GetType().FullName}");
Console.WriteLine($"native uri type={nodeA.Uri.GetType().FullName}");
Console.WriteLine($"nodeA.Uri={nodeA.Uri.AbsoluteUri}");
Console.WriteLine($"nodeB.Uri={nodeB.Uri.AbsoluteUri}");
Console.WriteLine($"nodeA.Equals(nodeB)={nodeA.Equals(nodeB)}");
Console.WriteLine($"nodeA.Uri.Equals(nodeB.Uri)={nodeA.Uri.Equals(nodeB.Uri)}");
Console.WriteLine($"HashSet<IUriNode>.Count={new HashSet<IUriNode> { nodeA, nodeB }.Count}");
Console.WriteLine($"nodeAFromFactoryString.Equals(nodeBFromFactoryString)={nodeAFromFactoryString.Equals(nodeBFromFactoryString)}");
Console.WriteLine($"nodeAFromFactoryString.Uri.Equals(nodeBFromFactoryString.Uri)={nodeAFromFactoryString.Uri.Equals(nodeBFromFactoryString.Uri)}");
Console.WriteLine($"HashSet<IUriNode> from UriFactory strings.Count={new HashSet<IUriNode> { nodeAFromFactoryString, nodeBFromFactoryString }.Count}");

try
{
    var nodeFromStringOverload = graph.CreateUriNode(affectedBy);
    Console.WriteLine($"CreateUriNode(string full URI)={nodeFromStringOverload.Uri.AbsoluteUri}");
}
catch (Exception ex)
{
    Console.WriteLine($"CreateUriNode(string full URI) throws {ex.GetType().Name}: {ex.Message}");
}

var lexicalUriComparer = EqualityComparer<Uri>.Create(
    (left, right) => string.Equals(left?.AbsoluteUri, right?.AbsoluteUri, StringComparison.Ordinal),
    uri => StringComparer.Ordinal.GetHashCode(uri.AbsoluteUri));
Console.WriteLine($"HashSet<Uri>(lexical comparer).Count={new HashSet<Uri>(lexicalUriComparer) { uriA, uriB }.Count}");

graph.Assert(new Triple(subject, nodeA, objectNode));
graph.Assert(new Triple(subject, nodeB, objectNode));
Console.WriteLine($"Graph triple count after two fragment-distinct predicates={graph.Triples.Count}");
Console.WriteLine($"TriplesWithPredicate(affectedBy)={graph.GetTriplesWithPredicate(nodeA).Count()}");
Console.WriteLine($"TriplesWithPredicate(trackedBy)={graph.GetTriplesWithPredicate(nodeB).Count()}");

Console.WriteLine();
Console.WriteLine("Rough construction cost probe");
const int iterations = 100_000;
Measure("new Uri(string)", iterations, i => _ = new Uri($"{affectedBy}-{i}"));
Measure("graph.UriFactory.Create(string)", iterations, i => _ = graph.UriFactory.Create($"{affectedBy}-{i}"));
Measure("graph.CreateUriNode(new Uri(string))", iterations, i => _ = graph.CreateUriNode(new Uri($"{affectedBy}-{i}")));
Measure("graph.CreateUriNode(graph.UriFactory.Create(string))", iterations, i => _ = graph.CreateUriNode(graph.UriFactory.Create($"{affectedBy}-{i}")));
Measure("new RdfUri(string)", iterations, i => _ = new RdfUri($"{affectedBy}-{i}"));
Measure("new Uri(string), repeated", iterations, _ => { var unused = new Uri(affectedBy); });
Measure("graph.UriFactory.Create(string), repeated", iterations, _ => { var unused = graph.UriFactory.Create(affectedBy); });
Measure("graph.CreateUriNode(new Uri(string)), repeated", iterations, _ => { var unused = graph.CreateUriNode(new Uri(affectedBy)); });
Measure("graph.CreateUriNode(graph.UriFactory.Create(string)), repeated", iterations, _ => { var unused = graph.CreateUriNode(graph.UriFactory.Create(affectedBy)); });
Measure("new RdfUri(string), repeated", iterations, _ => { var unused = new RdfUri(affectedBy); });

var rdfUriSet = new HashSet<RdfUri> { new(affectedBy), new(trackedBy) };
Console.WriteLine($"HashSet<RdfUri>.Count={rdfUriSet.Count}");

static void Measure(string name, int iterations, Action<int> action)
{
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    var before = GC.GetAllocatedBytesForCurrentThread();
    var sw = Stopwatch.StartNew();
    for (var i = 0; i < iterations; i++)
    {
        action(i);
    }
    sw.Stop();
    var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
    Console.WriteLine($"{name}: {sw.ElapsedMilliseconds} ms, {allocated / (double)iterations:F1} bytes/op");
}

readonly record struct RdfUri(string Value)
{
    public override string ToString() => Value;
}
