# Extended OSLC Configuration Management design

This document contains the work that should follow the first StrictDoc
configuration-management probe. The initial milestone is deliberately smaller:
it exposes generic `oslc_config:Configuration` resources, resolves a selected
configuration to `/data/{branch}/{tag}/`, and tests the limited Jazz GCM flow
that heliple2@CM already achieved. The initial milestone is described in
[oslc-config-initial.md](./oslc-config-initial.md).

This document covers the later subtype graph and the integration surfaces that
should not be implemented speculatively: `Stream`, `Baseline`, `Selections`,
`VersionResource`, TRS, LDX, and LQE interoperability.

The implementation target is still a local configuration provider. StrictDoc
does not become a global-configuration server merely by exposing local
streams, baselines, or an `acceptedBy` relationship.

## Evidence and reference material

The design combines four sources of evidence:

- the local OSLC Configuration Management specification under
  `/Users/ezandbe/code/a/oslc/oslc-op/oslc-specs/specs/config/`;
- the OSLC TRS specification under
  `/Users/ezandbe/code/a/oslc/oslc-op/oslc-specs/specs/trs/`;
- the checked-out heliple2 `CM` branch under
  `/Users/ezandbe/code/a/phd/heliple2/`; and
- the attached OSLC4Net domain package under
  `/Users/ezandbe/code/a/oslc/oslc4net/OSLC4Net_SDK/OSLC4Net.Domains.ConfigurationManagement/`.

The two SDK RDF resources were parsed successfully with Apache Jena:

```text
Resources/vocab.nt
Resources/shapes.nt
```

The vocabulary query confirms that `Stream` and `Baseline` are subclasses of
`oslc_config:Configuration`. The generated shape resources confirm that the
SDK has separate shapes for `Component`, `Stream`, `Baseline`, `Selections`,
and `VersionResource`.

The attached package is an implementation aid, not a complete server. Its
`bin/` and `obj/` outputs should not be treated as design input.

## What the attached OSLC4Net package tells us

### Domain declarations and generated records

`ConfigurationManagementDomain.cs` declares the Config vocabulary and partial
records for:

```text
Activity
Baseline
ChangeSet
Component
Contribution
ChangeSetSelections
Selections
Stream
VersionResource
```

The concrete generated shape evidence is under
`Generated/OSLC4Net.CodeGen/OSLC4Net.CodeGen.OslcSourceGenerator.OslcDomainGenerator/`,
notably:

```text
OSLC4Net.Domains.ConfigurationManagement.Component.Shape.g.cs
OSLC4Net.Domains.ConfigurationManagement.Stream.Shape.g.cs
OSLC4Net.Domains.ConfigurationManagement.Baseline.Shape.g.cs
OSLC4Net.Domains.ConfigurationManagement.Selections.Shape.g.cs
OSLC4Net.Domains.ConfigurationManagement.VersionResource.Shape.g.cs
```

There is no `Configuration` record declaration in that file, and there is no
generated `Configuration.Shape.g.cs` in `Generated/`. This is not a statement
that the OSLC vocabulary lacks `oslc_config:Configuration`; the vocabulary
defines it as the superclass of the concrete configuration types. It means the
first StrictDoc generic resource needs a small server-owned model or a generic
resource representation rather than assuming that code generation has already
provided a generic model.

The generated files are valuable for the later subtype phase because they make
the resource-shape constraints visible in normal C# metadata. In particular:

| Generated type | Relevant generated properties | Later server obligation |
|---|---|---|
| `Component` | exactly one `configurations` LDP container | publish one stable container containing both streams and baselines |
| `Stream` | `component`, `branch`, `baselines`, `selections`, `previousBaseline`, `acceptedBy`, `accepts`, `contribution` | publish mutable configuration metadata and read-only container references |
| `Baseline` | `component`, exactly one `baselineOfStream`, `streams`, `selections`, `previousBaseline`, `acceptedBy`, `contribution` | publish an immutable snapshot and stable derived-stream container |
| `Selections` | `selects` | select VersionResource URIs, not configurations or components |
| `VersionResource` | exactly one `dcterms:isVersionOf`, `component`, `committed`, `versionId` and common metadata | expose an immutable version identity independent of a later context request |

The SDK source generator is configured by
`OSLC4Net.Domains.ConfigurationManagement.csproj` to consume
`Resources/vocab.nt` and `Resources/shapes.nt`. The generated records therefore
describe serialization and shape metadata; they are not a persistence layer
and do not decide how `/data/{branch}/{tag}/` is resolved.

### One handler, polymorphic representations

`Stream` and `Baseline` being subclasses of `Configuration` does not require
separate controller families. A single configuration resource handler can:

1. resolve a provider-owned configuration URI;
2. load its descriptor;
3. select a representation strategy based on the descriptor kind; and
4. serialize either the generic first-milestone form or the later subtype form.

Separate endpoints are useful for the containers and delegated UI, but not
because the RDF classes require separate HTTP implementations. The later
implementation should keep one registry and one context resolver while making
the subtype serializer explicit and testable.

## The staged model

The implementation must have a hard observation point between generic Config
and subtype Config:

| Stage | Wire representation | Internal mapping | Purpose |
|---|---|---|---|
| A. Generic probe | `rdf:type oslc_config:Configuration` only | URI maps to `{branch}/{tag}` | establish discovery, picker, context propagation, and limited Jazz GCM behavior |
| B. Subtype graph | `Stream`/`Baseline` plus inherited Configuration semantics | same registry, with immutable snapshot metadata | satisfy subtype shapes and allow Jazz to distinguish mutable and frozen configurations |
| C. Version graph | `Selections` selecting `VersionResource` nodes | paired JSON/sidecar snapshot gets a stable revision | make version identity, TRS, and context-aware link resolution durable |
| D. Indexed integration | TRS publication and LDX consumption | owner-side links are indexed by context | enable complete cross-application link and LQE behavior |

The stage-A server may know internally that `HEAD` is mutable and another tag
is immutable. It must enforce that write boundary even while returning generic
Configuration RDF. It must not claim that this is already a conforming Stream
and Baseline graph. The Jazz assessment decides whether stage B should be
implemented immediately or whether a compatibility adjustment is needed first.

## Later subtype graph

The intended provider-owned URI scheme remains:

```text
Component       {base}/oslc_config/components/{componentId}
Configurations  {componentUri}/configurations
Configuration   {base}/oslc_config/configurations/{configurationId}
Selections      {base}/oslc_config/selections/{selectionId}
VersionResource {base}/oslc_config/versions/{uid}/{snapshotRevision}
```

The configuration container is the important membership boundary:

```text
Component
  └── oslc_config:configurations -> LDP container
        ├── Stream   -> /data/{branch}/HEAD/      (mutable)
        └── Baseline -> /data/{branch}/{tag}/      (immutable)
```

The provider must not use the component’s concept-resource container as a
configuration container. The Config shape explicitly describes
`oslc_config:configurations` as the container for both streams and baselines.

### Stream and Baseline

Both resources are `Configuration` subclasses. The serializer should emit at
least:

```text
Stream:
  rdf:type              oslc_config:Stream
  oslc_config:component componentUri
  oslc_config:branch    branchUri
  oslc_config:baselines baselinesContainerUri
  oslc_config:selections selectionsUri

Baseline:
  rdf:type                    oslc_config:Baseline
  oslc_config:component       componentUri
  oslc_config:baselineOfStream streamUri
  oslc_config:selections      selectionsUri
  oslc_config:streams         derivedStreamsContainerUri
```

`baselineOfStream` is a single, read-only relation. `previousBaseline` is a
history relation and must come from publication metadata, not from sorting
directory names. The stream and baseline URI must remain stable when the
underlying export is re-read.

The `{tag}` convention is an internal publication rule:

```text
HEAD  -> Stream
other -> Baseline
```

It is acceptable for the initial probe to enforce this rule without exposing
the types. The subtype phase must expose the same rule in RDF and in the
resource shapes, previews, `Allow` headers, and mutation responses.

### Selections and VersionResource

`Selections` is not a list of configurations. Every non-unbound selection must
select a version resource. The later StrictDoc graph should therefore be:

```text
Stream/Baseline
  └── oslc_config:selections -> Selections
        └── oslc_config:selects -> VersionResource
                                      └── dcterms:isVersionOf -> requirement URI
```

The generated selection-shape description is explicit that selected resources
must not be of type `Configuration`; sub-configurations belong in
`oslc_config:contribution`. This prevents a tempting but invalid shortcut in
which the stream simply selects every requirement and recursively selects its
own configuration.

The version URI must identify the exact paired snapshot used for the response:

```text
{base}/oslc_config/versions/{requirementUid}/{snapshotRevision}
```

The representation at that URI must remain available after a later stream
publication. A concept GET with a context selects a version. A direct GET of a
VersionResource returns that exact version and ignores a supplied configuration
context.

The snapshot revision must cover both `strictdoc.json` and `sidecar.json`.
Otherwise a link-only stream PUT could change the apparent version without
changing the requirement export, or a baseline could resolve a sidecar value
from the wrong snapshot.

## Heliple2@CM as the first compatibility target

The checked-out heliple2 `CM` branch did participate in Jazz GC in a limited
capacity. That is important positive evidence, not a reason to wait for a
complete Config graph before testing StrictDoc.

The working path in heliple2 is:

```text
rootservices
  -> oslc_config:cmServiceProviders/catalog pointer
  -> Config provider 6
  -> /configurations/query and /configurations/selector
  -> generic Configuration resource
  -> context-bearing RM requests
```

The branch also adds `oslc:publisher`, registers Config resource shapes, and
sets Jazz’s `globalConfigurationAware=yes` metadata on the Config and RM
providers. Its Config query/picker is sufficient for the limited GC flow that
was observed, even though the branch has no active subtype-aware Stream or
Baseline endpoints and no TRS/LDX service.

The StrictDoc stage-A acceptance probe should reproduce the useful part of
that chain:

- rootservices and SCR discovery;
- a configuration catalog and provider;
- generic Configuration query and delegated selection;
- an `acceptedBy` relationship if the Jazz test requires it;
- context propagation to RM query, GET, preview, and links; and
- link-only PUT against the mutable `HEAD` snapshot.

The heliple2-specific details remain in
[oslc-config-initial.md](./oslc-config-initial.md) and
[jazz-config.md](./jazz-config.md). The important translation is not to copy
heliple2’s context OID in requirement URIs or its non-standard search-term
syntax. StrictDoc should use `Configuration-Context` and
`oslc_config.context`, with the file-backed descriptor as the context object.

## Subtype implementation plan after the Jazz assessment

The assessment must be a real black-box test against the target Jazz GCM flow,
not an RDF snapshot comparison alone. Run it after stage A and record the
requests and responses as fixtures.

### Assessment questions

1. Does Jazz discover and list generic Configuration resources through the
   provider query capability?
2. Does the configuration picker accept the returned generic URIs?
3. Does Jazz send `Configuration-Context` on requirement GET, query, preview,
   and link operations?
4. Does Jazz require the response to advertise `Stream` and `Baseline` types,
   or does the generic limited flow continue to work?
5. Does Jazz require `configurations`, `baselines`, `streams`, and `selections`
   containers before showing a local configuration as a usable contribution?
6. Does Jazz follow VersionResource links, or are concept representations
   sufficient for the selected scenario?
7. Which writes are attempted on a selected local configuration, and are they
   link-only writes that can remain in the sidecar?

### Subtype work, if the assessment requires it

Implement in this order:

1. Add a typed configuration descriptor with `Kind`, `Branch`, `Tag`,
   `ComponentUri`, `SnapshotRevision`, and linked container URIs.
2. Extend the shared configuration handler to emit `Stream` and `Baseline`
   types while retaining the same configuration URI registry.
3. Add the component configurations LDPC and the stream baselines and baseline
   streams LDPCs. Return stable `rdfs:member` values and LDP headers.
4. Add one stable `Selections` resource per published configuration and make
   the baseline selections immutable.
5. Add deterministic VersionResource URIs and the direct version GET/HEAD
   behavior.
6. Validate each response against the local Config shapes and then repeat the
   Jazz context-switching and picker tests.

Do not add POST/branch creation/change-set delivery in this phase. The existing
file-backed publication workflow can produce baseline directories; it does not
yet provide the transaction semantics needed to accept arbitrary configuration
RDF or stream selection mutations.

## TRS: publication of configuration and version changes

TRS belongs here because it is an integration consistency mechanism, not a
sidecar format. The normative local references are:

- `specs/trs/tracked-resource-set.html`;
- `specs/trs/trs-vocab.ttl`; and
- `specs/trs/trs-shapes.ttl`.

### Discovery and resource set shape

Once implemented, expose a provider-owned feed and publish its location using
the discovery mechanism required by the target Jazz/LQE deployment. The feed
must return a `trs:TrackedResourceSet` with:

```text
trs:base      -> a Base resource
trs:changeLog -> a ChangeLog resource
```

The first base page must identify a `trs:cutoffEvent`. The change log needs
stable `trs:order` values, `trs:changed` resource URIs, and pagination that
preserves the ordering across concurrent publications.

The initial feed should be scoped to one StrictDoc provider/component rather
than mixing unrelated applications. A later deployment may expose multiple
component feeds if the target LQE installation benefits from that partitioning.

### What the base and change log contain

The base must enumerate the resources that an indexer needs to bootstrap the
provider, including:

- the Config service provider, query capability, dialogs, and resource shapes;
- the Component and configuration containers;
- current Stream and Baseline resources after the subtype phase;
- Selections resources;
- VersionResource URIs for selected requirements; and
- requirement resources or version resources required by the chosen TRS
  profile.

The most important Config rule is that TRS entries for versioned resources use
VersionResource URIs, not concept URIs. A concept URI in a TRS feed cannot tell
LQE or LDX which selected version was indexed.

Publication events must be generated from the same atomic publication that
updates the paired JSON and sidecar:

| Local event | TRS event | Resource URI |
|---|---|---|
| first publication of a component/configuration/version | `trs:Creation` | created provider-owned URI |
| new stream export or accepted stream link-only PUT | `trs:Modification` | affected configuration and VersionResource |
| baseline/archive removal, if ever supported | `trs:Deletion` | removed resource URI |

An accepted link-only PUT is a modification of the selected resource state. It
must not mutate an old baseline or emit an event for a concept representation
that claims to be immutable. If the sidecar remains compatibility-only, the
event must still accurately describe the representation that a consumer will
receive.

### TRS and caches

The publication service must assign the snapshot revision before it emits the
change event. The event, ETag, VersionResource URI, and context resolver must
all agree on that revision. A consumer must never observe an event for a
half-written JSON/sidecar pair.

Tests must cover:

- a consumer starting from the base and then following the change log;
- a baseline remaining unchanged after a stream event;
- pagination across several stream publications;
- retrying the same event without creating duplicate state; and
- a resource GET using the final ETag named by a modification event.

## LDX: link index discovery and consumption

StrictDoc is not an LDX server in the initial design. The first LDX feature is
an outbound client that discovers incoming links owned by other providers. A
future StrictDoc-owned link publisher may expose a LinkProvider only when it
has a tested endpoint and TRS-backed index contribution path.

### Discovery from an external SCR

The client should:

1. fetch the external application’s SCR and rootservices;
2. locate its `LinkProvider` and endpoint using the SCR vocabulary used by the
   Jazz release under test;
3. resolve the provider’s authentication/friend relationship;
4. map the requested secondary predicate to the owner-side primary predicate;
5. query the link index for the target resource and active global/local
   configuration context; and
6. merge the discovered values into the target-side representation without
   writing a backlink to the StrictDoc sidecar.

Jazz examples use a JSON query containing `targetURLs`, `linkTypes`, and a
global configuration URL (`gcURL`). Treat that as a compatibility shape to be
verified against the deployed endpoint, not as a universal LDX API contract.

### Direction and ownership

The owner-side predicate mapping is maintained in
[link-ownership.md](./link-ownership.md). For example:

```text
CM --oslc_cm:implementsRequirement--> StrictDoc RM requirement
RM requirement --oslc_rm:implementedBy--> CM resource
```

The second value is an incoming view. The StrictDoc renderer may show it, but
the sidecar must not become a second authoritative store merely because Jazz
initiated the link from the other application.

The LDX query must carry the same configuration context as the requirement
request. A link can be present in a global index while being absent from the
selected local baseline because the target version is not selected there.

### Jazz capability flags

Do not set these flags merely because an RDF property or a sidecar exists:

```text
jfs_proc:supportContributionsToLinkIndexProvider
jfs_proc:supportLinkDiscoveryViaLinkIndexProvider
```

Set the first only after StrictDoc can publish owner-side changes through TRS
and the target LDX installation indexes them. Set the second only after the
StrictDoc client can discover an external owner’s incoming link in the correct
configuration context. The limited heliple2@CM success did not require these
flags; it therefore does not prove either flag is safe to advertise.

## LQE: indexed query and reporting concerns

LQE is related to TRS and LDX but is not interchangeable with either:

```text
TRS -> publishes resource/change history
LDX -> resolves configuration-aware incoming links
LQE -> indexes provider data for query and Report Builder
```

The Jazz material collected for this design, including the configuration-aware
CLM integration article and the LQE/Report Builder integration guidance, makes
the practical risk clear: a provider can be discoverable and readable in a
picker while still being unusable as an indexed reporting source if its
identity, shapes, versions, or change feed are unstable. The local evidence is
listed in [jazz-config.md](./jazz-config.md); the LQE article used during the
investigation is [Integrating external data sources with LQE and Report
Builder](https://jazz.net/library/article/91450).

### LQE requirements for StrictDoc

The LQE integration plan must cover:

1. **Stable discovery.** LQE must reach the same service provider, domain,
   query capability, shapes, and TRS feed from the public base URL after a
   reverse proxy is introduced.
2. **Stable identities.** Concept URIs, configuration URIs, and
   VersionResource URIs must not depend on request query order, Host headers,
   or an ephemeral cache key.
3. **Version semantics.** A VersionResource must remain fetchable after the
   current stream advances. A concept URI alone is insufficient for historical
   reports.
4. **Shape-complete data.** The provider must publish the RM shape and the
   Config shapes used by its resources. Generated SDK shapes are a useful local
   cross-check, but the server must return the actual shape links.
5. **Context-aware links.** Link values indexed from a stream must not be
   reported as if they were present in every baseline. TRS events and LDX
   queries must use the same context/version mapping.
6. **Deletion and archival.** If a baseline or requirement is archived, the
   event policy must say whether LQE retains it as historical data or removes it
   from the current index.
7. **Authentication.** The LQE registration, TRS polling, resource GETs, and
   any LDX calls must use the same tested Jazz friend/authentication setup.

The Jazz LQE guidance adds two metadata decisions that the generic OSLC
Config specification does not settle for us:

- publish the process resources that LQE/Report Builder use to scope and
  authorize data, including a service provider and either a real or simulated
  project area/access context; and
- make the configuration support value explicit, using the Jazz
  `jrs:projectConfigSupport` property where the deployment expects it. `yes`
  exposes the provider to configuration-scoped reporting, while `compatible`
  is the transition value when the provider can participate in either
  reporting mode. If StrictDoc
  does not model project areas, choose explicitly between one application-wide
  `acc:AccessContext`, feed-level permissions, or a synthetic project area;
  do not leave the choice to an accidental absence of metadata.

For versioned resources, publish `acc:accessContext` on the version graph and
keep the `Selections` resource aligned with the version-resource URI. Where a
project-area model is used, publish `process:projectArea` consistently on the
resources that belong to it. The LQE
article also recommends TRS feeds for most external data sources rather than a
static vocabulary alone, because TRS publishes both metadata and instance
resources as they change. These decisions are derived from
[Integrating external data sources with LQE and Report Builder](https://jazz.net/library/article/91450),
not from the generic Config vocabulary.

### LQE acceptance tests

The extended integration test should:

- register the StrictDoc provider in a test LQE/LQE-like consumer;
- bootstrap from the TRS base and verify the Config and RM shapes;
- index a stream requirement and its VersionResource;
- publish a baseline and verify that the old VersionResource remains stable;
- accept a stream link-only PUT and verify one ordered modification event;
- query the same requirement under two configurations and verify different
  selected versions or link sets where the snapshots differ; and
- run a Report Builder-style query that uses the provider’s published shape and
  does not depend on an implementation-private path.

Do not make LQE success a prerequisite for the generic stage-A Jazz probe. Make
it a gate for claiming the extended, indexed integration level.

## Extended REST surface

These endpoints belong to the subtype/TRS/LDX/LQE phase, not the first generic
Configuration probe:

| Endpoint | Methods | Purpose |
|---|---|---|
| `{component}/configurations` | `GET`, `HEAD`, `OPTIONS` | enumerate Stream and Baseline members |
| `{stream}/baselines` | `GET`, `HEAD`, `OPTIONS` | enumerate immutable baselines of a stream |
| `{baseline}/streams` | `GET`, `HEAD`, `OPTIONS` | enumerate streams derived from a baseline |
| configuration resource | `GET`, `HEAD`, `OPTIONS` | subtype RDF, shapes, containers, and metadata |
| selections resource | `GET`, `HEAD`, `OPTIONS` | VersionResource selections |
| VersionResource | `GET`, `HEAD` | exact immutable requirement version |
| TRS resource | `GET` | base, change log, cutoff, events, and pagination |
| external LinkProvider | outbound query | context-aware incoming-link discovery |

POST, configuration metadata PUT, branch creation, and change-set delivery
remain separate lifecycle work. They should not be smuggled into the subtype
read graph.

## Extended completion criteria

The extended implementation is complete only when:

- Stream and Baseline resources are emitted as `Configuration` subclasses and
  satisfy their local shapes;
- the component/configuration and stream/baseline containers are stable and
  correctly typed;
- Selections select VersionResource URIs and never configuration URIs;
- direct VersionResource reads are immutable and context-independent;
- paired JSON/sidecar publication, ETags, VersionResource URIs, and TRS events
  share one revision model;
- a test LDX can index a StrictDoc-owned change and StrictDoc can consume an
  external incoming link without materializing a backlink; and
- a test LQE can bootstrap, update, and query the provider without relying on
  private paths or unstable concept/version identities.

Only then should the provider advertise the full Jazz link-index flags and
claim configuration-aware indexed integration. Limited Jazz GCM participation
through the generic stage remains a valid earlier milestone.
