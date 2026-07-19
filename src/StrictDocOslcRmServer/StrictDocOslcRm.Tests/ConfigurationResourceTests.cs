using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Formatters;
using OSLC4Net.Core.Model;
using OSLC4Net.Server.Providers;
using StrictDocOslcRm.Models;
using StrictDocOslcRm.Services;

namespace StrictDocOslcRm.Tests;

public sealed class ConfigurationResourceTests
{
    [Test]
    public async Task GenericConfiguration_SerializesAsConfigurationWithMatchingContributionType()
    {
        var resource = ConfigurationResourceFactory.CreateConfiguration(
            new ConfigurationContext("main", "HEAD", "/data/main/HEAD"),
            "https://strictdoc.example");

        await Verify(resource);
    }

    [Test]
    public async Task Stream_SerializesWithConfigurationSecondaryTypeAndBaselinesContainer()
    {
        var resource = ConfigurationResourceFactory.CreateTypedConfiguration(
            new ConfigurationContext("main", "HEAD", "/data/main/HEAD"),
            "https://strictdoc.example",
            new Uri("https://strictdoc.example/oslc_config/configurations/main/v0.1.0"));

        await Verify(resource);
    }

    [Test]
    public async Task Baseline_SerializesWithConfigurationSecondaryTypeAndSourceStream()
    {
        var resource = ConfigurationResourceFactory.CreateTypedConfiguration(
            new ConfigurationContext("main", "v0.1.0", "/data/main/v0.1.0"),
            "https://strictdoc.example");

        await Verify(resource);
    }

    [Test]
    public async Task BaselinesContainer_SerializesAsAnLdpContainer()
    {
        var container = ConfigurationResourceFactory.CreateBaselinesContainer(
            "main",
            [new ConfigurationContext("main", "v0.1.0", "/data/main/v0.1.0")],
            "https://strictdoc.example");

        await Verify(container);
    }

    [Test]
    public async Task Stream_RdfXmlAdvertisesConcreteAndGenericConfigurationTypes()
    {
        var resource = ConfigurationResourceFactory.CreateTypedConfiguration(
            new ConfigurationContext("main", "HEAD", "/data/main/HEAD"),
            "https://strictdoc.example");
        var document = XDocument.Parse(await SerializeRdfXmlAsync(resource));
        var rdf = XNamespace.Get(OslcConstants.RDF_NAMESPACE);
        var configuration = XNamespace.Get(ConfigurationVocabulary.Namespace);
        var subject = document
            .Descendants()
            .Single(element => element.Attribute(rdf + "about")?.Value ==
                               "https://strictdoc.example/oslc_config/configurations/main/HEAD");
        var types = subject
            .Elements(rdf + "type")
            .Select(element => element.Attribute(rdf + "resource")?.Value)
            .ToArray();

        await Assert.That(subject.Name).IsEqualTo(configuration + "Stream");
        await Assert.That(types).Contains(ConfigurationVocabulary.Configuration);
    }

    private static async Task<string> SerializeRdfXmlAsync(IResource resource)
    {
        var httpContext = new DefaultHttpContext();
        using var body = new MemoryStream();
        httpContext.Response.Body = body;
        var context = new OutputFormatterWriteContext(
            httpContext,
            (stream, encoding) => new StreamWriter(stream, encoding),
            resource.GetType(),
            resource)
        {
            ContentType = OslcMediaType.APPLICATION_RDF_XML,
        };

        await new OslcRdfOutputFormatter().WriteResponseBodyAsync(context, Encoding.UTF8);

        body.Position = 0;
        using var reader = new StreamReader(body);
        return await reader.ReadToEndAsync();
    }
}
