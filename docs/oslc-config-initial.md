# Initial OSLC Configuration Management design

This is the implementation plan for adding configuration-management-aware
resource resolution to the StrictDoc RM OSLC server. It is intentionally an
initial design: it supports streams, baselines, context-aware reads, and
link-only stream updates without turning StrictDoc content into a mutable RDF
store.

The related documents are:

- [Link direction and ownership](./link-ownership.md), the predicate policy for
  writable sidecar links.
- [Link sidecar storage](./LINK_SIDECAR.md), the current unscoped sidecar
  behavior that this design scopes by configuration.
- [Jazz interop: writable RM link properties](./JAZZ_INTEROP.md), the captured
  protocol evidence for the existing Jazz link-save flow.
- [Configuration management proposal](./CONFIGURATION_MANAGEMENT_PROPOSAL.md),
  an earlier proposal whose SQLite recommendation is not part of this initial
  file-layout milestone.

## Decision summary

Use a stable OSLC concept-resource URI for each requirement and select its
StrictDoc representation from an OSLC configuration context:

```text
configuration context URI -> branch + tag -> files on disk
```

The initial file layout is:

```text
/data/{branch}/{tag}/strictdoc.json
/data/{branch}/{tag}/sidecar.json
```

`tag = HEAD` identifies an OSLC stream. The `{branch}` component is the stream
name in that case. Any other tag identifies a baseline under that branch. For
example:

```text
/data/main/HEAD/strictdoc.json       # stream: main
/data/main/HEAD/sidecar.json
/data/main/v1.2.0/strictdoc.json     # baseline: v1.2.0 from main
/data/main/v1.2.0/sidecar.json
/data/feature-x/HEAD/strictdoc.json  # stream: feature-x
```

The JSON export remains the authoritative source for requirements. The sidecar
contains only the explicitly allowed link assertions and their metadata. A
successful stream PUT can update the sidecar, but never the StrictDoc JSON.

## Why this is the right scope

The current server has one configured JSON path and caches documents and
requirements without a configuration key. `StrictDocService` reads
`StrictDoc:JsonFilePath` once and uses fixed cache keys; `RequirementController`
resolves `/?a={uid}` against that one set of requirements. The current PUT path
already has the desired high-level boundary: it parses RDF, passes it to the
sidecar service, and applies only persisted link values back to the response.
`FileLinkSidecarService` currently stores a JSON index keyed by resource URI and
allow-lists five RM-side predicates.

The configuration work should preserve that boundary and add context to every
operation that can expose a requirement:

- individual requirement GET and PUT;
- requirement query and selection results;
- compact and preview representations;
- resource shapes and capability metadata where a link property is advertised;
- ETags and caches.

The adapter is not being asked to edit titles, descriptions, identifiers,
hierarchy, or other StrictDoc fields through OSLC.

## Evidence reviewed

### heliple2

heliple2 is useful as an example of the OSLC Configuration Management surface,
but not as a complete implementation recipe:

- `ServiceProviderService5.java` and `ServiceProviderService6.java` expose
  configuration query capabilities and selection dialogs through the OSLC
  configuration-management namespace.
- `ConfigurationService.java` exposes GET, HTML, compact, and preview forms
  for a `Configuration` resource.
- `StreamService.java` exposes GET, HTML, compact, and preview forms for a
  `Stream` resource.
- `ApplicabilityContextToConfiguration.java` maps an application-side
  applicability context to an OSLC `Configuration`, including a stable
  resource URI, title, identifier, description, and component link.
- `ConfigurationId.java` and `StreamId.java` show the generated pattern of
  turning a resource identifier into an OSLC resource URI.

These services are generated/delegate-based and the inspected resource
services are GET-oriented; they do not show context-aware requirement loading
or a file-backed stream/baseline export layout. The StrictDoc plan should copy
the useful separation between configuration resources and domain resources,
not heliple2’s unrelated source-domain model or every generated endpoint.

### refimpl

The OSLC reference implementation is a useful false-positive check:

- RM resource GET is implemented in `WebServiceBasic.java`.
- RM resource PUT uses an `If-Match` parameter and delegates replacement to
  `RestDelegate.updateRequirement`.
- The reference RM server has no configuration-management implementation in
  its RM service set. It therefore does not make local configuration resources,
  global configuration traversal, or GCM integration a prerequisite for an
  ordinary RM provider.

The conclusion is that StrictDoc needs context-aware resolution and the
configuration capabilities required by the intended Jazz flow, but does not
need to reproduce every heliple2 configuration-domain resource before the
first end-to-end requirement lookup works.

### Linking Profiles

The Linking Profiles note defines the Config profile as extending the
bi-directional profile with configuration management, link ownership, PUT on
resources, preview, and OSLC Query. It requires the primary predicate to be
stored on the source/owner domain side and describes the secondary predicate as
the target-side view. The full extracted table and the StrictDoc subset are in
[link-ownership.md](./link-ownership.md).

The profile is non-normative. Its value here is predictable interoperability:
it tells the adapter which link assertions may be accepted and where the
canonical assertion belongs. It does not make arbitrary requirement fields
writable.

## Context model

Introduce a context descriptor resolved before loading a JSON export:

```text
ConfigurationContext
  Uri              # the URI supplied by the client
  Branch           # the {branch} path component
  Tag              # HEAD for a stream, otherwise a baseline tag
  Kind             # Stream when Tag == HEAD, Baseline otherwise
  JsonPath         # /data/{branch}/{tag}/strictdoc.json
  SidecarPath      # /data/{branch}/{tag}/sidecar.json
  Revision         # immutable export revision, e.g. hash or publication id
```

Do not derive a filesystem path directly from an untrusted URI. Resolve the
context URI through a registry or a tightly validated provider-owned URI
scheme, then validate branch and tag names against a safe path-segment policy.
The registry should be able to map externally supplied GCM context URIs to the
same local descriptor if the client does not use provider-owned configuration
URIs.

The resolver should support both OSLC context mechanisms already identified in
the repository:

```text
Configuration-Context: <configuration-uri>
```

and:

```text
?oslc_config.context=<encoded-configuration-uri>
```

Rules:

1. If both are absent, use one configured default stream, initially
   `{defaultBranch}/HEAD`.
2. If both are present and differ, return `400 Bad Request`.
3. If a context URI is syntactically valid but unknown, return `404 Not Found`
   rather than silently falling back to the default.
4. If a context identifies a baseline, classify it as immutable from the
   resolved `Tag`, not from a client-controlled flag.
5. Pass the resolved descriptor explicitly through services; do not use a
   mutable process-global “current context”.

## Read behavior

For a request such as:

```text
GET /?a=REQ-001
Configuration-Context: https://strictdoc-rm.example/oslc/configurations/main
```

the server should:

1. resolve the context URI to `main/HEAD`;
2. load `/data/main/HEAD/strictdoc.json`;
3. locate `REQ-001` in that export;
4. build the normal OSLC RM resource;
5. merge `/data/main/HEAD/sidecar.json` for the exact resource URI; and
6. return the representation with the context-sensitive cache headers.

The same UID may resolve to a different requirement representation under
`main/v1.2.0`. The resource URI can remain stable because the selected
configuration is part of request context rather than part of the concept URI.

The context must also flow into:

- OSLC Query, so a query never mixes requirements from two exports;
- selection dialogs, so a selected URI is from the requested context;
- compact resources and preview documents, so Jazz previews the selected
  version;
- link sidecar reads, so a Jazz link from one stream does not appear in another
  stream by accident.

If HTML exports vary by branch or baseline, publish them beside the JSON export
and resolve them from the same descriptor. If they are initially identical,
the file lookup may remain shared, but preview URLs still need to preserve the
context for a later context-specific HTML export.

## Write behavior

Only a stream (`tag = HEAD`) accepts the link-only update path. A baseline is a
published immutable view; its JSON and sidecar must be read-only.

For a stream PUT:

1. Resolve the context and verify that it is a stream.
2. Resolve the requirement in that stream’s `strictdoc.json`.
3. Parse RDF/XML, Turtle, or N-Triples according to `Content-Type`.
4. Require the incoming subject to equal the exact requirement URI.
5. Compare StrictDoc-owned fields with the current representation and reject
   attempts to modify them.
6. Extract only the explicit sidecar predicates in
   [link-ownership.md](./link-ownership.md); reject arbitrary predicates.
7. Preserve the RDF closure needed for link titles or reified link metadata,
   skolemizing blank nodes as the current sidecar implementation does.
8. Atomically replace the context’s `sidecar.json` entry for the resource.
9. Return `200 OK` with the refreshed link-only representation for Jazz
   compatibility. Keep the existing `204 No Content` compatibility finding in
   [JAZZ_INTEROP.md](./JAZZ_INTEROP.md) as a regression test.

For a baseline PUT, return a clear immutable-context error and do not open the
sidecar for writing. The exact status (`405` or `409`) should be selected with
the Jazz client test, but the result must not be a successful mutation.

PUT should gain conditional-update support when the client supplies
`If-Match`. The ETag must represent the selected context, JSON export revision,
and sidecar revision; a stale sidecar update must not overwrite a newer link
graph.

## Sidecar layout and migration

Keep the current JSON sidecar value format initially, but move the file next to
the export:

```json
{
  "https://strictdoc-rm.example/?a=REQ-001": {
    "NTriples": "<...> <...> <...> .\n",
    "UpdatedAt": "2026-07-10T00:00:00+00:00"
  }
}
```

The configuration directory is the scope boundary. Do not key by UID alone,
and do not load every sidecar file when serving one resource. A future database
can be considered if concurrent writers or link volume justify it, but SQLite
is not needed to establish the initial configuration behavior.

Migration steps:

1. Choose the current default branch name.
2. Copy the current JSON export to
   `/data/{defaultBranch}/HEAD/strictdoc.json`.
3. Convert the current sidecar index to
   `/data/{defaultBranch}/HEAD/sidecar.json` without changing its RDF payload.
4. Add a context registry entry for the default stream.
5. Keep a one-time fallback/import command for the old paths, then remove the
   old `StrictDoc:JsonFilePath` and unscoped sidecar assumptions after the
   migration is verified.

Baseline publication should create a complete directory atomically: write the
JSON and sidecar to a staging directory, validate them, then publish/rename
the `{branch}/{tag}` directory. A request must see either the old published
revision or the new one, never a half-written pair.

## HTTP and cache contract

Every context-sensitive response must vary by content negotiation. When the
context is supplied in the header, include:

```text
Vary: Accept, Configuration-Context
```

When the context is supplied in the query parameter, the URL separates cache
entries; `Vary: Accept` is still required. ETags should include at least:

```text
context URI + export revision + sidecar revision + representation variant
```

Do not put a diagnostic or non-standard context triple in requirement RDF just
to make caching visible. If configuration resources are exposed, use the OSLC
Configuration Management vocabulary and shapes for that metadata.

## Configuration resource surface

The smallest useful local configuration surface is:

- one `Stream` resource for each published `{branch}/HEAD` directory;
- one `Baseline` resource for each published `{branch}/{tag}` directory where
  `tag != HEAD`;
- query and selection capability sufficient for a client to discover those
  resources;
- a stable mapping from each configuration resource URI to its context
  descriptor.

The heliple2 implementation is a useful example for exposing configuration
query, resource, compact, and preview endpoints. The refimpl comparison shows
that these are not part of the ordinary RM server baseline, so they should be
implemented as the configuration slice needed by the selected Jazz flow.

Defer the following until a concrete client requires them:

- global configurations spanning multiple providers;
- contribution traversal and nested configuration resolution;
- change-set resources and version-resource mutation;
- OSLC Link Discovery Management;
- creation or deletion of StrictDoc requirements through OSLC.

## Implementation phases

### 1. Context resolution

Add an `IConfigurationContextResolver` and `ConfigurationContext` model. Cover
header/query/default behavior, conflict rejection, unknown contexts, safe path
validation, and context-aware error responses.

### 2. Context-aware StrictDoc loading

Change the StrictDoc service API to accept a context descriptor. Key parsed-data
caches by context URI plus export revision, and ensure negative caches are also
context-scoped. Preserve the current UID/resource URI mapping.

### 3. Context-aware sidecars

Change the sidecar API to accept the resolved context. Move the file path from a
single configured store to `/data/{branch}/{tag}/sidecar.json`, retain the
N-Triples payload, and implement atomic file replacement per context.

### 4. Link policy enforcement

Move the predicate allowlist into a named policy based on
[link-ownership.md](./link-ownership.md). Add tests proving that StrictDoc
fields and unapproved predicates cannot be persisted, while approved link
assertions round-trip only within their selected context.

### 5. Stream/baseline discovery

Expose the minimal OSLC Configuration Management resources and query/selection
capabilities. Start with local provider-owned context URIs; add external URI
mapping only when the Jazz/GCM flow demonstrates that it is needed.

### 6. Conditional updates and cache correctness

Add context/export/sidecar-aware ETags, `If-Match` handling, `Vary` headers, and
tests for stale updates and stream/baseline isolation.

### 7. End-to-end Jazz verification

Verify, in order:

1. Jazz discovers the RM link property from the provider-owned shape.
2. Jazz sends the intended configuration context.
3. A stream GET returns the stream export and sidecar links.
4. A stream link-only PUT returns the Jazz-compatible `200 OK` behavior.
5. The same resource under a baseline is unchanged and rejects PUT.
6. A link created in one stream does not appear in another stream or baseline.

## Acceptance criteria

- A requirement GET under two contexts can return two different exports while
  preserving the concept-resource URI.
- No context-sensitive request reads the old global JSON path after migration.
- Query, preview, compact, and direct resource responses use the same context.
- Stream PUT cannot modify StrictDoc-owned fields.
- Baseline PUT cannot mutate either `strictdoc.json` or `sidecar.json`.
- Only the approved link predicates are written, with the exact subject URI.
- Sidecar links are isolated by `{branch}/{tag}`.
- Cache validators cannot serve one context’s representation for another.
- The local configuration surface can enumerate streams and baselines needed by
  the target Jazz flow.

## Open decisions

These decisions should be made before implementation begins:

- the provider-owned URI shape for streams and baselines;
- how external GCM context URIs map to local `{branch}/{tag}` descriptors;
- whether the initial Jazz flow needs local configuration resources or only
  context-header/query handling;
- whether secondary/incoming link predicates remain a Jazz compatibility mode
  or are rejected in strict Config-profile mode;
- how sidecar links are copied, merged, or intentionally not copied when a
  stream produces a baseline or a new branch;
- the exact immutable-baseline response status for PUT.

## Wiki audit

The supplied wiki snapshot was inspected for the files with configuration and
Jazz-related names, including:

```text
wiki/bin/attach/Deployment/CLMCfgMRecommendedPractices
wiki/bin/attach/Deployment/CLMUsageModelBestPractices
wiki/bin/attach/Deployment/IntegratingWithConfigurationManagementEnabledCLMApplications
wiki/bin/attach/Main/AppSdkConsumingBaseline
wiki/bin/attach/Main/AssociatingGlobalConfigurationsAndReleases
wiki/bin/attach/Main/3016x_RRCStreamSetUp
```

Those files are HTML login responses (`Jazz Community Site - Login`), not the
underlying Jazz articles. They contain no usable configuration-management
protocol or deployment guidance, so this design does not create
`docs/jazz-config.md` or derive requirements from those files. The actionable
Jazz evidence available in this repository remains
[JAZZ_INTEROP.md](./JAZZ_INTEROP.md).

