using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OSLC4Net.Domains.RequirementsManagement;
using StrictDocOslcRm.Services;

namespace StrictDocOslcRm.Tests;

public sealed class StrictDocServiceRelationTests : IAsyncDisposable
{
    private const string BaseUrl = "https://strictdoc.example.test";
    private readonly string _dataDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    [Test]
    public async Task GetAllRequirements_MapsParentRolesAndTheirInverses()
    {
        Directory.CreateDirectory(_dataDirectory);
        var jsonPath = Path.Combine(_dataDirectory, "strictdoc.json");
        await File.WriteAllTextAsync(jsonPath, """
            {"DOCUMENTS":[{"_NODE_TYPE":"DOCUMENT","MID":"demo","TITLE":"Demo","NODES":[
              {"_NODE_TYPE":"REQUIREMENT","UID":"TARGET","TITLE":"Target","STATEMENT":"Target"},
              {"_NODE_TYPE":"REQUIREMENT","UID":"DECOMPOSES","TITLE":"Decomposes","STATEMENT":"Decomposes","RELATIONS":[{"TYPE":"Parent","ROLE":"Decomposes","VALUE":"TARGET"}]},
              {"_NODE_TYPE":"REQUIREMENT","UID":"ELABORATES","TITLE":"Elaborates","STATEMENT":"Elaborates","RELATIONS":[{"TYPE":"Parent","ROLE":"Elaborates","VALUE":"TARGET"}]},
              {"_NODE_TYPE":"REQUIREMENT","UID":"SPECIFIES","TITLE":"Specifies","STATEMENT":"Specifies","RELATIONS":[{"TYPE":"Parent","ROLE":"Specifies","VALUE":"TARGET"}]},
              {"_NODE_TYPE":"REQUIREMENT","UID":"CONSTRAINS","TITLE":"Constrains","STATEMENT":"Constrains","RELATIONS":[{"TYPE":"Parent","ROLE":"Constrains","VALUE":"TARGET"}]},
              {"_NODE_TYPE":"REQUIREMENT","UID":"SATISFIES","TITLE":"Satisfies","STATEMENT":"Satisfies","RELATIONS":[{"TYPE":"Parent","ROLE":"Satisfies","VALUE":"TARGET"}]},
              {"_NODE_TYPE":"REQUIREMENT","UID":"DECOMPOSED-BY","TITLE":"Decomposed by","STATEMENT":"Decomposed by","RELATIONS":[{"TYPE":"Parent","ROLE":"Decomposed by","VALUE":"TARGET"}]},
              {"_NODE_TYPE":"REQUIREMENT","UID":"ELABORATED-BY","TITLE":"Elaborated by","STATEMENT":"Elaborated by","RELATIONS":[{"TYPE":"Parent","ROLE":"Elaborated by","VALUE":"TARGET"}]},
              {"_NODE_TYPE":"REQUIREMENT","UID":"SPECIFIED-BY","TITLE":"Specified by","STATEMENT":"Specified by","RELATIONS":[{"TYPE":"Parent","ROLE":"Specified by","VALUE":"TARGET"}]},
              {"_NODE_TYPE":"REQUIREMENT","UID":"CONSTRAINED-BY","TITLE":"Constrained by","STATEMENT":"Constrained by","RELATIONS":[{"TYPE":"Parent","ROLE":"Constrained by","VALUE":"TARGET"}]},
              {"_NODE_TYPE":"REQUIREMENT","UID":"SATISFIED-BY","TITLE":"Satisfied by","STATEMENT":"Satisfied by","RELATIONS":[{"TYPE":"Parent","ROLE":"Satisfied by","VALUE":"TARGET"}]}
            ]}]}
            """);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["StrictDoc:JsonFilePath"] = jsonPath })
            .Build();
        var service = new StrictDocService(cache, NullLogger<StrictDocService>.Instance, configuration);

        var requirements = await service.GetAllRequirementsAsync(BaseUrl);
        var target = requirements.Single(requirement => requirement.Identifier == "TARGET");

        await Assert.That(Relation(requirements, "DECOMPOSES", requirement => requirement.Decomposes)).Contains(Uri("TARGET"));
        await Assert.That(target.DecomposedBy).Contains(Uri("DECOMPOSES"));
        await Assert.That(Relation(requirements, "ELABORATES", requirement => requirement.Elaborates)).Contains(Uri("TARGET"));
        await Assert.That(target.ElaboratedBy).Contains(Uri("ELABORATES"));
        await Assert.That(Relation(requirements, "SPECIFIES", requirement => requirement.Specifies)).Contains(Uri("TARGET"));
        await Assert.That(target.SpecifiedBy).Contains(Uri("SPECIFIES"));
        await Assert.That(Relation(requirements, "CONSTRAINS", requirement => requirement.Constrains)).Contains(Uri("TARGET"));
        await Assert.That(target.ConstrainedBy).Contains(Uri("CONSTRAINS"));
        await Assert.That(Relation(requirements, "SATISFIES", requirement => requirement.Satisfies)).Contains(Uri("TARGET"));
        await Assert.That(target.SatisfiedBy).Contains(Uri("SATISFIES"));
        await Assert.That(Relation(requirements, "DECOMPOSED-BY", requirement => requirement.DecomposedBy)).Contains(Uri("TARGET"));
        await Assert.That(target.Decomposes).Contains(Uri("DECOMPOSED-BY"));
        await Assert.That(Relation(requirements, "ELABORATED-BY", requirement => requirement.ElaboratedBy)).Contains(Uri("TARGET"));
        await Assert.That(target.Elaborates).Contains(Uri("ELABORATED-BY"));
        await Assert.That(Relation(requirements, "SPECIFIED-BY", requirement => requirement.SpecifiedBy)).Contains(Uri("TARGET"));
        await Assert.That(target.Specifies).Contains(Uri("SPECIFIED-BY"));
        await Assert.That(Relation(requirements, "CONSTRAINED-BY", requirement => requirement.ConstrainedBy)).Contains(Uri("TARGET"));
        await Assert.That(target.Constrains).Contains(Uri("CONSTRAINED-BY"));
        await Assert.That(Relation(requirements, "SATISFIED-BY", requirement => requirement.SatisfiedBy)).Contains(Uri("TARGET"));
        await Assert.That(target.Satisfies).Contains(Uri("SATISFIED-BY"));
    }

    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(_dataDirectory))
        {
            Directory.Delete(_dataDirectory, recursive: true);
        }

        return ValueTask.CompletedTask;
    }

    private static HashSet<Uri> Relation(IEnumerable<Requirement> requirements, string uid,
        Func<Requirement, HashSet<Uri>> selector) => selector(requirements.Single(requirement => requirement.Identifier == uid));

    private static Uri Uri(string uid) => new($"{BaseUrl}/?a={uid}");
}
