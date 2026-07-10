# URI fragment equality probe

## Scope

This is a small executable check of URI equality semantics for OSLC/RDF
predicates such as:

```text
http://open-services.net/ns/rm#affectedBy
http://open-services.net/ns/rm#trackedBy
```

These two URIs differ only by fragment. For RDF, they are different resources
and must not collapse.

The first pass covers:

- .NET `System.Uri`
- dotNetRDF `IUriNode`
- Java `java.net.URI`
- Java `java.net.URL`
- Apache Jena `Node_URI`
- Python `urllib.parse`
- JavaScript WHATWG `URL`
- Go `net/url.URL`
- Rust `url::Url`

## Commands

```sh
dotnet run --project docs/experiments/uri-fragment/dotnet/UriFragmentProbe.csproj
mvn -q -f docs/experiments/uri-fragment/java/pom.xml compile exec:java -Dexec.mainClass=UriFragmentProbe
python3 docs/experiments/uri-fragment/python/uri_fragment_probe.py
node docs/experiments/uri-fragment/javascript/uri_fragment_probe.mjs
go run docs/experiments/uri-fragment/go/uri_fragment_probe.go
cargo run --manifest-path docs/experiments/uri-fragment/rust/Cargo.toml --quiet
```

Environment:

```text
.NET SDK: 10.0.301
dotNetRDF: 3.5.1
Java: Temurin 21.0.11
Jena: 6.1.0
Python: 3.14.6
Node.js: 26.3.0
Go: 1.26.4
Rust: 1.96.0
Rust url crate: 2.5.8
```

## .NET result

```text
.NET System.Uri
type=System.Uri
a=http://open-services.net/ns/rm#affectedBy fragment=#affectedBy
b=http://open-services.net/ns/rm#trackedBy fragment=#trackedBy
a.Equals(b)=True
a == b=True
hash(a)=-650582172 hash(b)=-650582172
dotNetRDF EqualityHelper.AreUrisEqual(a,b)=False
HashSet<Uri>.Count=1
HashSet<string>.Count=2
graph.UriFactory type=VDS.RDF.CachingUriFactory
graph.UriFactory.InternUris=True
UriFactory.Root type=VDS.RDF.CachingUriFactory
UriFactory.InternUris=True
ReferenceEquals(UriFactory.Create same string)=True
ReferenceEquals(graph.UriFactory.Create same string)=True
ReferenceEquals(rootUri, graphUri)=True

dotNetRDF IUriNode
node type=VDS.RDF.UriNode
native uri type=System.Uri
nodeA.Uri=http://open-services.net/ns/rm#affectedBy
nodeB.Uri=http://open-services.net/ns/rm#trackedBy
nodeA.Equals(nodeB)=False
nodeA.Uri.Equals(nodeB.Uri)=True
HashSet<IUriNode>.Count=2
nodeAFromFactoryString.Equals(nodeBFromFactoryString)=False
nodeAFromFactoryString.Uri.Equals(nodeBFromFactoryString.Uri)=True
HashSet<IUriNode> from UriFactory strings.Count=2
CreateUriNode(string full URI) throws RdfException: The Namespace URI for the given Prefix 'http' is not known by the in-scope NamespaceMapper.  Did you forget to define a namespace for this prefix?
HashSet<Uri>(lexical comparer).Count=2
Graph triple count after two fragment-distinct predicates=2
TriplesWithPredicate(affectedBy)=1
TriplesWithPredicate(trackedBy)=1
```

Findings:

- `System.Uri.Equals` and `System.Uri ==` ignore fragment-only differences for
  these HTTP URIs.
- `HashSet<Uri>` collapses the two OSLC predicate URIs into one item.
- dotNetRDF has an RDF-aware `EqualityHelper.AreUrisEqual(Uri, Uri)` that treats
  the two URIs as different.
- dotNetRDF exposes `IUriNode.Uri` as `System.Uri`.
- dotNetRDF `IUriNode.Equals` remains fragment-sensitive.
- dotNetRDF graph storage/querying keeps the two predicates distinct.
- dotNetRDF node equality is documented as ordinal string comparison of the
  string form of the URI.
- The default graph URI factory is `VDS.RDF.CachingUriFactory` with
  `InternUris=True`.
- `UriFactory.Root` is a static app-domain root factory.
- `UriFactory.Create(sameString)` returns the same interned `Uri` instance when
  interning is enabled.
- The default graph factory and root factory shared the same interned URI
  instance in this probe.
- `graph.CreateUriNode(string)` is not the right API for a full URI string. It
  resolves QNames using the graph namespace map.
- For a full URI string, create a `System.Uri` through the graph/root URI
  factory and then create the node, for example:

  ```csharp
  IUriNode node = graph.CreateUriNode(graph.UriFactory.Create(uriString));
  ```

  or:

  ```csharp
  IUriNode node = graph.CreateUriNode(new Uri(uriString));
  ```

  The resulting node is safe for RDF equality. The contained `node.Uri` is still
  unsafe if used directly as a `HashSet<Uri>` key.

Practical consequence: OSLC4Net and adapter code must not use `System.Uri`
equality/hash semantics for RDF identity. Use exact lexical URI strings,
dotNetRDF nodes, or an explicit comparer based on `AbsoluteUri`/original string.

## .NET construction cost probe

This is not BenchmarkDotNet; it is a rough single-process probe using
`Stopwatch` and `GC.GetAllocatedBytesForCurrentThread` over 100,000 operations.
It is enough to compare broad costs, not to publish exact nanosecond numbers.

```text
new Uri(string): 23 ms, 175.9 bytes/op
graph.UriFactory.Create(string): 462 ms, 486.8 bytes/op
graph.CreateUriNode(new Uri(string)): 92 ms, 426.1 bytes/op
graph.CreateUriNode(graph.UriFactory.Create(string)): 126 ms, 511.9 bytes/op
new RdfUri(string): 5 ms, 119.9 bytes/op

new Uri(string), repeated: 5 ms, 56.0 bytes/op
graph.UriFactory.Create(string), repeated: 28 ms, 144.0 bytes/op
graph.CreateUriNode(new Uri(string)), repeated: 23 ms, 296.0 bytes/op
graph.CreateUriNode(graph.UriFactory.Create(string)), repeated: 34 ms, 176.0 bytes/op
new RdfUri(string), repeated: 0 ms, 0.0 bytes/op
HashSet<RdfUri>.Count=2
```

Observations:

- A dotNetRDF `IUriNode` necessarily adds object overhead on top of URI
  construction.
- `UriFactory.Create(string)` is not free. With unique strings, the
  caching/interning machinery costs more than `new Uri(string)` in this rough
  probe.
- With repeated strings, `UriFactory` reduces allocation when creating URI nodes
  because it can reuse the URI instance, but it still has lookup overhead.
- A string-backed value type such as `RdfUri` is much cheaper for domain-model
  storage, especially when it wraps an existing string.
- Therefore, do not store `IUriNode` in OSLC4Net model classes just for
  performance. Construct dotNetRDF nodes at serialization/query boundaries.

## Java result

```text
Java java.net.URI
type=java.net.URI
a=http://open-services.net/ns/rm#affectedBy fragment=affectedBy
b=http://open-services.net/ns/rm#trackedBy fragment=trackedBy
a.equals(b)=false
hash(a)=2037276512 hash(b)=1782259926
HashSet<URI>.Count=2

Java java.net.URL
type=java.net.URL
a=http://open-services.net/ns/rm#affectedBy ref=affectedBy
b=http://open-services.net/ns/rm#trackedBy ref=trackedBy
a.equals(b)=false
hash(a)=-1850597192 hash(b)=672313326
HashSet<URL>.Count=2

Apache Jena Node
node type=org.apache.jena.graph.Node_URI
native URI value type=java.lang.String
nodeA.getURI()=http://open-services.net/ns/rm#affectedBy
nodeB.getURI()=http://open-services.net/ns/rm#trackedBy
nodeA.equals(nodeB)=false
HashSet<Node>.Count=2
Model statement count after two fragment-distinct predicates=2
StatementsWithPredicate(affectedBy)=1
StatementsWithPredicate(trackedBy)=1
```

Findings:

- `java.net.URI` equality is fragment-sensitive.
- `java.net.URL` equality is fragment-sensitive in this test.
- Jena URI nodes use `java.lang.String` as the native URI value exposed by
  `Node.getURI()`.
- Jena node/model equality keeps the two predicates distinct.

Practical consequence: Jena avoids this specific .NET problem because RDF URI
identity is represented lexically as a string at the Jena node level.

## Python result

```text
Python urllib.parse.urlparse
type=urllib.parse.ParseResult
a=http://open-services.net/ns/rm#affectedBy fragment=affectedBy
b=http://open-services.net/ns/rm#trackedBy fragment=trackedBy
a == b=False
set(ParseResult).count=2

Python urllib.parse.urlsplit
type=urllib.parse.SplitResult
a=http://open-services.net/ns/rm#affectedBy fragment=affectedBy
b=http://open-services.net/ns/rm#trackedBy fragment=trackedBy
a == b=False
set(SplitResult).count=2
set(str).count=2
```

Finding: Python standard parsed URL tuple types include the fragment in
equality/hash semantics.

## JavaScript result

```text
JavaScript WHATWG URL
type=URL
a=http://open-services.net/ns/rm#affectedBy hash=#affectedBy
b=http://open-services.net/ns/rm#trackedBy hash=#trackedBy
a === b=false
a.href === b.href=false
a === a2=false
a.href === a2.href=true
Set<URL>.size=2
Set<URL> same lexical value size=2
Set<string href>.size=2
```

Finding: JavaScript `URL` objects compare by object identity, not lexical URL
value. Fragment-distinct URLs do not collapse, but duplicate lexical URLs also
do not collapse if stored as `URL` objects. Use `url.href` strings as map/set
keys for RDF identity.

## Go result

```text
Go net/url.URL
type=url.URL
a=http://open-services.net/ns/rm#affectedBy fragment=affectedBy
b=http://open-services.net/ns/rm#trackedBy fragment=trackedBy
reflect.DeepEqual(a,b)=false
*a == *b=false
map[url.URL] count=2
map[*url.URL] count=2
map[string] count=2
```

Finding: Go `url.URL` value equality includes the fragment in this case.
Pointer-keyed maps should still be avoided for RDF identity because they are
object identity, not URI identity.

## Rust result

```text
Rust url::Url
type=url::Url
a=http://open-services.net/ns/rm#affectedBy fragment=Some("affectedBy")
b=http://open-services.net/ns/rm#trackedBy fragment=Some("trackedBy")
a == b=false
HashSet<Url>.count=2
HashSet<String>.count=2
```

Finding: Rust `url::Url` equality/hash semantics include the fragment.

## Cross-platform conclusion

The mismatch is specifically dangerous in .NET code that stores RDF identifiers
in `System.Uri` collections or dictionaries.

Safe:

- `HashSet<string>(StringComparer.Ordinal)` over full URI strings.
- dotNetRDF `IUriNode` equality.
- Jena `Node_URI` equality.
- Jena string URI values.
- Java `URI`.
- Python `ParseResult`/`SplitResult`.
- Go `url.URL` values.
- Rust `url::Url`.

Unsafe:

- `HashSet<Uri>` for RDF resources.
- `Dictionary<Uri, ...>` for RDF resources.
- Comparing OSLC predicates with `System.Uri.Equals` or `==`.
- JavaScript `Set<URL>`/`Map<URL, ...>` when duplicate lexical URI detection
  matters, because object identity is used.
- Go pointer-keyed `map[*url.URL]...` when duplicate lexical URI detection
  matters, because pointer identity is used.

## OSLC4Net follow-up

OSLC4Net APIs currently expose many OSLC link and property values as
`System.Uri`. That is acceptable as a transport/container type only if OSLC4Net
never relies on default `System.Uri` equality for RDF identity.

Required follow-ups:

- Audit `HashSet<Uri>`, `Dictionary<Uri, ...>`, `Distinct()`, `GroupBy()`, and
  direct `Uri ==`/`Uri.Equals` usage where values are RDF resource identifiers.
- Introduce an RDF/OSLC URI comparer using exact lexical identity.
- Consider a small `OslcUri`/`RdfUri` value object or a shared
  `IEqualityComparer<Uri>` that compares full serialized URI strings including
  fragments.
- Keep dotNetRDF node-level operations as the RDF identity source where
  possible.
- Document this .NET `System.Uri` fragment-equality trap in OSLC4Net docs.

## Recommended OSLC4Net type direction

For OSLC4Net, the clean conceptual model is:

```text
RDF identity = exact URI lexical value
RDF node identity = dotNetRDF IUriNode
.NET URL transport/parser = System.Uri only at boundaries
```

Recommended value type:

- Introduce an OSLC/RDF URI value type, e.g. `RdfUri` or `OslcUri`.
- Store the canonical lexical URI string internally. Do not store
  `ReadOnlySpan<char>`.
- Implement equality/hash using `StringComparer.Ordinal` over that lexical
  value.
- Provide conversions:
  - `RdfUri.FromString(string)`
  - `RdfUri.FromUri(Uri)` using `uri.AbsoluteUri`
  - `Uri ToUri()` for APIs that need `System.Uri`
  - `IUriNode ToUriNode(IGraph graph)` using
    `graph.CreateUriNode(graph.UriFactory.Create(value))`

Why not `ReadOnlySpan<char>`:

- `ReadOnlySpan<char>` is a `ref struct`; it cannot be stored in normal heap
  objects, cannot be used as `HashSet<T>` element type, and cannot be exposed as
  an ordinary model property.
- Span lifetime is tied to the backing memory. Model objects need stable, owned
  identity values.
- `ReadOnlyMemory<char>` can be stored, but equality/hashing over memory slices
  is awkward, backing memory may still be mutable depending on origin, and a
  defensive copy usually turns it back into a string-like owned value.
- URI identity is long-lived domain data, not a transient parsing buffer. A
  string-backed value is the right .NET shape.

Why not dotNetRDF `IUriNode` in model classes:

- It leaks the RDF provider into OSLC domain models.
- It is graph/factory-oriented and belongs at RDF serialization/query
  boundaries.
- It still wraps `System.Uri`, so consumers can still accidentally fall back to
  unsafe `node.Uri` equality.
- It is heavier than a lexical value for plain domain storage.

Where `QName` fits:

- QName is useful for compact predicate names and namespace-mapped metadata.
- QName is not a general RDF resource identifier because its meaning depends on
  an external prefix map.
- Subjects and objects must be URI/IRI lexical identifiers, not QNames.
- Predicates may be displayed or configured as QNames, but should be normalized
  to full lexical URI identity before equality/hash operations.

Recommended collection type for OSLC link properties:

- Internally: `HashSet<RdfUri>` or `HashSet<Uri>` with an explicit lexical
  comparer.
- Public mutable model properties under current OSLC4Net serializer constraints:
  prefer concrete `HashSet<RdfUri>` or `ISet<RdfUri>` once the serializer
  supports `RdfUri`.
- Public read-only API surface: expose `IReadOnlyCollection<RdfUri>` or
  `IReadOnlySet<RdfUri>` where mutation should not be public.

Current OSLC4Net serializer caveat:

- Deserialization already detects `ISet<T>` and activates `HashSet<T>`.
- Serialization paths currently look for `ICollection<T>`.
- `ISet<T>` extends `ICollection<T>`, so it fits the current implementation.
- `IReadOnlyCollection<T>` and `IReadOnlySet<T>` do not extend `ICollection<T>`,
  so switching annotated model properties directly to read-only interfaces
  likely needs serializer changes first.

Pragmatic migration path:

1. Add a lexical `IEqualityComparer<Uri>` for transitional code and use
   dotNetRDF `EqualityHelper.AreUrisEqual` where direct equality checks are
   needed.
2. Direct all OSLC4Net URI construction through
   `UriFactory.Create(string)`/`UriFactory.Root.Create(string)` where dotNetRDF
   is already a dependency. This preserves URI interning and avoids repeated
   `new Uri(...)` instances for repeated RDF identifiers.
3. Replace internal `HashSet<Uri>` and `Dictionary<Uri, ...>` RDF identity usage
   first.
4. Consider `RdfUri`/`OslcUri` only if OSLC4Net wants provider-neutral domain
   classes that do not depend on dotNetRDF at all.
5. Keep model properties as concrete `HashSet<...>` or `ISet<...>` until
   serialization supports read-only collection interfaces.
6. Add serializer support for `IEnumerable<T>`/`IReadOnlyCollection<T>` output.
7. Only then consider changing public generated model properties to read-only
   interfaces.

Performance note:

- For hot paths, keep concrete `HashSet<T>` internally. Interface dispatch
  through `IReadOnlyCollection<T>`/`ISet<T>` is usually not the main cost here,
  but concrete types give the JIT more room and avoid accidental abstraction
  overhead.
- Use read-only interfaces primarily as API contracts, not as the internal
  storage type.

## Immediate OSLC4Net audit points

The current tree contains usages that should be reviewed:

- Model properties such as `ISet<Uri>` backed by `HashSet<Uri>`.
- `ResourceShapeFactory` type mappings for `ISet<Uri>`/`IEnumerable<Uri>`.
- Client logic comparing `IUriNode.Uri == someUri` or
  `IUriNode.Uri.Equals(...)`.
- Any `Dictionary<Uri, ...>`, `Distinct()`, or `GroupBy()` over RDF/OSLC
  identifiers.

For transitional fixes where a full `RdfUri` migration is too large, use an
explicit comparer:

```csharp
sealed class RdfUriComparer : IEqualityComparer<Uri>
{
    public bool Equals(Uri? x, Uri? y) =>
        x is null ? y is null :
        y is not null && string.Equals(x.AbsoluteUri, y.AbsoluteUri, StringComparison.Ordinal);

    public int GetHashCode(Uri obj) =>
        StringComparer.Ordinal.GetHashCode(obj.AbsoluteUri);
}
```

or use dotNetRDF's `EqualityHelper.AreUrisEqual(x, y)` for equality and pair it
with an ordinal-string hash over `AbsoluteUri`.
