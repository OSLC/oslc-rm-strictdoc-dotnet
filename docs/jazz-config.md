# Jazz configuration-management notes

This document records implementation-relevant behavior found in the fetched
Jazz/CLM material. It is interoperability evidence, not a replacement for the
OSLC Configuration Management specification. The main design is
[oslc-config-initial.md](./oslc-config-initial.md); this document explains why
some specification-level SHOULD or MAY features should be treated as early
StrictDoc interoperability requirements when Jazz depends on them. The later
subtype/TRS/LDX/LQE implementation work is collected in
[oslc-config-extended.md](./oslc-config-extended.md).

## Conclusions for StrictDoc

### heliple2 branch used for the implementation audit

The relevant heliple2 source is the checked-out `CM` branch at
`/Users/ezandbe/code/a/phd/heliple2` (`99dd2a89`, tracking `origin/CM`). This
branch, rather than the default branch, contains the active Config-management
changes. Its live surface is:

- `RootServicesService` adds an `oslc:publisher` pointer and an
  `oslc_config:cmServiceProviders` pointer, while retaining the historical
  RM/CM/AM catalog pointers;
- `ScrService` adds the conventional `/scr` resource and points to
  `/rootservices` and `/application-about`;
- `PublisherService` exposes application metadata;
- `Application` registers `ServiceProvider6Service1` as the active Config
  query/selection service and binds provider 6 through
  `SAS_ComponentConfigurationRepositoryFactory`;
- `ServiceProvider6Repo` advertises
  `jfs_proc:globalConfigurationAware = "yes"`; and
- Config resource GET/compact/preview services remain available for generic
  `Configuration` and `Component` resources.

The branch still has no active Stream or Baseline REST service, no dedicated
Selections or VersionResource endpoint, no Stream/Baseline shape registration,
and no TRS or LinkProvider implementation. Its Config query is backed by
ShareAspace ApplicabilityContext data rather than a complete local
configuration graph.

The branch’s context behavior is also product-specific: it parses
`oslc.searchTerms` as `{search text} @ {applicability-context-oid}`, constructs
a base64 `sas-informationfilter` header, and embeds the context OID in
requirement URIs. This is useful evidence that all linked requirements and
queries must use the same context, but StrictDoc should translate the behavior
to the standard `Configuration-Context` header and `oslc_config.context` query
parameter rather than copy the URI scheme.

### Configuration is component-scoped

Jazz treats a component as the unit that owns versioned artifacts. A local RM
component has streams and baselines; a global configuration groups component
configurations. The first StrictDoc implementation should therefore expose one
local `oslc_config:Component` for the configured StrictDoc dataset, with its
streams and baselines beneath that component. Multiple StrictDoc components
can remain an explicit later decision rather than an accidental consequence of
the directory layout.

This maps naturally to the proposed layout:

```text
Component
  ├── Stream:   /data/{branch}/HEAD/
  └── Baseline: /data/{branch}/{tag}/   where {tag} != HEAD
```

### A default stream is practically required

The OSLC specification permits a server to reject a request without a
configuration context, but Jazz guidance assumes that an application without
a global-configuration protocol can provide a default local configuration.
StrictDoc should select a configured default stream, initially
`/data/{defaultBranch}/HEAD`, for requests without an explicit context. This
is an interoperability choice, not a claim that every OSLC server must do so.

### Streams and baselines have materially different behavior

A stream is mutable; a baseline is frozen and captures the state of the
component at a point in time. Jazz guidance uses baselines for releases,
milestones, stable downstream consumption, audit, and comparison. A baseline
must freeze the requirement export and its link state together. In particular,
the sidecar for a baseline must be a published snapshot, not a writable view
over the current stream sidecar.

The initial `HEAD`/non-`HEAD` mapping is therefore sound, provided the server
also exposes standard `Stream`, `Baseline`, and `VersionResource` metadata and
does not infer mutability from a client-supplied flag.

### Configuration-aware links are directional in Jazz

The strongest interoperability finding is the link-storage rule: with
configuration management enabled, Jazz stores a link only with the source or
owner artifact, publishes that owned link through TRS to the Link Index
Provider (LDX), and lets the target application discover incoming links in the
current configuration context. It does not maintain a second authoritative
backlink on the target.

Consequences for StrictDoc:

- The primary predicate belongs to the owning application, as extracted in
  [link-ownership.md](./link-ownership.md).
- A requirement representation may expose incoming secondary predicates, but
  those values are a discovered view, not a second canonical assertion.
- The current five-predicate sidecar allowlist is useful for the existing Jazz
  link-save compatibility path. In a stricter configuration mode it should be
  described as a compatibility store until StrictDoc has an owner-side link
  publication/discovery service.
- A baseline must never be changed merely to materialize an incoming backlink.

The first milestone can keep the current sidecar contract because the request
specifically needs link-only stream PUT. The implementation should retain a
clear mode boundary: compatibility sidecar writes now; owner-side storage plus
context-aware link discovery as the eventual canonical Config-profile model.

### Context is required on all versioned-resource interactions

Jazz guidance says the correct local or global configuration context must be
used when creating, displaying, modifying, or deleting a versioned artifact.
The same context affects link creation and link resolution. A link can be
present in the link index but invisible in a different stream or baseline
because the target version is not selected there.

This makes context propagation a cross-cutting concern, not only a requirement
GET feature. StrictDoc must carry the resolved context through direct GET/PUT,
OSLC Query, compact and preview responses, selection dialogs, link reads, and
cache validators.

### Jazz needs configuration selection and integration metadata

The Jazz integration guidance calls out configuration selection UI and the
ability to pass a selected context to the provider. For future participation in
global configurations it also describes provider discovery, an
`oslc_config:acceptedBy oslc_config:Configuration` relationship, and delegated
configuration selection/creation UI.

The initial StrictDoc scope can remain local-only, but it should expose the
standard component/configuration resources and a delegated selection UI early.
Global configuration composition, contribution traversal, and LDX/TRS are
larger features; they should be deferred explicitly rather than omitted from
the interoperability model.

When owner-side link publication and discovery are implemented, the provider
should also evaluate the Jazz-facing service-provider capabilities
`supportContributionsToLinkIndexProvider` and
`supportLinkDiscoveryViaLinkIndexProvider`. A sidecar alone is not sufficient
reason to advertise either capability; advertising them should mean that the
corresponding TRS/LDX behavior is actually available.

## Jazz-informed implementation priorities

| Capability | OSLC specification level | Jazz evidence | StrictDoc priority |
|---|---|---|---|
| Header and query configuration context | MUST support both methods | Context is used for versioned artifact resolution | First milestone |
| Default local stream | Server may provide one | Needed when no global protocol is available | First milestone |
| Component, streams, baselines | Core configuration resource model | Component is the local ownership unit | First milestone |
| Baseline immutability | Baseline is immutable | Used for release, audit, and stable consumption | First milestone |
| Configuration selection UI | Required for config servers | Jazz users select local/global context through UI | First milestone |
| Context-aware compact/preview | SHOULD, if implemented MUST honor context | UI navigation and previews follow current context | First milestone |
| Directional link ownership | Linking Profile / Jazz behavior | Owner stores; target discovers through LDX | Policy now; discovery later |
| TRS/LDX link discovery | Product integration behavior | Required for complete incoming-link behavior | Later, with explicit compatibility mode |
| Global configuration contribution | Integration capability | Needed to participate in GCM hierarchies | Later |
| Change sets and version-resource mutation | Optional/advanced | Useful for full lifecycle management | Later |

The table is intentionally conservative about heliple2 features. A generated
endpoint is not automatically a StrictDoc requirement. Conversely, a Jazz
integration behavior that appears as a SHOULD or MAY in a generic OSLC surface
is elevated here when the fetched Jazz material makes it part of the expected
workflow.

## Stream and baseline strategy

The Jazz planning material recommends starting with the simplest stream
strategy that meets the product need: use few streams, create branches only for
parallel work or meaningful variants, baseline at releases or milestones, and
archive configurations that are no longer needed. It also emphasizes stable
naming for components, streams, baselines, and change sets.

For the initial StrictDoc adapter this suggests:

1. Use one component and one default stream for the first deployment.
2. Treat each `{branch}` as an intentional stream, not as an arbitrary path
   segment.
3. Publish a baseline directory atomically from a stream snapshot.
4. Preserve the same link vocabulary and RDF URIs across streams and
   components.
5. Do not implement global hierarchy or delivery semantics until a concrete
   Jazz/GCM scenario requires them.

## Source notes

The following local files were supplied as the fetched Jazz material. The
online links identify the original articles where a stable URL was available.

- `CLMCfgMRecommendedPractices _ Deployment _ TWiki.html` — [CLM/CE
  configuration-management recommended practices](https://jazz.net/wiki/bin/view/Deployment/CLMCfgMRecommendedPractices).
  Useful for component boundaries, default configurations, naming, stream and
  baseline strategy, and directional-link integration guidance.
- `ConfigurationManagementFAQ _ Deployment _ TWiki.html` — [Configuration
  Management FAQ](https://jazz.net/wiki/bin/view/Deployment/ConfigurationManagementFAQ).
  Useful for the distinction between components, streams, baselines, and the
  no-backlink link-storage rule.
- `IntegratingWithConfigurationManagementEnabledCLMApplications _ Deployment _
  TWiki.html` — [Integrating with configuration-management-enabled CLM
  applications](https://jazz.net/wiki/bin/view/Deployment/IntegratingWithConfigurationManagementEnabledCLMApplications).
  Useful for context propagation, configuration selection UI, delegated
  integration, and TRS/LDX expectations.
- `Configuring Rational Team Concert to establish a global configuration
  context for work item links - Library_ Articles - Jazz Community Site.html` —
  [RTC global context for work item links](https://jazz.net/library/article/92499).
  Useful evidence that the current global context controls creation and
  resolution, and that links can disappear when a different version is
  selected.
- `Best practices for Managing Baselines with IBM Doors Next.html` — [Best
  practices for managing baselines with IBM DOORS
  Next](https://www.sodiuswillert.com/en/blog/best-practices-for-managing-baselines-with-ibm-doors-next).
  Useful practical explanation of immutable baselines, streams, audit, and
  configuration-aware traceability.
- `CLM configuration management_ Defining your component strategy - Library_
  Articles - Jazz Community Site.html` — [Defining your component
  strategy](https://jazz.net/library/article/90573). Useful for component
  granularity and reuse decisions.
- `CLM configuration management_ Patterns for stream usage - Library_ Articles
  - Jazz Community Site.html` — [Patterns for stream
  usage](https://jazz.net/library/article/90581). Useful for the recommendation
  to begin with the simplest viable stream strategy.
- `CLM configuration management_ Single stream strategy - Library_ Articles -
  Jazz Community Site.html` — [Single stream
  strategy](https://jazz.net/library/article/90591). Useful as the closest
  operational model for a first StrictDoc deployment.

The fetched copies are under:

```text
/Users/ezandbe/Downloads/jazz_gcm/
```

That path is an audit trail for this design session; implementations should
use the linked source documents or a versioned local evidence bundle rather
than depending on a developer-specific Downloads path.
