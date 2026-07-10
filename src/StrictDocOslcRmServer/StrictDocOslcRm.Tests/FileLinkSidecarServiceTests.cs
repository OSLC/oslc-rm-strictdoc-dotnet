using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OSLC4Net.Domains.RequirementsManagement;
using StrictDocOslcRm.Services;

namespace StrictDocOslcRm.Tests;

public class FileLinkSidecarServiceTests
{
    [Test]
    public async Task ReplaceLinksAsync_StoresOnlyWritableSidecarGraphAndSkolemizesBlankNodes()
    {
        // Arrange
        var storePath = Path.Combine(Path.GetTempPath(), "strictdoc-oslc-rm-tests", Guid.NewGuid().ToString("N"), "link-sidecars.json");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LinkSidecars:StorePath"] = storePath
            })
            .Build();
        var logger = Substitute.For<ILogger<FileLinkSidecarService>>();
        var service = new FileLinkSidecarService(configuration, logger);
        var resourceUri = new Uri("https://strictdoc-rm.oslc.ldsw.eu/?a=REQ-001");
        var publicBaseUri = new Uri("https://strictdoc-rm.oslc.ldsw.eu");
        var rdfXml = """
                     <?xml version="1.0" encoding="utf-8"?>
                     <rdf:RDF
                         xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"
                         xmlns:dcterms="http://purl.org/dc/terms/"
                         xmlns:oslc_rm="http://open-services.net/ns/rm#">
                       <rdf:Description rdf:about="https://strictdoc-rm.oslc.ldsw.eu/?a=REQ-001">
                         <dcterms:title>StrictDoc-owned title must not be sidecar persisted</dcterms:title>
                         <oslc_rm:affectedBy rdf:resource="https://jazz.example/ccm/resource/itemName/com.ibm.team.workitem.WorkItem/1" />
                         <oslc_rm:trackedBy>
                           <rdf:Description>
                             <rdf:value rdf:resource="https://jazz.example/ccm/resource/itemName/com.ibm.team.workitem.WorkItem/2" />
                             <dcterms:title>Jazz link title</dcterms:title>
                           </rdf:Description>
                         </oslc_rm:trackedBy>
                         <oslc_rm:satisfiedBy rdf:resource="https://strictdoc-rm.oslc.ldsw.eu/.well-known/genid/jazz-link-object-1" />
                       </rdf:Description>
                       <rdf:Description rdf:about="https://strictdoc-rm.oslc.ldsw.eu/.well-known/genid/jazz-link-object-1">
                         <rdf:value rdf:resource="https://jazz.example/ccm/resource/itemName/com.ibm.team.workitem.WorkItem/3" />
                         <dcterms:title>Jazz named link object title</dcterms:title>
                       </rdf:Description>
                       <rdf:Description rdf:about="https://strictdoc-rm.oslc.ldsw.eu/.well-known/genid/jazz-reified-link-1">
                         <rdf:type rdf:resource="http://www.w3.org/1999/02/22-rdf-syntax-ns#Statement" />
                         <rdf:subject rdf:resource="https://strictdoc-rm.oslc.ldsw.eu/?a=REQ-001" />
                         <rdf:predicate rdf:resource="http://open-services.net/ns/rm#affectedBy" />
                         <rdf:object rdf:resource="https://jazz.example/ccm/resource/itemName/com.ibm.team.workitem.WorkItem/1" />
                         <dcterms:title>Jazz named reified link title</dcterms:title>
                       </rdf:Description>
                     </rdf:RDF>
                     """;

        // Act
        await service.ReplaceLinksAsync(resourceUri, "application/rdf+xml", rdfXml, publicBaseUri);

        var ntriples = await service.GetNTriplesAsync(resourceUri);
        var requirement = new Requirement
        {
            Identifier = "REQ-001",
            Title = "Requirement",
            Description = "Requirement"
        };
        await service.ApplyLinksAsync(requirement, resourceUri);
        var namedLinkObjectNTriples = await service.GetResourceNTriplesAsync(
            new Uri("https://strictdoc-rm.oslc.ldsw.eu/.well-known/genid/jazz-link-object-1"));
        var namedReifiedNTriples = await service.GetResourceNTriplesAsync(
            new Uri("https://strictdoc-rm.oslc.ldsw.eu/.well-known/genid/jazz-reified-link-1"));
        var skolemUri = Regex.Match(
            ntriples!,
            @"https://strictdoc-rm\.oslc\.ldsw\.eu/\.well-known/genid/oslc_[0-9a-f]+").Value;
        var skolemNTriples = await service.GetResourceNTriplesAsync(new Uri(skolemUri));

        // Assert
        await Assert.That(ntriples).IsNotNull();
        await Assert.That(ntriples!).DoesNotContain("_:");
        await Assert.That(ntriples!).Contains("https://strictdoc-rm.oslc.ldsw.eu/.well-known/genid/oslc_");
        await Assert.That(ntriples!).DoesNotContain("StrictDoc-owned title");
        await Assert.That(ntriples!).Contains("http://www.w3.org/1999/02/22-rdf-syntax-ns#subject");
        await Assert.That(ntriples!).Contains("http://www.w3.org/1999/02/22-rdf-syntax-ns#predicate");
        await Assert.That(ntriples!).Contains("http://www.w3.org/1999/02/22-rdf-syntax-ns#object");
        await Assert.That(ntriples!).Contains("Jazz named reified link title");
        await Assert.That(namedLinkObjectNTriples).Contains("Jazz named link object title");
        await Assert.That(namedReifiedNTriples).Contains("Jazz named reified link title");
        await Assert.That(skolemNTriples).Contains("Jazz link title");
        await Assert.That(requirement.AffectedBy).Contains(new Uri("https://jazz.example/ccm/resource/itemName/com.ibm.team.workitem.WorkItem/1"));
        await Assert.That(requirement.TrackedBy).Contains(new Uri("https://jazz.example/ccm/resource/itemName/com.ibm.team.workitem.WorkItem/2"));
        await Assert.That(requirement.SatisfiedBy).Contains(new Uri("https://jazz.example/ccm/resource/itemName/com.ibm.team.workitem.WorkItem/3"));
    }
}
