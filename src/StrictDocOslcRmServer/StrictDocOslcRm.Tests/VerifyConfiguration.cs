using System.Runtime.CompilerServices;
using EmptyFiles;
using OSLC4Net.Core.DotNetRdfProvider;
using OSLC4Net.Core.Model;
using VDS.RDF;
using VDS.RDF.Writing;

namespace StrictDocOslcRm.Tests;

public static class VerifyConfiguration
{
    [ModuleInitializer]
    public static void Init()
    {
        FileExtensions.AddTextExtension("ttl");

        VerifierSettings.RegisterFileConverter<IGraph>(
            (graph, _) =>
            {
                using var writer = new System.IO.StringWriter();

                var rdfc = new RdfCanonicalizer();
                var graphCollection = new GraphCollection
                {
                    { graph, false }
                };
                var canonicalizedRdfDataset = rdfc.Canonicalize(new TripleStore(graphCollection));

                var newGraph = canonicalizedRdfDataset.OutputDataset.Graphs.Single();

                // https://www.w3.org/TR/rdf-canon/ prescribes N-Quads serialization, but let's try Turtle for better readability
                var ttlWriter = new CompressingTurtleWriter();
                ttlWriter.Save(newGraph, writer);
                return new ConversionResult(null, "ttl", writer.ToString());
            });

        VerifierSettings.RegisterFileConverter<IResource>(
            (resource, _) =>
            {
                using var writer = new System.IO.StringWriter();
                var graph = DotNetRdfHelper.CreateDotNetRdfGraph([resource]);

                var rdfc = new RdfCanonicalizer();
                var graphCollection = new GraphCollection
                {
                    { graph, false }
                };
                var canonicalizedRdfDataset = rdfc.Canonicalize(new TripleStore(graphCollection));

                var newGraph = canonicalizedRdfDataset.OutputDataset.Graphs.Single();

                // https://www.w3.org/TR/rdf-canon/ prescribes N-Quads serialization, but let's try Turtle for better readability
                var ttlWriter = new CompressingTurtleWriter();
                ttlWriter.Save(newGraph, writer);
                return new ConversionResult(null, "ttl", writer.ToString());
            });
    }
}
