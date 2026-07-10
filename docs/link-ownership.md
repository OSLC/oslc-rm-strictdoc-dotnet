# OSLC link direction and ownership

This document extracts the link direction and storage-ownership rules from the
OSLC Linking Profiles note and applies them to the StrictDoc RM adapter. The
profile is a non-normative interoperability supplement; the table below is an
interoperability policy, not a replacement for the OSLC domain specifications.

Source: [`link-profiles.md`](../../../oslc-op/oslc-specs/notes/linking-profiles/link-profiles.md),
especially the `Link Ownership` section and its ownership table. The published
version is [OSLC Linking Profiles 1.0](https://docs.oasis-open-projects.org/oslc-op/linking-profiles/v1.0/linking-profiles.html).

## Vocabulary

For a link assertion:

```text
source --primary predicate--> target
target --secondary predicate--> source
```

The source-side primary predicate is the directional assertion. The secondary
predicate is an inverse-shaped view used for navigation or compatibility; OSLC
does not define inverse-property entailment. The profile convention is that a
link is stored once, on the source/owner side, and that the other side discovers
the incoming link rather than storing a duplicate.

This matters for configuration management: an owner-side update must be able to
create a link to a resource in a baseline without modifying the target version.
Duplicating the assertion on both resources would create synchronization and
immutability problems.

## Profile ownership table

The following table is transcribed from the Linking Profiles note. “Primary” is
the predicate that belongs to the source/owner domain. “Secondary” is the
predicate exposed from the target side when that inverse view is defined.

| Source/owner domain | Primary predicate | Target domain | Secondary predicate |
|---|---|---|---|
| RM | `oslc_rm:constraints` | RM | `oslc_rm:constrainedBy` |
| RM | `oslc_rm:decomposes` | RM | `oslc_rm:decomposedBy` |
| RM | `oslc_rm:elaborates` | RM | `oslc_rm:elaboratedBy` |
| RM | `oslc_rm:satisfies` | RM | `oslc_rm:satisfiedBy` |
| RM | `oslc_rm:specifies` | RM | `oslc_rm:specifiedBy` |
| RM | `oslc_rm:uses` | RM | unspecified |
| QM | `oslc_qm:validatesRequirement` | RM | `oslc_rm:validatedBy` |
| QM | `oslc_qm:validatesRequirementCollection` | RM | `oslc_rm:validatedBy` |
| CM | `oslc_cm:implementsRequirement` | RM | `oslc_rm:implementedBy` |
| CM | `oslc_cm:tracksRequirement` | RM | `oslc_rm:trackedBy` |
| CM | `oslc_cm:affectsRequirement` | RM | `oslc_rm:affectedBy` |
| CM | `oslc_cm:testedByTestCase` | QM | `oslc_qm:testsChangeRequest` |
| CM | `oslc_cm:relatedTestScript` | QM | `oslc_qm:relatedChangeRequest` |
| CM | `oslc_cm:relatedTestCase` | QM | `oslc_qm:relatedChangeRequest` |
| CM | `oslc_cm:relatedTestPlan` | QM | `oslc_qm:relatedChangeRequest` |
| CM | `oslc_cm:relatedTestExecutionRecord` | QM | `oslc_qm:relatedChangeRequest` |
| CM | `oslc_cm:blocksTestExecutionRecord` | QM | `oslc_qm:blockedByChangeRequest` |
| CM | `oslc_cm:affectsTestResult` | QM | `oslc_qm:affectedByChangeRequest` |
| CM | `oslc_cm:affectedByDefect` | CM | `oslc_cm:affectsPlanItem` |
| CM | `oslc_cm:tracksChangeSet` | `oslc_config:ChangeSet` | unspecified |
| CM | `oslc_cm:relatedChangeRequest` | CM | unspecified |
| AM | `jazz_am:derives` | Any | unspecified |
| AM | `jazz_am:elaborates` | Any | unspecified |
| AM | `jazz_am:external` | Any | unspecified |
| AM | `jazz_am:refine` | Any | unspecified |
| AM | `jazz_am:satisfy` | Any | unspecified |
| AM | `jazz_am:trace` | Any | unspecified |

All rows are directional. For example:

```text
TestCase --oslc_qm:validatesRequirement--> Requirement
Requirement --oslc_rm:validatedBy--> TestCase
```

The profile recommends that the first assertion is physically stored by QM and
that RM obtains the incoming view through discovery or query. A link may still
be initiated from either end: the initiating application must update the
source/owner application using the primary predicate, or use the secondary
predicate when the owner-side update protocol requires it. This matches the
Jazz guidance recorded in [jazz-config.md](./jazz-config.md): configuration
management uses directional links with no authoritative duplicate backlink.

## StrictDoc RM policy

StrictDoc is an RM provider. The following distinction is important:

1. The profile says that RM owns the RM primary predicates.
2. The StrictDoc JSON export remains authoritative for requirement content and
   structural relations.
3. The sidecar is the only writable persistence layer exposed by this adapter.
4. A sidecar assertion must never silently replace or delete a StrictDoc JSON
   assertion.

The initial adapter policy should therefore be explicit rather than “all RDF is
writable”:

| Predicate group | Profile ownership | Initial StrictDoc treatment |
|---|---|---|
| `oslc_rm:constraints`, `oslc_rm:decomposes`, `oslc_rm:elaborates`, `oslc_rm:satisfies`, `oslc_rm:specifies`, `oslc_rm:uses` | StrictDoc’s RM source side | Keep read-only when generated from StrictDoc content. Add sidecar-backed writes only after additive/replace/delete semantics are defined for each property. |
| `oslc_rm:affectedBy` | CM primary, RM secondary | Allow as a sidecar link for the Jazz compatibility flow already observed. |
| `oslc_rm:implementedBy` | CM primary, RM secondary | Allow as a sidecar link. |
| `oslc_rm:trackedBy` | CM primary, RM secondary | Allow as a sidecar link. |
| `oslc_rm:validatedBy` | QM primary, RM secondary | Allow as a sidecar link, including validation links from a QM resource or collection. |
| `oslc_rm:satisfiedBy` | RM primary on the other RM resource, RM secondary here | Allow as a sidecar link when StrictDoc is the target-side view. |
| `oslc_rm:constrainedBy`, `oslc_rm:decomposedBy`, `oslc_rm:elaboratedBy`, `oslc_rm:specifiedBy` | RM secondary | Keep disabled until the adapter has tests for owner-side storage and PUT replacement semantics; then add them as explicit predicates, not through a wildcard. |

The current implementation already allow-lists five sidecar predicates
(`affectedBy`, `implementedBy`, `trackedBy`, `validatedBy`, and `satisfiedBy`).
It does not allow arbitrary RDF, which is the right safety boundary. The
configuration-management work should move this list into a named policy or
registry so that predicate ownership, whether the predicate is generated by
StrictDoc, and whether it is writable in a stream or baseline are reviewable
data rather than scattered conditionals.

These five secondary predicates are an interoperability compatibility mode for
the current Jazz link-save flow, not proof that StrictDoc owns the corresponding
links. In a strict configuration-management mode, the canonical assertion
should remain with the source/owner application and the incoming value should
be resolved through context-aware link discovery. Keeping the distinction
explicit prevents a writable sidecar from becoming a second authoritative
backlink store.

## PUT and storage rules

For a stream (`tag = HEAD`):

- accept only the explicit sidecar predicate set;
- require the RDF subject to equal the exact requirement URI;
- reject changes to identifiers, titles, descriptions, types, shapes, and
  StrictDoc-generated structural links;
- store only the accepted link assertions and their required metadata closure;
- return the link-only representation on GET by merging StrictDoc RDF and the
  context-specific sidecar.

For a baseline (`tag != HEAD`), the published JSON and sidecar are immutable.
PUT must not mutate either file. If a compatibility requirement later demands a
baseline-side link update, that needs a new versioned configuration or a formal
change-set design; it must not turn a baseline sidecar into a mutable exception.

The sidecar remains configuration-scoped:

```text
/data/{branch}/{tag}/sidecar.json
```

The same resource URI may therefore have different link graphs in different
streams. A GET or PUT must resolve the context before loading or writing the
sidecar.
