# Link sidecar storage

## Goal

The StrictDoc JSON export remains the authoritative source for requirements.
OSLC links that Jazz requires this adapter to persist are stored separately as
sidecar RDF. This keeps the adapter effectively read-only with respect to
StrictDoc content while allowing Jazz interop for directional links.

The sidecar stores only link assertions and their small metadata subgraphs. It
must not duplicate titles, descriptions, statements, hierarchy, or other
StrictDoc-owned data.

## Scope

Writable sidecar predicates should be explicitly allow-listed. For the Jazz
`ChangeRequest affectsRequirement Requirement` case, the expected RM-side
secondary predicate is:

```text
http://open-services.net/ns/rm#affectedBy
```

Other likely RM-side secondary predicates from the OSLC RM shape are:

```text
http://open-services.net/ns/rm#implementedBy
http://open-services.net/ns/rm#trackedBy
http://open-services.net/ns/rm#validatedBy
http://open-services.net/ns/rm#satisfiedBy
```

Do not accept arbitrary RDF updates. Treat the sidecar as a controlled writable
slice of the resource graph.

## Storage model

The current implementation uses a file-backed JSON index for simplicity:

```json
{
  "https://strictdoc-rm.oslc.ldsw.eu/?a=SDOC-HIGH-REQS-MANAGEMENT": {
    "NTriples": "<...> <...> <...> .\n",
    "UpdatedAt": "2026-06-23T18:26:01.509098+00:00"
  }
}
```

The production-oriented SQLite equivalent remains:

```sql
CREATE TABLE resource_link_sidecars (
  resource_uri TEXT PRIMARY KEY,
  ntriples TEXT NOT NULL,
  version INTEGER NOT NULL DEFAULT 1,
  updated_at TEXT NOT NULL,
  updated_by TEXT
);
```

The `resource_uri` is the exact OSLC resource URI, for example:

```text
https://strictdoc-rm.oslc.ldsw.eu/?a=SDOC-HIGH-REQS-MANAGEMENT
```

Do not key the sidecar by UID alone. In OSLC, the resource URI is the
identifier.

## RDF format

Store sidecar graphs as N-Triples. Export is then:

```sql
SELECT ntriples FROM resource_link_sidecars ORDER BY resource_uri;
```

and concatenate the rows with a newline.

This works only if stored sidecar graphs contain no blank nodes. Incoming Jazz
RDF may contain blank nodes, for example to represent a reified link node with a
title. Normalize those blank nodes into Skolem IRIs before persistence.

Use the RDF-recognized `.well-known/genid` convention:

```text
https://strictdoc-rm.oslc.ldsw.eu/.well-known/genid/oslc-sidecar/sha256-...
```

The Skolem IRI identifies an adapter-generated sidecar node. It does not need to
be a useful deployment URL.

## Write algorithm

For PUT/PATCH handling:

1. Parse the incoming RDF representation.
2. Resolve the subject resource URI exactly.
3. Reject changes to StrictDoc-owned predicates.
4. Extract only allow-listed writable link predicates from the resource subject.
5. Include the RDF closure needed by those links, such as link-title metadata
   nodes.
6. Skolemize blank nodes into stable `.well-known/genid/...` IRIs.
7. Serialize the extracted sidecar graph as N-Triples.
8. Replace the SQLite row atomically.
9. Increment the sidecar version.

The full GET representation is:

```text
StrictDoc JSON-derived RDF graph
+ sidecar RDF graph for the exact resource URI
```

The ETag should cover both parts, for example:

```text
hash(strictdoc_export_revision, resource_uri, sidecar_version)
```

## Delete semantics

If a PUT representation omits an existing writable link, treat that as deletion
of that sidecar assertion, provided the request passed precondition checks.

If the extracted writable sidecar graph becomes empty, delete the SQLite row.

## Optional derived index

If lookup by target becomes necessary, add a derived index:

```sql
CREATE TABLE outbound_link_index (
  source_uri TEXT NOT NULL,
  predicate_uri TEXT NOT NULL,
  target_uri TEXT NOT NULL,
  PRIMARY KEY (source_uri, predicate_uri, target_uri)
);
```

Keep `ntriples` authoritative. The index is rebuildable.
