# Configuration management proposal

## Problem

The adapter currently resolves each OSLC requirement URI against one StrictDoc
JSON export. For OSLC Configuration Management, the same concept resource URI
must resolve under a configuration context. Practically, that means a request
for:

```text
https://strictdoc-rm.oslc.ldsw.eu/?a=REQ-001
```

must be resolved using either:

```text
Configuration-Context: <configuration-uri>
```

or:

```text
?oslc_config.context=<encoded-configuration-uri>
```

The local OSLC config primer and config spec both describe those two mechanisms.
A cache-safe response must also account for configuration context; if the header
form is used, the server should return `Vary: Configuration-Context`.

## Working model

Treat StrictDoc requirement URIs as OSLC concept resource URIs. The selected
configuration context maps to source coordinates:

```text
configuration context URI -> branch | tag | commit -> StrictDoc JSON export location
```

The requirement URI remains stable. The representation changes with the selected
configuration context.

Example:

```text
GET /?a=REQ-001
Configuration-Context: https://strictdoc-rm.oslc.ldsw.eu/oslc/configs/main

resolves to:
branch=main
json=s3://strictdoc-rm/exports/main/strictdoc.json
```

```text
GET /?a=REQ-001
Configuration-Context: https://strictdoc-rm.oslc.ldsw.eu/oslc/configs/v1.2.0

resolves to:
tag=v1.2.0
json=s3://strictdoc-rm/exports/tags/v1.2.0/strictdoc.json
```

## Storage recommendation

Move the mutable sidecar and configuration registry to SQLite before
configuration management grows the file format further.

Do not turn SQLite into an RDF store. Keep the RDF payload opaque and store
small indexes needed by the adapter.

Recommended first schema:

```sql
create table configuration_contexts (
  uri text primary key,
  kind text not null check (kind in ('branch', 'tag', 'commit')),
  ref_name text not null,
  json_location text not null,
  html_location text null,
  is_default integer not null default 0,
  created_at text not null,
  updated_at text not null
);

create table link_sidecars (
  resource_uri text not null,
  configuration_uri text not null,
  ntriples text not null,
  updated_at text not null,
  primary key (resource_uri, configuration_uri),
  foreign key (configuration_uri) references configuration_contexts(uri)
);

create index link_sidecars_configuration_uri_idx
  on link_sidecars(configuration_uri);
```

Keep N-Triples as the stored sidecar format. It is stable, line-oriented, easy
to export, and matches the current sidecar semantics. Blank nodes should still
be skolemized to `/.well-known/genid/...` before persistence.

Use `Microsoft.Data.Sqlite` directly at first. Add Dapper only if the SQL
mapping code starts obscuring the protocol logic. The expected scale is tiny:
fewer than 10,000 requirements and probably fewer than 100 manual links/month.

## Link sidecar scoping

Sidecar links should be configuration-scoped by default:

```text
(resource_uri, configuration_uri) -> sidecar RDF
```

That avoids accidentally leaking a Jazz-created backlink from one branch/tag
context into another. If we later need global links, add an explicit global
context row rather than omitting the context key.

PUT behavior:

1. Resolve the request configuration context.
2. Resolve the StrictDoc JSON export for that context.
3. Validate that the requirement exists in that context.
4. Parse incoming RDF.
5. Persist only allow-listed writable RM link predicates to
   `(resource_uri, configuration_uri)`.
6. Return `200 OK` for Jazz compatibility. An empty `200 OK` is known to work;
   returning a body is an adapter choice, not a Jazz expectation.

GET behavior:

1. Resolve the request configuration context or default context.
2. Load the StrictDoc JSON export for that context.
3. Build the requirement RDF from JSON.
4. Merge sidecar RDF from `(resource_uri, configuration_uri)`.
5. Return `Vary: Configuration-Context` when the header form was used.

## Source layout options

### Local folder layout

Use this for the first implementation:

```text
/data/exports/
  main/strictdoc.json
  feature-x/strictdoc.json
  tags/v1.2.0/strictdoc.json
```

Pros: simple, debuggable, no cloud dependency.

Cons: deployment must synchronize exports.

### S3/object storage

Use once exports are produced by CI:

```text
s3://strictdoc-rm/exports/branches/main/strictdoc.json
s3://strictdoc-rm/exports/tags/v1.2.0/strictdoc.json
```

Pros: clean CI handoff, versioned blobs, deploys do not need the export files
baked into the image.

Cons: credentials and caching need care.

### Git checkout

Possible, but less attractive for the adapter runtime. The adapter would need to
clone/fetch and manage worktrees or archive extraction. Prefer CI producing
immutable JSON/HTML exports and publishing them to local storage or S3.

## HTTP/cache details

ETags should include:

```text
configuration_uri
strictdoc_json_revision
sidecar_updated_at/version
```

For header-based configuration context:

```text
Vary: Accept, Configuration-Context
```

For query-parameter configuration context, normal URL-based cache separation is
enough, but `Vary: Accept` is still required for RDF content negotiation.

The response body should include the selected configuration context only if/when
we add the OSLC configuration vocabulary to the representation. Do not invent
extra non-standard triples just for debugging.

## Phased implementation

1. Add a `IConfigurationContextResolver`.
   - Accept `Configuration-Context` header.
   - Accept `oslc_config.context` query parameter.
   - Reject conflicting values.
   - Fall back to one configured default context.

2. Add a configuration registry.
   - Start with SQLite.
   - Seed it from appsettings or a small migration.
   - Map context URI to branch/tag/commit and JSON/HTML locations.

3. Make StrictDoc loading context-aware.
   - Add context to the service API.
   - Cache parsed JSON per `(configuration_uri, json_revision)`.

4. Move link sidecars to SQLite.
   - Preserve opaque N-Triples.
   - Scope rows by `(resource_uri, configuration_uri)`.
   - Keep the existing JSON sidecar importer as a one-time migration path.

5. Add OSLC configuration resources.
   - Expose local stream/baseline resources once Jazz needs to browse them.
   - Defer global-configuration contribution traversal until required.

## Open questions

- Which Jazz flow will send the configuration context first:
  `Configuration-Context` header, `oslc_config.context`, or both?
- Are sidecar links allowed only in mutable stream contexts, or should selected
  baselines reject PUT?
- How are branch/tag names authorized and published by CI?
- Should the adapter expose OSLC local configuration resources itself, or only
  understand Jazz/GCM-provided context URIs?
- How should a sidecar link created on one branch be cherry-picked or merged to
  another branch, if at all?
