# Jazz interop: writable RM link properties

See [LINK_SIDECAR.md](./LINK_SIDECAR.md) for the proposed persistence model. See
[CONFIGURATION_MANAGEMENT_PROPOSAL.md](./CONFIGURATION_MANAGEMENT_PROPOSAL.md)
for the next-step design for resolving requirements under OSLC configuration
contexts.

## Current symptom

Jazz reports:

```text
The application providing the resource of
'https://strictdoc-rm.oslc.ldsw.eu/?a=SDOC-HIGH-REQS-MANAGEMENT'
does not support the required link property.

The resource cannot be updated:
Could not access the link property with one of the known OSLC protocols.
```

Observed server traffic contains only:

```text
GET https://strictdoc-rm.oslc.ldsw.eu/?a=SDOC-HIGH-REQS-MANAGEMENT
GET https://strictdoc-rm.oslc.ldsw.eu/?a=SDOC-HIGH-REQS-MANAGEMENT
```

with RDF/XML and HTML negotiation. No PUT is attempted.

That strongly suggests Jazz is failing capability discovery before update. It is
not yet testing our persistence path.

After adding `oslc:instanceShape` and the provider-owned requirement shape, Jazz
progressed to:

```text
GET /?a=SDOC-HIGH-REQS-MANAGEMENT -> 200 application/rdf+xml
PUT /?a=SDOC-HIGH-REQS-MANAGEMENT -> 405
```

That confirms the discovery fix worked. The remaining failure was no longer
shape discovery; it was the missing HTTP PUT implementation on the selected
requirement URI.

## Evidence from the save-time server log

From the moment Save is pressed in Jazz, the adapter receives exactly this
protocol sequence:

```text
GET /.well-known/oslc/rootservices.xml                    -> 200 application/rdf+xml
GET /?a=SDOC-HIGH-REQS-MANAGEMENT Accept: application/rdf+xml -> 200 application/rdf+xml
GET /?a=SDOC-HIGH-REQS-MANAGEMENT                         -> 200 text/html
```

There is no:

```text
PUT /?a=SDOC-HIGH-REQS-MANAGEMENT
PATCH /?a=SDOC-HIGH-REQS-MANAGEMENT
OPTIONS /?a=SDOC-HIGH-REQS-MANAGEMENT
GET /oslc/catalog
GET /oslc/service_provider/...
GET /oslc/shapes/...
```

So Jazz is not reaching the link-storage step. It is deciding from rootservices
plus the selected resource representations that the resource cannot be updated
for the required link property.

One additional detail from the log: the HTML request is served by
`StaticFileWithContentNegotiationMiddleware` as `/data/html/index.html`, not by
`RequirementController`. That may be acceptable for a human browser fallback,
but it means the HTML representation of the OSLC resource is not a
requirement-specific representation and cannot advertise any OSLC affordances.
The RDF/XML representation is therefore the critical one for Jazz.

## Link direction involved

The user action is:

```text
ChangeRequest1 oslc_cm:affectsRequirement Requirement1
```

The corresponding RM-side secondary predicate is:

```text
Requirement1 oslc_rm:affectedBy ChangeRequest1
```

The OSLC Linking Profiles note maps:

```text
CM oslc_cm:affectsRequirement -> RM oslc_rm:affectedBy
```

The same note also says links should normally be stored only on the owner/source
side and that incoming/secondary links should preferably be discovered instead
of redundantly stored. However, IBM Jazz has
historical/non-configuration-management behaviours where backlink storage is
still expected. This adapter needs to interoperate with that behaviour even
though StrictDoc JSON remains read-only.

## Why no PUT is fishy

If Jazz intended to write `oslc_rm:affectedBy`, the expected sequence would
include:

```text
GET requirement RDF
PUT requirement RDF with added oslc_rm:affectedBy
```

or another known OSLC update protocol.

Since no PUT is observed, Jazz likely cannot prove from the GET/discovery
metadata that the resource supports the required writable property.

The likely missing protocol signals are:

1. The requirement RDF does not include `oslc:instanceShape`.
2. The advertised service shape is an external OSLC RM shape URI, not a
   provider-owned shape that Jazz can reliably dereference.
3. The provider-owned shape does not explicitly declare `oslc_rm:affectedBy` as
   writable.
4. The HTTP resource endpoint does not advertise or implement update support.

Given the save-time log, `oslc:instanceShape` on the selected resource is the
most important next signal. Jazz is not fetching the service provider during
this flow, so relying only on `oslc:QueryCapability oslc:resourceShape` is
probably insufficient.

## Required shape signal

The provider should expose a local Requirement resource shape and point to it
from both:

```text
oslc:QueryCapability oslc:resourceShape <.../oslc/shapes/requirement>
Requirement1 oslc:instanceShape <.../oslc/shapes/requirement>
```

The local shape must include:

```turtle
<#affectedBy>
    a oslc:Property ;
    oslc:name "affectedBy" ;
    oslc:occurs oslc:Zero-or-many ;
    oslc:propertyDefinition oslc_rm:affectedBy ;
    oslc:range oslc:AnyResource ;
    oslc:representation oslc:Reference ;
    oslc:valueType oslc:Resource ;
    oslc:readOnly false ;
    dcterms:title "affectedBy" .
```

This mirrors the OSLC RM Requirement shape: `affectedBy` is zero-or-many,
reference-valued, resource-valued, and writable.

## Required update signal

After Jazz recognizes the property as writable, it may attempt an update. The
requirement endpoint should support:

```text
GET /
PUT /
```

for the exact requirement URI:

```text
https://strictdoc-rm.oslc.ldsw.eu/?a=SDOC-HIGH-REQS-MANAGEMENT
```

Recommended HTTP behaviour:

```text
GET: return RDF/XML, Turtle, or JSON-LD
GET: include ETag
GET: include Link: <.../oslc/shapes/requirement>; rel="http://open-services.net/ns/core#instanceShape"
PUT: require If-Match where Jazz supplies it
PUT: validate immutable StrictDoc-owned properties
PUT: persist only allow-listed writable link predicates to the sidecar
PUT: return 200 OK for Jazz compatibility
```

`OPTIONS`/`Allow: GET, HEAD, OPTIONS, PUT` may help diagnostics, but the main
OSLC signal is RDF shape/update support.

Jazz failed on a successful `204 No Content` response in this link-save flow. An
empty `200 OK` response was verified to work. Do not infer that Jazz expects a
representation body on successful PUT; the interop requirement observed so far
is avoiding `204`.

Current live JSON-LD evidence for a requirement with a Jazz-created sidecar
link:

```json
{
  "@id": "https://strictdoc-rm.oslc.ldsw.eu/?a=SDOC-HIGH-REQS-DECOMP",
  "@type": ["http://open-services.net/ns/rm#Requirement"],
  "http://open-services.net/ns/core#instanceShape": [
    { "@id": "https://strictdoc-rm.oslc.ldsw.eu/oslc/shapes/requirement" }
  ],
  "http://open-services.net/ns/rm#affectedBy": [
    {
      "@id": "https://jazz.tail7abc.ts.net:9443/ccm/resource/itemName/com.ibm.team.workitem.WorkItem/72"
    }
  ]
}
```

## OSLC4Net relevance

The merged OSLC4Net PR
[OSLC/oslc4net#389](https://github.com/OSLC/oslc4net/pull/389) is relevant if we
generate the local shape from annotated CLR resources. It fixed URI collection
properties being detected as OSLC resource-valued shape properties.

That matters because link properties such as `affectedBy` are naturally
represented as collections of `Uri`. Without correct URI-collection shape
generation, the provider may fail to advertise:

```text
oslc:valueType oslc:Resource
oslc:representation oslc:Reference
oslc:occurs oslc:Zero-or-many
```

For Jazz interop, generated shape output must be inspected on the wire. The
question is not whether the C# model contains the property; the question is
whether Jazz sees the RDF shape declaring the property writable.

## Recommended next implementation step

Implement the protocol surface in this order:

1. Add a local `/oslc/shapes/requirement` RDF endpoint.
2. Point service-provider query capability `oslc:resourceShape` to that local
   shape.
3. Add `oslc:instanceShape` to every requirement RDF representation.
4. Add `oslc_rm:affectedBy` to the requirement model/output, initially empty
   when no sidecar links exist.
5. Add `PUT` on the requirement URI, but allow only sidecar link updates.
6. Persist sidecar links as described in [LINK_SIDECAR.md](./LINK_SIDECAR.md).
7. Re-test Jazz. The expected change is that the failed preflight becomes an
   actual PUT attempt.

## Implemented in this branch

The adapter now implements the discovery and minimal update parts of that
sequence:

- `/oslc/shapes/requirement` returns a provider-owned RDF/XML
  `oslc:ResourceShape`.
- The shape declares `oslc_rm:affectedBy` as `oslc:Zero-or-many`,
  `oslc:Reference`, `oslc:Resource`, and `oslc:readOnly false`.
- Requirement RDF includes `oslc:instanceShape <.../oslc/shapes/requirement>`.
- Service-provider query capabilities advertise the same local shape instead of
  the external RM spec shape URI.
- Direct requirement responses include `OSLC-Core-Version: 2.0`.
- CORS allows `PUT`.
- The requirement URI supports `PUT` for RDF/XML, Turtle, and N-Triples input.
- The PUT handler persists only allow-listed writable RM link predicates to a
  sidecar store.
- GET merges persisted sidecar links back into the OSLC requirement
  representation.

StrictDoc-owned fields remain read-only. The PUT path extracts only sidecar link
triples; it does not update the StrictDoc JSON export.
