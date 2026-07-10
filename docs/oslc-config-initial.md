# OSLC Configuration Management and Jazz GCM implementation plan

This is the implementation plan for making the StrictDoc RM server a
configuration-management-aware OSLC provider and a usable participant in Jazz
Global Configuration Management (GCM).

The implementation is intentionally staged. The first milestone is a generic
`oslc_config:Configuration` provider with the file-backed context mapping and
the link-only stream write boundary. We will use that implementation to assess
Jazz behaviour before committing to the subtype graph. `Stream` and `Baseline`
are subclasses of `Configuration`, so this sequencing is a wire-level probe,
not a decision to create unrelated resource models.

The implementation target for this document is therefore:

```text
Jazz rootservices/SCR discovery
        -> configuration catalog and service provider
        -> generic Configuration query and picker
        -> context-aware RM resolution
        -> link-only PUT on the internal HEAD context
        -> Jazz behaviour assessment
        -> subtype graph and VersionResources, if required
```

The related documents are:

- [Link direction and ownership](./link-ownership.md), the extracted Linking
  Profiles policy for primary and secondary predicates.
- [Jazz configuration-management notes](./jazz-config.md), the product-level
  evidence behind the Jazz-specific discovery and link-index requirements.
- [Extended OSLC Config design](./oslc-config-extended.md), the later subtype,
  TRS, LDX, and LQE design and acceptance criteria.
- [Link sidecar storage](./LINK_SIDECAR.md), the current unscoped sidecar
  behavior that this design scopes by configuration.
- [Jazz interop: writable RM link properties](./JAZZ_INTEROP.md), the existing
  link-save observations, including the response behavior that must remain a
  regression test.
- [Configuration management proposal](./CONFIGURATION_MANAGEMENT_PROPOSAL.md),
  an earlier proposal whose SQLite recommendation is not required for the
  first file-backed implementation.

The normative baseline is the local OSLC Configuration Management
specification in
`/Users/ezandbe/code/a/oslc/oslc-op/oslc-specs/specs/config/`, especially:

- `config-resources.html` for discovery, context propagation, operations, and
  delegated UI;
- `versioned-resources.html` for VersionResource behavior;
- `config-vocab.ttl` for the vocabulary; and
- `config-shapes.ttl` for the Component, Stream, Baseline, Selections,
  Contribution, and VersionResource constraints.

The OSLC Linking Profiles note and the fetched Jazz material tighten the
practical interoperability target. They are not replacements for the OASIS
specification.

## Executive decisions

### File layout and context mapping

The published data layout is:

```text
/data/{branch}/{tag}/strictdoc.json
/data/{branch}/{tag}/sidecar.json
```

The internal mapping is intentionally simple:

```text
tag == HEAD  -> mutable publication context
tag != HEAD  -> immutable publication context
```

Examples:

```text
/data/main/HEAD/strictdoc.json       # mutable stream: main
/data/main/HEAD/sidecar.json
/data/main/v1.2.0/strictdoc.json     # immutable baseline of main
/data/main/v1.2.0/sidecar.json
/data/feature-x/HEAD/strictdoc.json  # mutable stream: feature-x
```

`{branch}` is intended to become a Stream identity, not a component identity,
but the first generic probe need not emit `oslc_config:Stream` yet. The first
deployment should expose one StrictDoc component per configured StrictDoc
document/dataset. If the deployment has several independently versioned
documents, each document should become a separate component; that must not
happen accidentally merely because files share a parent directory.

The JSON export is authoritative for requirements and structure. The sidecar
is the only writable persistence layer for the explicitly accepted link
properties. A request resolved to `HEAD` can replace the sidecar entry for one
requirement, but cannot edit StrictDoc titles, descriptions, identifiers,
hierarchy, or other exported fields. A non-`HEAD` tag is internally immutable
from the first milestone, even though the response may still use the generic
Configuration type. The subtype phase will expose these two behaviours as
Stream and Baseline RDF.

### Three integration levels

These levels keep the implementation and the advertised Jazz metadata honest:

| Level | What it provides | Jazz metadata | Status |
|---|---|---|---|
| Generic Configuration probe | Config discovery, generic Configuration query/picker, context-aware RM, and link-only `HEAD` writes | `globalConfigurationAware=yes` only if the Jazz probe confirms the limited heliple2-style flow | First implementation |
| Local subtype provider | Component, Stream, Baseline, Selections, VersionResources, containers, and immutable snapshots | Set only after subtype behaviour is verified | Follow-up implementation |
| Indexed configuration linking | Owner-side TRS publication plus incoming-link discovery through LDX and LQE-compatible indexing | Link-index flags only after the corresponding services work | Extended design |

The server must not advertise full subtype or link-index conformance merely
because it can return a generic `Configuration` RDF object. Jazz clients use
`globalConfigurationAware` to decide whether to offer linking and delegated
UI, and the value defaults to `no` when absent. The CM branch of heliple2 is a
useful counterexample to an overly strict sequencing rule: it achieved limited
Jazz GC participation with a generic Config service, picker, and discovery
metadata. That is the first behaviour to reproduce and measure.

### Local versus global configurations

StrictDoc is initially a **local configuration provider**, not a GCM provider.
It should expose local streams and baselines and allow GCM to contribute them.
It should not emit `globalConfigurationService` metadata or pretend to resolve
multi-component global configuration composition. A global configuration URI
can still be accepted as an external context once a resolver can delegate its
resolution to GCM or another provider.

## Findings from the current StrictDoc server

The existing server has the following relevant surface:

| Current code | Observed behavior | Consequence |
|---|---|---|
| `Controllers/RootServicesController.cs` | Serves only `/.well-known/oslc/rootservices.xml`; publishes RM catalog and OAuth data; no Config catalog, TRS, or link-provider pointer | Jazz discovery currently cannot reach a local configuration catalog through root services |
| `Controllers/ServiceContributionController.cs` | Serves only `/.well-known/oslc/scr`; emits a minimal Jazz `jd:Application` and RM provider entry | It is not the conventional `/scr` resource and has no configuration provider or link-index information |
| `Controllers/CatalogController.cs` | Publishes one RM catalog at `/oslc/catalog` | A separate configuration catalog/service must be added, or the catalog must explicitly advertise both domains |
| `Controllers/ServiceProviderController.cs` | Publishes RM query and requirement selection only | The provider has no `oslc:domain` Config service and no Jazz configuration capability properties |
| `Controllers/RequirementController.cs` | Reads one globally configured JSON export; direct GET and PUT use `/?a={uid}`; PUT already filters through the sidecar | This is the correct write boundary, but all load, link, query, preview, and cache operations need a context argument |
| `Services/StrictDocService.cs` | Loads and caches documents/requirements without a context key | Cache entries can leak a stream or baseline into another context |
| `Services/FileLinkSidecarService.cs` | Uses one configured sidecar store keyed by resource URI and an allowlist of five predicates | The store must become context-scoped and its policy must be made explicit |
| `Program.cs` | CORS is broad; in-process toy OAuth1 remains; no OIDC/OAuth2 or friend-management integration | Basic delegated UI may work locally, but this is not yet a complete Jazz linking-profile security surface |

The existing root services and SCR controllers should be retained as the
starting point for serialization, but their routes and RDF graphs need a
deliberate compatibility pass. The root services document is Jazz-specific;
it is not interchangeable with an OSLC ServiceProviderCatalog.

## Deep heliple2 inspection: the `CM` branch

The relevant heliple2 source is the `CM` branch, now checked out at
`/Users/ezandbe/code/a/phd/heliple2` (`HEAD 99dd2a89`, tracking
`origin/CM`). This branch changes the conclusion materially: it contains the
actual generated Config service and the Jazz-specific discovery additions.
It is still a partial implementation, but it is the correct source baseline
for the StrictDoc plan.

### What the `CM` branch actually registers

The active registration is in:

`/Users/ezandbe/code/a/phd/heliple2/ShareAspaceAdaptor/src/main/java/com/eurostep/shareaspace/oslcserver/servlet/Application.java`

The branch registers:

- `ServiceProvider6Service1`, the active Config service under `@OslcService`
  for `http://open-services.net/ns/config#`;
- `ServiceProvider6Service`, the catalog-side service-provider endpoint;
- `ConfigurationService` and `ComponentService` for direct resource GET,
  compact, and preview;
- `RootServicesService`, `ScrService`, and `PublisherService`;
- Config model shapes for Configuration, Component, Contribution, Selections,
  and VersionResource; and
- six generated ServiceProvider repositories/factories, with provider 6
  dedicated to the Config service.

The branch does **not** register a Stream service. The old `StreamService.java`
was renamed to `PartService.java` as part of the CM branch’s broader domain
regeneration. There is no Baseline service, Selections resource service, or
VersionResource resource service. The shape map still omits Stream and
Baseline. This is the most important distinction between “Config support is
present” and “the provider has a complete OSLC Config graph.”

The branch also changes the application from `javax` to `jakarta`, regenerates
the service-provider IDs, and separates the providers into six generated
service-provider classes. StrictDoc should copy the behavior, not the Java
namespace or generated naming scheme.

### Root services delta in the `CM` branch

`services/RootServicesService.java` now adds two important manual changes:

```xml
xmlns:oslc="http://open-services.net/ns/core#"
xmlns:oslc_config="http://open-services.net/ns/config#"
<oslc:publisher rdf:resource="{servlet}/application-about" />
<oslc_config:cmServiceProviders rdf:resource="{catalog}" />
```

It retains the generated entries:

```xml
<oslc_am:amServiceProviders rdf:resource="{catalog}" />
<oslc_rm:rmServiceProviders rdf:resource="{catalog}" />
<oslc_cm:cmServiceProviders rdf:resource="{catalog}" />
```

This is the root-services delta that the earlier audit missed. It proves that
the Jazz integration tested by heliple2 expects a Config discovery pointer
and an `oslc:publisher` resource, in addition to the ordinary RM catalog.

It also shows a likely interoperability hazard: the same catalog URI is
published for AM, RM, historical CM, and the Config pointer. The branch does
not create a separate Config catalog or add a TRS declaration. StrictDoc
should initially use the same discovery chain only if its catalog visibly
contains the Config provider and the provider’s `oslc:domain` is correct. A
separate `/oslc_config/catalog` is safer for StrictDoc because it prevents a
Jazz client from receiving a mixed catalog whose root-services predicate and
members disagree.

The root-services implementation continues to expose only GET, RDF/XML/XML,
OAuth URLs, and `X-OSLC-Version: 2.0`. It does not advertise a global
configuration service, a TRS feed, or an LDX endpoint. Therefore the CM branch
implements Config discovery, not full GCM link discovery.

### SCR and publisher resources in the `CM` branch

The branch adds:

`/Users/ezandbe/code/a/phd/heliple2/ShareAspaceAdaptor/src/main/java/com/eurostep/shareaspace/oslcserver/services/ScrService.java`

at `GET /scr`, and:

`/Users/ezandbe/code/a/phd/heliple2/ShareAspaceAdaptor/src/main/java/com/eurostep/shareaspace/oslcserver/services/PublisherService.java`

at `GET /application-about`.

The SCR is deliberately minimal:

```xml
<jazz_discovery10:Application>
  <jazz_discovery10:rootServices rdf:resource=".../rootservices" />
  <oslc:publisher rdf:resource=".../application-about" />
  <jazz_discovery10:contextRoot rdf:datatype="xsd:string">...</jazz_discovery10:contextRoot>
</jazz_discovery10:Application>
```

It contains no Config or RM domain declarations, no catalog/service-provider
sections, and no `LinkProvider`. The publisher describes the application and
version but is not itself the Config catalog. This is exactly why StrictDoc
needs a real SCR acceptance test: the presence of `/scr` alone does not tell
Jazz that Config resources exist.

The branch’s SCR is also the evidence for the route decision in this document:
use conventional `{base}/scr`, not only `/.well-known/oslc/scr`. StrictDoc can
retain the current well-known route as a compatibility redirect, but `/scr`
must be the canonical Jazz resource.

### Active Config service: query and delegated picker

The active Config provider is:

`/Users/ezandbe/code/a/phd/heliple2/ShareAspaceAdaptor/src/main/java/com/eurostep/shareaspace/oslcserver/services/ServiceProvider6Service1.java`

Its live route is:

```text
GET /configurations/query
GET /configurations/selector?terms=...
```

It declares:

- `@OslcService(http://open-services.net/ns/config#)`;
- an `@OslcQueryCapability` for `oslc_config:Configuration`;
- RDF/XML, JSON-LD, Turtle, XML, and JSON query representations;
- an HTML query page; and
- an `@OslcDialog` for Configuration selection, 750x750, which converts
  `terms` to `oslc.searchTerms` and renders the generated selection dialog.

The query and picker are backed by the same `Configuration` delegate. This is
the implementation to follow for the StrictDoc configuration provider, with
one crucial change: StrictDoc’s query must enumerate a provider-owned
Component/Stream/Baseline graph rather than source-side ApplicabilityContext
objects.

The Config service’s injected delegate is typed as:

```text
Delegate<Configuration, ConfigurationId, ServiceProvider6Service1Id>
```

and `ApplicationBinder` binds it to
`SAS_ComponentConfigurationRepositoryFactory`. That factory maps:

```text
ServiceProvider6Service1Id -> ApplicabilityContextsServiceToConfiguration
ComponentId                -> ApplicabilityContextToComponent
ConfigurationId            -> ApplicabilityContextToConfiguration
```

It returns a JSON-backed `RestRepo`. This is a generated repository adapter,
not a persistence model for streams or baselines.

### Service-provider metadata and the Jazz flag

`ServiceProvider6Repo.java` creates one cached provider with:

```text
title       = "GC contribution (SAs)"
description = "OSLC Service Provider for the OSLC Global Configuration contribution from ShareAspace"
```

and adds:

```text
{http://jazz.net/xmlns/prod/jazz/process/1.0/}globalConfigurationAware = "yes"
```

`ServiceProvider2Repo.java` does the same for the RM Requirements provider.
This is a strong Jazz clue: the CM branch advertises both the RM provider and
the Config provider as global-configuration-aware, and names provider 6 as a
GCM contribution provider.

It does **not** set
`supportContributionsToLinkIndexProvider` or
`supportLinkDiscoveryViaLinkIndexProvider`. It does not publish TRS or query
LDX. Therefore StrictDoc should implement those flags only after its TRS/LDX
behavior exists. The initial `globalConfigurationAware` decision should be
based on the stage-A Jazz probe, using heliple2’s limited generic-Config
success as the compatibility baseline rather than waiting for the full
subtype graph.

### Configuration and component resource services

The CM branch retains the generated resource services:

| Source file | Active route | Live operations | Important limitation |
|---|---|---|---|
| `ConfigurationService.java` | `configurationService/Configuration/{id}` | GET RDF/XML, JSON-LD, Turtle, XML, JSON; HTML; compact; small/large preview | No explicit PUT/POST/DELETE/HEAD/OPTIONS; resource is generic `Configuration` |
| `ComponentService.java` | `componentService/Component/{id}` | GET RDF/XML, JSON-LD, Turtle, XML, JSON; HTML; compact; small/large preview | No component configurations LDPC endpoint in this generated service |
| `ServiceProvider6Service1.java` | `configurations/query`, `configurations/selector` | Query and delegated selection | No direct stream/baseline/selection/version endpoints |
| no active class | none | no Stream or Baseline resource route | StrictDoc must supply both |

The Config resource services are read-oriented and CORS-aware for HTML/preview
paths, but the CORS helper allows only `origin, content-type, accept,
authorization`; it does not allow `Configuration-Context`. StrictDoc must add
that header for context-aware previews and picker requests.

The branch’s Config preview templates are still useful: they display
`component`, `branch`, `acceptedBy`, `contribution`, `selections`, and
`previousBaseline` where available. The branch does not prove that each field
is populated by the backend. In particular, the `static/previews/stream/*`
templates survived the `StreamService.java` to `PartService.java` rename; they
are stale generated assets, not evidence of a live Stream endpoint.

### The source mapping remains partial

The CM branch improves the transformers by adding:

- an injected `SAS_COMPONENTCONFIGURATION_BASE_URL` source URI;
- `oslc_config:acceptedBy oslc_config:Configuration` in
  `ApplicabilityContextToConfiguration`; and
- source URL/header plumbing for the regenerated Jakarta/Lynxwork adapter.

`ApplicabilityContextToConfiguration.java` still maps a source Applicability
Context to one generic Configuration. It still sets metadata and component,
but not branch, Stream/Baseline type, selections, baselines, previousBaseline,
baselineOfStream, contribution, or VersionResource. Its comment still says
that the configuration and component are the same thing in this
implementation.

`ApplicabilityContextToComponent.java` still does not set the required
`oslc_config:configurations` LDPC. It retains the comment that the source
system’s relationship is opposite to OSLC’s read-only where-used direction.

`ApplicabilityContextsServiceToConfiguration.java` still returns a list of
generic Configurations with title, short title, identifier, and URI. It parses
the source query term with `SasHelpers.extractTermFromSearchTerms` and uses
the remaining part as a source search term. This is not a query over a local
configuration graph.

### The CM branch’s real context mechanism

This is the most important Java behavior to translate carefully.

`SasHelpers.java` defines:

```text
SAS_NO_CONTEXT = "99999999999999"
```

and parses `oslc.searchTerms` in the non-standard form:

```text
{search text} @ {ShareAspace applicability-context oid}
```

`ServiceProvider2Service1Id` exposes `oslc.searchTerms` for the RM query.
`SystemElementsServiceToRequirement.java` calls
`SasHelpers.extractContextFromSearchTerms`, then:

1. fetches the user’s default ShareAspace information filter;
2. replaces its `applicability` object with the selected context;
3. base64-encodes the JSON; and
4. sends it to the source API as `sas-informationfilter`.

For a query, if a context is present, the transformer performs an N+1
applicability check with `ShareAspaceClient.checkIfApplicable` and filters the
source results. It deliberately avoids `parallelStream()` for the header
construction because HK2/request injection fails on worker threads, although
it later uses a parallel stream for the explicit applicability checks after
copying the headers and authorization into `ShareAspaceClient`.

`SystemElementToRequirement.java` carries the context in the generated
requirement URI itself:

```text
WebService1/Requirement/{id}/{applicabilityContextOid}
```

On direct GET it derives the source `sas-informationfilter` from the path
context, sends the source request with that header, and constructs every
child, related-element, and realization link with the same context segment.
The requirement transformer maps source children to `oslc_rm:decomposedBy`,
related elements to `oslc_rm:elaboratedBy`, and realizations to
`oslc_rm:implementedBy`.

This behavior explains why the CM branch can make a context-specific RM
request work even without the standard Config header. It is not a substitute
for the OSLC Config protocol. For StrictDoc, the equivalent architecture is:

```text
standard Configuration-Context/query context
        -> ConfigurationContext descriptor
        -> strictdoc.json + sidecar snapshot
```

The descriptor must be passed explicitly through the loader and link builder;
do not encode a filesystem branch or context OID into the requirement URI.
The CM branch’s approach is nevertheless important evidence that context must
be applied not only to the top-level requirement but also to every linked
requirement and to source-side query filtering.

### CM branch URI and model peculiarities

`RequirementId.java` requires both `id` and `applicabilityContextOid`; its
`validate()` rejects a requirement URI without a context segment. The context
is therefore part of the resource identity in heliple2, whereas the OSLC
Config specification distinguishes a stable concept URI from a selected
VersionResource. StrictDoc must not copy this URI model; it should provide a
compatibility serializer only if Jazz demonstrably requires it.

`ConfigurationId.java` and `ComponentId.java` still accept only a non-empty
opaque path segment and construct generic resource URIs. They do not validate
kind, branch, baseline immutability, or version identity.

The CM branch imports the Lyo classes for Component, Configuration,
Contribution, Selections, ConceptResource, and VersionResource and registers
their shapes, but those imports are not live endpoints. The local Config
shapes remain the source of truth for read-only and cardinality constraints.

### CM branch example RDF graphs

The checked-out `help/Examples` directory remains useful evidence:

- `componentRM_ADC.xml` and `componentGC7.xml` include Component,
  configurations, title, identifier/shortId, service provider, and access
  context.
- `streamRMAirDataComputer.xml` includes Stream, component, baselines,
  selections, previousBaseline, acceptedBy, and provenance.
- `configGC14.xml` includes Stream, inline Contribution data, archived state,
  previousBaseline, component, acceptedBy, and provenance.
- `447rm.xml` and `448rm.xml` show context-bearing artifact subjects and
  reified link statements.

These examples show what a Jazz consumer may encounter, while the CM branch
source explains how context is actually propagated. StrictDoc should use the
standard context protocol and a complete local Config graph, not copy the
partial source-domain transformation.

## Reference-implementation false-positive check

The OSLC reference implementation under `/Users/ezandbe/code/a/oslc/refimpl/`
was used to separate ordinary RM obligations from configuration obligations.

`WebServiceBasic.java` shows the ordinary RM resource GET path. Its RM PUT
path uses an `If-Match` value and delegates replacement to
`RestDelegate.updateRequirement`. The reference RM server does not implement
the Config domain, local configurations, global configuration traversal, or
GCM discovery.

This rules out two false positives:

1. StrictDoc does not need to implement every generated heliple2 resource or
   every CM/QM/AM service merely because the model jar contains it.
2. StrictDoc does need the Config-specific surface once it claims to be a
   configuration provider: context-aware concept GET/HEAD/PUT, configuration
   discovery, component/configuration/selections resources, delegated config
   selection, and the Jazz discovery metadata described below.

The reference implementation also supports the useful concurrency rule: PUT
must be conditional when an ETag/`If-Match` contract is used. The StrictDoc
sidecar update must adopt that rule before multiple Jazz clients can write the
same stream.

## Public discovery contract

The public base URL is the value produced by `IBaseUrlService`, including the
deployment context path. Every URI below is relative to that base and must
remain valid behind a reverse proxy.

### Root services

Expose the same RDF/XML root-services graph at:

```text
GET {base}/rootservices
GET {base}/.well-known/oslc/rootservices.xml
```

`{base}/rootservices` is the conventional Jazz URL. The well-known URI is the
OSLC Core bootstrap URI. Both responses must have the same expanded RDF
properties and the same canonical `rdf:about`; do not generate one graph from
the configured URL and another from the incoming Host header.

The current `RootServicesController` should be changed to emit, in addition
to its existing RM and authentication entries:

```xml
<oslc_rm:rmServiceProviders rdf:resource="{base}/oslc/catalog" />
<cm:cmServiceProviders rdf:resource="{base}/oslc_config/catalog" />
<jd:oslcCatalogs>
  <oslc:ServiceProviderCatalog rdf:about="{base}/oslc/catalog">
    <oslc:domain rdf:resource="http://open-services.net/ns/rm#" />
  </oslc:ServiceProviderCatalog>
  <oslc:ServiceProviderCatalog rdf:about="{base}/oslc_config/catalog">
    <oslc:domain rdf:resource="http://open-services.net/ns/config#" />
  </oslc:ServiceProviderCatalog>
</jd:oslcCatalogs>
```

Here `cm:cmServiceProviders` is the historical Jazz root-services predicate.
The XML prefix is irrelevant; the expanded namespace URI must match the
predicate used by the target Jazz version. The fetched Jazz guidance writes
the example with an `oslc_config` prefix even though it is a root-services
discovery property. Add an interoperability fixture from the target Jazz
deployment and assert the expanded QName, not the prefix spelling.

When TRS publication is enabled, add the TRS discovery declaration required
by the target Jazz/LQE deployment, for example a `trs:trackedResourceSet`
reference to the StrictDoc feed. Do not add a fake link-provider endpoint.
StrictDoc is not itself an LDX server in this design. Preserve the existing
OAuth root-services properties until the deployment authentication is replaced
with the supported Jazz-compatible mechanism.

Root-services acceptance checks:

- unauthenticated GET returns RDF/XML, including when `Accept` is omitted;
- the `rdf:about` URI is the requested canonical root-services URI;
- RM and configuration catalog links resolve without URL path arithmetic;
- a Jazz client can follow the config catalog link without knowing the
  internal `/oslc_config` path in advance; and
- OAuth/friend/authentication properties remain internally consistent.

### Service Contribution Resource (SCR)

Expose a Jazz SCR at:

```text
GET {base}/scr
```

The current `/.well-known/oslc/scr` route is not the conventional Jazz SCR
location and is also a vendor-specific path below the OSLC well-known
namespace. The migration should make `/scr` canonical and either redirect or
temporarily serve the old route for existing clients. The RDF/XML graph must
contain at least:

```xml
<jd:Application>
  <jd:contextRoot>{base}</jd:contextRoot>
  <jd:rootServices rdf:resource="{base}/rootservices" />
  <jd:domain rdf:parseType="Resource">
    <dcterms:identifier>http://open-services.net/ns/rm#</dcterms:identifier>
  </jd:domain>
  <jd:domain rdf:parseType="Resource">
    <dcterms:identifier>http://open-services.net/ns/config#</dcterms:identifier>
  </jd:domain>
  <jd:jsaSsoEnabled>false</jd:jsaSsoEnabled>
</jd:Application>
```

It should link the RM and configuration catalogs in the SCR’s service-provider
sections using the exact SCR vocabulary/shape expected by the target Jazz
release. The current resource has only an RM domain and one RM catalog entry.

`LinkProvider` has a different role from the local sidecar. Jazz guidance says
that a client discovers the LDX/`ILinkIndexService` endpoint from the
`LinkProvider` property in the **other application’s** SCR. StrictDoc must
parse that property when consuming Jazz SCRs. StrictDoc should advertise a
local `LinkProvider` only after it actually exposes an LDX-compatible endpoint;
the sidecar alone is not such an endpoint.

SCR acceptance checks:

- `/scr` returns RDF/XML without authentication;
- the application context root and root-services URI are correct behind a
  reverse proxy;
- both RM and Config domains/catalogs are discoverable;
- no `LinkProvider` is advertised until its endpoint and query contract exist;
- the client can parse an external CLM SCR and find its LinkProvider endpoint;
  and
- the old route, if retained, returns the same expanded graph or a clear
  redirect to `/scr`.

### Service provider catalogs and provider documents

Add a configuration catalog at:

```text
GET {base}/oslc_config/catalog
```

It should list one configuration-aware service provider for each configured
StrictDoc document/component. The existing `/oslc/catalog` remains the RM
catalog. A provider may be represented in both catalogs if that is how the
deployed Jazz client expects to find the RM and Config services, but each
service must have the correct `oslc:domain`.

Each configuration provider must expose:

```text
oslc:domain = http://open-services.net/ns/config#
```

and a service containing:

- an `oslc:QueryCapability` for `oslc_config:Configuration` (generic
  configuration members in stage A; Stream and Baseline members after the
  subtype phase);
- a query base with `oslc.where`, `oslc.select`, `oslc.searchTerms`, and paging
  behavior;
- an `oslc:Dialog` selection capability for configurations, with HTML UI,
  `terms` filtering, and a selection response containing configuration URIs;
- the generic Configuration resource shape; and
- optionally, read-only component/configuration creation factories once those
  operations are implemented.

The RM service in the same provider must continue to advertise the requirement
query/selection capability and the requirement shape. A single provider per
document is acceptable for the first implementation; do not create a new
provider for every branch or baseline.

The stage-A provider may add the Jazz process property after the heliple2-style
probe succeeds:

```xml
<jfs_proc:globalConfigurationAware
  rdf:datatype="http://www.w3.org/2001/XMLSchema#string">yes</jfs_proc:globalConfigurationAware>
```

Do not publish either link-index flag in the initial provider. They belong to
the TRS/LDX phase in [oslc-config-extended.md](./oslc-config-extended.md).
Omission is interpreted by CLM as `no`.

## Initial generic Configuration contract

The first provider-owned URIs should be stable and opaque:

```text
Component       {base}/oslc_config/components/{componentId}
Configurations  {componentUri}/configurations
Configuration   {base}/oslc_config/configurations/{configurationId}
```

The identifier portions must be URI-escaped and must not be accepted as raw
filesystem paths. A registry maps each configuration URI to a descriptor:

```text
ConfigurationContext
  Uri                 # provider-owned configuration URI
  ComponentUri
  Branch              # validated path segment
  Tag                 # HEAD or immutable publication tag
  JsonPath            # /data/{branch}/{tag}/strictdoc.json
  SidecarPath         # /data/{branch}/{tag}/sidecar.json
  SnapshotRevision    # digest/publication id for the paired files
```

The stage-A configuration resource returns generic
`rdf:type oslc_config:Configuration` plus title, identifier, component,
service provider, `acceptedBy` if required by the Jazz probe, and an instance
shape. It does not yet emit `Stream`, `Baseline`, `Selections`, or
`VersionResource` types. The same configuration handler must still enforce:

- `HEAD` descriptors are the only contexts writable through requirement PUT;
- non-`HEAD` descriptors are immutable;
- a context URI resolves to exactly one validated directory; and
- the paired JSON/sidecar snapshot revision is used by ETags and caches.

The generic Config query enumerates the registered descriptors and the picker
returns their stable URIs. A separate Config resource controller is not
required for Stream and Baseline in this stage: the later subtype phase should
extend this same handler polymorphically. The complete subtype graph,
Selections, VersionResource, TRS, LDX, and LQE plan is in
[oslc-config-extended.md](./oslc-config-extended.md).

The component and configuration containers can be introduced in the first
probe if Jazz requires them for discovery. If they are not needed by the
observed flow, do not pretend they already satisfy the later subtype shape;
record the result of the Jazz assessment and add the containers in the next
phase.

## Deferred subtype graph

When stage A has been tested, add the subtype representation using the same
registry:

```text
HEAD       -> rdf:type oslc_config:Stream
other tag  -> rdf:type oslc_config:Baseline
```

The later resources must include the Component `configurations` container,
Stream `baselines` and `selections`, Baseline `baselineOfStream`, `streams` and
`selections`, and Selections that select VersionResource URIs. `Stream` and
`Baseline` are both subclasses of `Configuration`; separate HTTP handlers are
not required. See the detailed shape/property rules and generated SDK mapping
in [oslc-config-extended.md](./oslc-config-extended.md).

## Context-aware RM REST contract

The context resolver is shared by direct resource access, query, selection,
compact, preview, sidecar, and cache code. It must accept both forms required
by the Config specification:

```http
Configuration-Context: https://strictdoc.example/oslc_config/configurations/main
```

```http
GET /?a=REQ-001&oslc_config.context=%3Chttps%3A%2F%2Fstrictdoc.example%2Foslc_config%2Fconfigurations%2Fmain%3E
```

The query parameter is an angle-bracket-delimited URI reference after
decoding. The implementation must not silently accept a raw filesystem path
or use a query value without URI validation.

Resolution rules:

1. If both header and query are present, the query value wins, even if the
   values differ.
2. Two or more differing query context values are `400 Bad Request`.
3. Repeated identical header values may be treated as one; differing header
   values are an error.
4. An unknown but syntactically valid configuration URI is `404 Not Found`,
   not a fallback to the default.
5. If no context is supplied, use the configured default stream, initially
   `{defaultBranch}/HEAD`. This is the practical Jazz choice when no global
   configuration protocol is available; the OSLC specification permits a
   server to reject the request instead.
6. A context on a non-versioned resource is not an error and may be used to
   resolve related resources.
7. In the later version phase, a context on a VersionResource URI is ignored
   and the exact version is returned.
8. Header-based responses include `Vary: Configuration-Context`; CORS allows
   the `Configuration-Context` header.

### Direct resource GET and HEAD

Both current routes must use the same resolver:

```text
GET|HEAD /?a={uid}
GET|HEAD /oslc/service_provider/{documentMid}/requirements/{uid}
```

The first is the existing public resource URI form. The second is the current
document query/resource path. They may return the same concept URI, but they
must not load different context logic.

For a concept GET:

1. resolve the configuration;
2. load the paired JSON and sidecar snapshot;
3. find `{uid}` in that export;
4. serialize the generic RM requirement for the first milestone;
5. merge only the sidecar links for this context and resource; and
6. return an ETag over context, snapshot, and representation variant.

The subtype phase adds a deterministic VersionResource URI and selected
version metadata at this point. Until then, the paired snapshot revision is an
internal cache and immutability key, not an externally advertised
VersionResource.

`HEAD` must perform the same resolution and existence checks as GET and return
the ETag, content type, context variation, and link/shape headers without the
body. This matters because the Config specification explicitly requires HEAD
for versioned concept resources in context.

### Requirement query

The existing:

```text
GET /oslc/service_provider/{documentMid}/requirements
```

must become context-aware. It must:

- use one resolved export for all members;
- put the correct concept URI and snapshot metadata in each member;
- preserve context in `nextPage` URIs;
- apply `oslc.where`, `oslc.select`, `oslc.orderBy`, `oslc.searchTerms`, and
  paging against the selected export;
- never mix a stream and baseline because the second page omitted a header;
  and
- return a context-aware ETag.

The configuration query endpoint must similarly return only registered generic
Configuration resources and support Jazz picker terms. After the subtype phase
it will return the corresponding Component/Stream/Baseline graph. A query
against a context-aware versioned-resource type must not return a resource
from another context.

### Requirement selection and previews

The existing requirement selector becomes context-aware. It must preserve the
context when the user opens the delegated UI, when it filters by `terms`, and
when it posts the selected resource URI back to Jazz. The selected URI should
be the concept URI; the consumer supplies context when it follows it.

Compact and preview requests must preserve the selected context in their
document URLs and RDF links. This includes the current `?compact` and
`?preview=small|large` forms and any future configuration-resource previews.
Do not cache a branch-independent preview if its title, link graph, selected
version, or component differs by context.

The OSLC Linking Profiles Config profile treats selection dialogs, previews,
PUT, and OSLC Query as required interoperability capabilities even where a
generic OSLC specification marks some of them as SHOULD/MAY. This is one of
the places where Jazz behavior elevates a generic optional capability.

## Write contract

### HEAD-context requirement PUT: link-only

`PUT` on a concept requirement resolved to the internal `HEAD` publication is
the one resource mutation in the first milestone. The response is still a
generic Configuration-context RM representation; the later subtype phase will
expose the same rule as a Stream operation:

```text
PUT /?a={uid}&oslc_config.context=...
Configuration-Context: ...       # query wins if both are present
If-Match: "..."
Content-Type: application/rdf+xml | text/turtle | application/n-triples
```

The operation must:

1. resolve and validate a `HEAD` context;
2. locate the requirement in that export;
3. validate the incoming RDF subject against the exact concept resource URI;
4. reject modifications to StrictDoc-owned properties;
5. extract only predicates allowed by `link-ownership.md`;
6. preserve the RDF closure needed for reified statements and link labels;
7. atomically replace that requirement’s entry in the stream sidecar;
8. recompute the sidecar/snapshot revision and ETag; and
9. return the refreshed resource with `200 OK`, preserving the existing Jazz
   compatibility behavior.

The existing implementation already rejects unsupported RDF media types and
parses RDF into a filtered sidecar graph. The new implementation must add
context and immutability checks before that code runs, not broaden the
allowlist.

If `If-Match` is supplied and stale, return `412 Precondition Failed` and do
not overwrite the sidecar. If it is absent, the policy must be explicit: the
first deployment may allow the current Jazz flow, but concurrent update tests
must be run before claiming safe multi-client writes.

### Baseline PUT

Any PUT resolved to a non-`HEAD` publication must fail before opening its
sidecar for writing. That publication is immutable even when the incoming RDF
contains only an accepted link predicate. The VersionResource-specific case is
deferred until the subtype phase.

The response code should be selected with a real Jazz test and then frozen in
the API contract. `409 Conflict` communicates an attempted mutation of a
published snapshot; `405 Method Not Allowed` communicates an unsupported
method. Both are preferable to a successful 200 response that changes a
baseline.

### Configuration-resource writes

The Config specification makes configuration PUT/DELETE and LDPC POST useful
local-server capabilities, but not all are mandatory for a read-only
published provider. StrictDoc should initially:

- advertise no creation factory until it can atomically publish JSON and
  sidecar pairs;
- return a read-only generic Configuration representation;
- use an external publication process to create a new baseline directory; and
- add POST stream/baseline and configuration metadata PUT only as a later
  transaction feature.

This is safe only if GCM can enumerate and select existing configurations. It
must be validated through the actual Jazz picker/contribution flow before the
provider is marked fully supported.

## Sidecar and snapshot publication

The first migration should retain the current sidecar value format but move its
scope beside the export:

```json
{
  "https://strictdoc.example/?a=REQ-001": {
    "NTriples": "<...> <...> .\n",
    "UpdatedAt": "2026-07-10T00:00:00+00:00"
  }
}
```

The sidecar key is the canonical concept URI, not the incoming URL with a
context query parameter. Context is represented by the directory containing
the sidecar. If a response serializer uses a context-qualified RDF subject
for Jazz compatibility, it must normalize that subject back to the canonical
key before reading or writing the sidecar.

Required file behavior:

- validate `{branch}` and `{tag}` as safe path segments;
- never derive a path directly from an arbitrary URI;
- load only the resolved sidecar, never scan all contexts for a resource;
- lock or use an atomic replace for each sidecar file;
- publish JSON and sidecar together for a baseline; and
- make a request see either the old complete snapshot or the new complete
  snapshot, never one file from each publication.

Baseline publication should write to a staging directory, validate the JSON,
sidecar, requirement UIDs, and RDF closure, calculate the snapshot revision,
then atomically rename the complete `{branch}/{tag}` directory into place.

When a stream produces a baseline, copy the stream sidecar as part of the
same snapshot operation. Future stream changes must not mutate the baseline
copy. Branch creation policy must also be explicit: initially copy a complete
source snapshot and assign a new stream URI; do not make a branch an alias to
the source directory.

## Link ownership and advanced indexing (summary)

The complete ownership table and StrictDoc policy are in
[link-ownership.md](./link-ownership.md). The first milestone retains the
explicit five-predicate sidecar allowlist and scopes it by `{branch}/{tag}`.
It does not implement an LDX server and does not claim that a sidecar value is
an owner-side link for every predicate.

The later owner-side TRS publication, outbound LDX discovery, Jazz link-index
flags, and LQE indexing concerns are intentionally moved to
[oslc-config-extended.md](./oslc-config-extended.md). That document also
defines why a discovered incoming link must not be materialized as a mutable
backlink in an immutable baseline.

## Authentication, CORS, and delegated UI gates

The Config Linking Profile extracted from the local Linking Profiles note
treats the following as required for its Config level: root services,
authentication, CSP/CORS for friends, selection dialogs, previews, link
ownership, PUT, OSLC Query, and configuration management. The generic Config
specification alone does not express all of those Jazz integration gates.

Current StrictDoc status:

- root services and SCR exist, but need the graph/route changes above;
- CORS currently allows broad origins/headers and needs an explicit tested
  policy for credentialed delegated dialogs and `Configuration-Context`;
- the app contains a toy OAuth1 provider marked for later replacement;
- OIDC/OAuth2 and Jazz friend relationships are not yet represented as a
  production integration contract; and
- CSP headers are not part of the current Config plan.

Before advertising full Config Linking Profile support, add integration tests
for:

- the chosen Jazz authentication/friend relationship;
- preflight `OPTIONS` for GET/PUT and `Configuration-Context`;
- preview and picker iframes from a Jazz origin;
- CSP and CORS headers that do not break delegated UI; and
- rejection of unauthorized sidecar and configuration mutations.

The initial local test environment may retain the toy OAuth1 path, but its
metadata must not be presented as production Jazz security conformance.

## Implementation plan

### Phase 0: freeze the public contract

Before editing the data loader:

1. Choose the canonical `/rootservices`, `/scr`, Config catalog, component,
   and generic configuration URI patterns.
2. Capture one real target Jazz rootservices/SCR/catalog/provider response as
   an interoperability fixture, including expanded namespaces.
3. Decide whether one StrictDoc document is one component in the deployment.
4. Choose the default branch and publication metadata source.
5. Define canonical concept URIs and ensure context is not encoded in them.
6. Freeze the status code for non-`HEAD` mutation attempts.

### Phase 1: discovery and metadata

Implement:

- a shared RDF/XML root-services builder used by both root-services routes;
- a conventional `/scr` controller and compatibility redirect/alias;
- a configuration catalog controller;
- configuration service-provider metadata and Jazz process properties;
- configuration query capability and delegated configuration picker;
- generic Configuration resource and ResourceShape endpoints; and
- explicit HEAD/OPTIONS behavior for discovery and configuration resources.

Acceptance gate: a client starting with only `{base}/rootservices` can discover
the configuration provider and open the configuration picker without knowing
StrictDoc’s internal paths.

### Phase 2: generic context and published configuration

Add:

- `IConfigurationContextResolver`;
- a validated configuration registry;
- configuration URI to `{branch}/{tag}` resolution;
- generic Configuration metadata and query/picker responses;
- default stream behavior; and
- context-aware requirement query, GET, compact, and preview responses.

Acceptance gate: the picker returns a generic Configuration URI, a selected
context reaches RM GET/query/preview, and no request can load a different
`{branch}/{tag}` through cache reuse or paging.

### Phase 3: context-scoped sidecars and link-only PUT

Change `ILinkSidecarService` and `FileLinkSidecarService` to accept the
resolved context or sidecar descriptor. Implement:

- migration of the old sidecar to the default `HEAD` context;
- per-context sidecar paths;
- atomic replace and conditional writes;
- exact subject validation;
- link policy data from `link-ownership.md`;
- `HEAD`-only accepted link PUT; and
- immutable non-`HEAD` rejection.

Acceptance gate: a link written to `main/HEAD` is absent from
`main/v1.2.0` unless it was present at publication, and an attempted
non-`HEAD` PUT does not change any file.

### Phase 4: assess Jazz behaviour

Run the stage-A server against the target Jazz GCM flow and capture:

- rootservices and SCR requests;
- Config catalog/provider discovery;
- generic Configuration query and picker requests;
- context-bearing RM requests and previews; and
- all writes attempted by Jazz against the selected context.

The assessment decides whether subtype RDF is required for the intended
workflow. Heliple2@CM is the positive baseline: it successfully participated
in Jazz GC in a limited capacity with generic Configuration resources, a
picker, and Jazz discovery metadata.

Acceptance gate: the result is a repeatable Jazz fixture and a written list
of missing subtype requests, rather than an assumption that every generated
Config resource must be implemented immediately.

### Phase 5: add the subtype graph if required

Extend the shared configuration handler, rather than creating unrelated
Stream and Baseline stores:

- emit `Stream` for `HEAD` and `Baseline` for other tags;
- add the Component configurations LDPC;
- add Stream baselines and Baseline derived-stream containers;
- add immutable/read-only Selections resources; and
- add deterministic VersionResource URIs and direct version GET/HEAD.

Acceptance gate: responses satisfy the local Config shapes, the same context
mapping remains in force, baselines remain stable after stream changes, and the
Jazz picker/context tests still pass.

### Phase 6: hand off extended integration

TRS publication, LDX discovery, LQE indexing, link-index capability flags,
change sets, and lifecycle POST operations are specified in
[oslc-config-extended.md](./oslc-config-extended.md). They are not prerequisites
for the generic stage-A probe and must not be advertised by the initial server.


## REST API acceptance matrix

The following is the minimum testable surface for the generic stage-A
implementation. Subtype, VersionResource, TRS, LDX, and LQE endpoints are
deliberately tracked in [oslc-config-extended.md](./oslc-config-extended.md),
not silently omitted.

| Endpoint | Methods | Required behavior |
|---|---|---|
| `{base}/rootservices` and well-known alias | GET, HEAD | RDF/XML Jazz root services; RM and Config catalog pointers; auth metadata |
| `{base}/scr` | GET, HEAD | Jazz SCR with application, root-services URI, RM/Config domains and catalogs; no fake LinkProvider |
| `{base}/oslc/catalog` | GET | RM ServiceProviderCatalog |
| `{base}/oslc_config/catalog` | GET | Config ServiceProviderCatalog |
| Config service provider | GET | Config domain, generic Configuration query capability, picker dialog, resource shape, Jazz process properties |
| Config query | GET | Search/paging over registered generic configurations; no cross-context leakage |
| Config picker | GET HTML | `terms` filter, delegated UI response, stable selected configuration URIs |
| Component | GET, HEAD, OPTIONS | Provider-owned component metadata; add the configurations LDPC if Jazz requires it in the stage-A assessment |
| Generic Configuration | GET, HEAD, OPTIONS | Generic `Configuration`, component, provider metadata, snapshot ETag |
| Concept requirement | GET, HEAD | Context-aware JSON selection, sidecar merge, ETag/Vary |
| Concept requirement in `HEAD` context | PUT | Link-only accepted predicates, `If-Match`, refreshed 200 response |
| Concept requirement in non-`HEAD` context | PUT | Immutable error; no file mutation |
| Requirement query | GET | Context-aware members, filters, paging, compact/preview links |
| Requirement picker | GET HTML | Context-preserving delegated UI |
| Stream/Baseline/Selections/VersionResource | — | Deferred until the Jazz assessment requires the subtype graph |
| TRS/LDX/LQE | — | Deferred; see the extended design |

The server should return an explicit `Allow` header on all read-only resource
responses so a Jazz client can distinguish a deliberately immutable resource
from a missing route. The initial generic resource may use the same handler
for both internal `HEAD` and non-`HEAD` descriptors while keeping the mutation
boundary explicit in the response and tests.

## Test scenarios

### Discovery

1. GET `{base}/rootservices` without credentials.
2. Follow the configuration catalog property, not a hard-coded path.
3. GET the catalog and each service provider.
4. Verify `oslc:domain=config#`, query capability, picker dialog,
   `globalConfigurationAware`, and acceptedBy metadata.
5. Fetch `/scr` and verify its application/root-services/domain graph.

### Context and generic configuration

1. Query the configuration picker and select `main/HEAD`.
2. GET `/?a=REQ-001` with the header context.
3. GET the same concept with the encoded angle-bracket query context.
4. Send both and confirm query context wins.
5. Send repeated differing values and confirm 400.
6. Update the `HEAD` export outside the OSLC server and confirm the new
   snapshot revision changes the ETag while a non-`HEAD` snapshot does not.
7. Confirm the same requirement UID is loaded from the selected directory and
   that a second-page or preview request cannot fall back to the default
   context.
8. Record whether Jazz requires `Stream`, `Baseline`, `Selections`, or
   VersionResource RDF before moving to the subtype phase.

### Link-only mutation

1. GET a requirement in the `HEAD` context and record ETag.
2. PUT RDF containing an allowed link and unchanged StrictDoc fields.
3. Confirm only `/data/{branch}/HEAD/sidecar.json` changes.
4. GET the requirement again and confirm the link is present.
5. GET the same UID under a baseline and confirm the baseline snapshot is
   unchanged.
6. PUT a title change, unknown predicate, wrong subject, stale ETag, and
   baseline context; confirm each fails without a sidecar write.

### Jazz/GCM generic probe

1. Register the local configuration provider in a test Jazz/GCM environment.
2. Use the configuration picker to select a generic Configuration URI.
3. Confirm Jazz sends that URI as the context for the requirement request.
4. Link a Jazz-owned resource to a StrictDoc requirement in that context and
   record whether the limited generic flow succeeds.
5. Attempt the same operation in a non-`HEAD` context and confirm the server
   rejects mutation without changing the snapshot.
6. Change the selected context and confirm the visible requirement/link set
   follows the selected directory.
7. Hand the request/response fixture and missing-subtype list to the extended
   subtype/TRS/LDX/LQE implementation plan.

## Non-goals for the first publication

The following are intentionally not prerequisites for the first end-to-end
local provider flow:

- acting as a global configuration server;
- resolving arbitrary nested global configuration composition locally;
- Stream/Baseline subtype RDF, Selections, and VersionResource endpoints;
- TRS publication, LDX discovery, and LQE indexing;
- change-set delivery and conflict resolution;
- editing StrictDoc-owned requirement content through OSLC PUT;
- creating a new StrictDoc requirement through OSLC POST; and
- implementing an LDX server inside StrictDoc.

The subtype and indexed integration work is specified in
[oslc-config-extended.md](./oslc-config-extended.md). These exclusions are
deliberate stage boundaries, not claims that Jazz never needs the features.

## Completion criteria

The generic implementation is ready for the Jazz assessment when:

- rootservices and SCR discovery work from the public base URI;
- the Config catalog/provider/picker chain works without path knowledge;
- generic Configuration resources resolve to validated `{branch}/{tag}`
  descriptors;
- header/query context resolution follows the specified precedence and error
  behavior;
- requirement GET, HEAD, query, picker, compact, preview, and PUT all use the
  same context descriptor;
- `HEAD` PUT is link-only and non-`HEAD` state is immutable;
- the paired JSON/sidecar snapshot is atomic and cache-keyed by context;
- the Jazz request/response fixture records whether generic Config is enough;
  and
- provider metadata is truthful about the limited capability actually tested.

The implementation is ready to claim subtype Config support only after the
assessment passes the extended shape, container, Selections, and
VersionResource criteria in [oslc-config-extended.md](./oslc-config-extended.md).
