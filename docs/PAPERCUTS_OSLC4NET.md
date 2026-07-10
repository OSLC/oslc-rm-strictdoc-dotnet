# OSLC4Net papercuts to handle later

This file tracks OSLC4Net-level issues discovered while making the StrictDoc RM
adapter interoperate with Jazz. These are not adapter fixes; they belong either
in OSLC4Net runtime behavior, generated metadata, or documentation.

## Instance shape endpoint generated from annotations

OSLC4Net should support exposing an `oslc:instanceShape` endpoint generated from
OSLC annotations.

Expected direction:

- Reuse the annotated resource model metadata.
- Generate a provider-owned `oslc:ResourceShape` document.
- Include writable link properties with correct:
  - `oslc:occurs`
  - `oslc:propertyDefinition`
  - `oslc:representation`
  - `oslc:valueType`
  - `oslc:readOnly`
- Advertise that generated shape from resource instances using
  `oslc:instanceShape`.
- Advertise the same generated shape from service-provider capabilities where
  relevant.

This is aligned with the approach attempted by the incoming OSLC4Net PR and with
how the reference implementation exposes shapes. The adapter currently has a
local manual shape endpoint only because the general OSLC4Net mechanism is not
yet there.

> This should already be possible with the ShapeFactory but it is not yet
> documented.

## .NET `System.Uri` fragment equality

`System.Uri` equality ignores fragment-only differences for HTTP URIs. That is
wrong for RDF/OSLC identity, where these are distinct resources:

```text
http://open-services.net/ns/rm#affectedBy
http://open-services.net/ns/rm#trackedBy
```

Do not use default `HashSet<Uri>`, `Dictionary<Uri, ...>`, `Uri.Equals`, or
`Uri ==` semantics for RDF resources. Prefer exact lexical URI strings,
dotNetRDF node equality, or an explicit RDF URI comparer.

See
[URI_FRAGMENT_REPORT.md](./docs/experiments/uri-fragment/URI_FRAGMENT_REPORT.md).

Follow-up design direction:

- Introduce a string-backed `RdfUri`/`OslcUri` value type with ordinal lexical
  equality.
- Keep dotNetRDF `IUriNode` at RDF serialization/query boundaries, not in OSLC
  domain model classes.
- Use concrete `HashSet<RdfUri>` internally for mutable collections and expose
  read-only views only after serializer support is confirmed.
- Do not use `ReadOnlySpan<char>` as the model/storage type; it cannot be stored
  safely in normal collection-backed domain models.

## OSLC4Net documentation updates

OSLC4Net docs should describe practical Jazz interoperability papercuts so
provider authors do not rediscover them by trial and error.

TODO topics:

- Jazz may require `oslc:instanceShape` on the selected resource, not only
  `oslc:QueryCapability oslc:resourceShape`.
- Shape documents must be dereferenceable with RDF content negotiation.
- PUT returning 204 may be HTTP-correct but still fail in Jazz link-save flows;
  returning 200 OK is the safer Jazz-compatible response. An empty 200 OK was
  verified to work.

## Jazz link-save response status

Jazz successfully reached the StrictDoc requirement `PUT` path after the adapter
advertised a writable provider-owned `oslc:instanceShape`, but Jazz failed when
the adapter answered the successful update with:

```text
204 No Content
```

The workaround is to return:

```text
200 OK
```

An empty `200 OK` was verified to work. Do not document this as Jazz expecting
an updated RDF representation in the response body. This is not a general HTTP
requirement; `204` is valid for a successful `PUT`. It is a Jazz
interoperability papercut in this update flow.

OSLC4Net docs should call this out for provider authors implementing
Jazz-compatible link directionality.

## Standard shape URI handling

OSLC4Net should handle standard OSLC shape URIs correctly instead of assuming
that a shape URI dereferences directly to a single obvious shape document.

Required client behavior:

- Keep a cache for dereferenced shape documents.
- Dereference shape URIs with content negotiation, accepting multiple RDF media
  types, at least:
  - `application/rdf+xml`
  - `text/turtle`
  - `application/ld+json`
  - possibly `application/n-triples`
- Parse the returned RDF graph.
- Find the requested shape resource by the original shape URI inside the
  response graph.
- Do not assume the request URI and the document URL are always the same thing.

This matters for standard shape references such as OSLC RM shapes. A provider
may legitimately reference a standard shape URI, and a client needs RDF-aware
lookup semantics rather than URL/string-only assumptions.
