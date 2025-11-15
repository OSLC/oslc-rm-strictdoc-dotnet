# OSLC Requirements Management server for StrictDoc

## Overview

[StrictDoc](https://github.com/strictdoc-project/strictdoc) is a simple yet rigorous tool for requirements management. It aims to provide a plaintext requirements management sufficient for demonstrating conformance with ISO 26262, DO178 and similar standards (when combined with Git for configuration management and human-followed processes otherwise stipulated).

This project provides a simple showcase of how an OSLC server for the Requirements Management domain could be added to the HTML output of the StrictDoc, which is a static site.

The main utility of such OSLC server would be to enable traceability with a higher fidelity than just pasting permanent links in the description field, e.g. in Jira.

## Getting started

### Run the OSLC server

The OSLC server is a .NET application that includes a static website hosting, which we leverage to reduce the number of moving parts in this solution. It uses [OSLC4Net](https://github.com/OSLC/oslc4net) for OSLC REST API implementation assistance.

Install .NET 9 (you may also need to trust the HTTPS cert dotnet installs for localhost) and run:

```
cd src/StrictDocOslcRmServer/StrictDocOslcRm
dotnet run
```

### (Optional) Rebuild the requirements site

Install [uv](https://docs.astral.sh/uv/) for Python first. Then run:

```sh
cd src/hellow-requirements/
uvx strictdoc export --formats html,json hello.sdoc
```


## Exploring the OSLC services

### Exploring the Docker build of the OSLC server

We will start with a [standardized](https://www.iana.org/assignments/well-known-uris/well-known-uris.xhtml) well-known endpoint for OSLC:

```sh
curl -X GET 'http://localhost:8080/.well-known/oslc/rootservices.xml'
```

We will then proceed to list the services in the OSLC catalog:

```sh
curl -X GET 'http://localhost:8080/oslc/catalog' \
  --header 'Accept: text/turtle;q=0.9, application/rdf+xml;q=0.7, application/ld+json;q=0.5, application/n-triples;q=0.3' \
  --header 'OSLC-Core-Version: 2.0'
```

In our implementation, one StrictDoc document is mapped to one OSLC Service Provider. We can obtain the metadata for a specific provider in the catalog:

```sh
curl -X GET 'http://localhost:8080/oslc/service_provider/e526fe19bd024f2ea7c84b9bccaf1243' \
  --header 'Accept: text/turtle;q=0.9, application/rdf+xml;q=0.7, application/ld+json;q=0.5, application/n-triples;q=0.3' \
  --header 'OSLC-Core-Version: 2.0'
```

We can proceed to query all requirements within a given OSLC provider (i.e, within a StrictDoc document):

```sh
curl -X GET 'http://localhost:8080/oslc/service_provider/e526fe19bd024f2ea7c84b9bccaf1243/requirements' \
  --header 'Accept: text/turtle;q=0.9, application/rdf+xml;q=0.7, application/ld+json;q=0.5, application/n-triples;q=0.3' \
  --header 'OSLC-Core-Version: 2.0'
```

That would produce the following response graph:


```turtle
@prefix rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>.
@prefix rdfs: <http://www.w3.org/2000/01/rdf-schema#>.
@prefix xsd: <http://www.w3.org/2001/XMLSchema#>.
@prefix dcterms: <http://purl.org/dc/terms/>.
@prefix oslc: <http://open-services.net/ns/core#>.

<http://localhost:8080/?a=SDOC-HIGH-REQS-DECOMP>
    a <http://open-services.net/ns/rm#Requirement>;
    <http://open-services.net/ns/rm#decomposes> <http://localhost:8080/?a=SDOC-HIGH-REQS-MANAGEMENT>;
    dcterms:identifier "SDOC-HIGH-REQS-DECOMP";
    dcterms:title "Requirements decomposition"^^rdf:XMLLiteral;
    dcterms:description "StrictDoc shall support requirement decomposition."^^rdf:XMLLiteral.

<http://localhost:8080/?a=SDOC-HIGH-REQS-MANAGEMENT>
    a <http://open-services.net/ns/rm#Requirement>;
    dcterms:identifier "SDOC-HIGH-REQS-MANAGEMENT";
    dcterms:title "Requirements management"^^rdf:XMLLiteral;
    dcterms:description "StrictDoc shall enable requirements management."^^rdf:XMLLiteral.
```

Opening the `http://localhost:8080/?a=SDOC-HIGH-REQS-DECOMP` in the browser navigates directly to the requirement within the corresponding document:

![](./docs/static/req-2-open.png)

### Exploring the debug build of the OSLC server

We will start with a [standardized](https://www.iana.org/assignments/well-known-uris/well-known-uris.xhtml) well-known endpoint for OSLC:

```sh
curl -X GET 'https://localhost:7000/.well-known/oslc/rootservices.xml'
```

We will then proceed to list the services in the OSLC catalog:

```sh
curl -X GET 'https://localhost:7000/oslc/catalog' \
  --header 'Accept: text/turtle;q=0.9, application/rdf+xml;q=0.7, application/ld+json;q=0.5, application/n-triples;q=0.3' \
  --header 'OSLC-Core-Version: 2.0'
```

In our implementation, one StrictDoc document is mapped to one OSLC Service Provider. We can obtain the metadata for a specific provider in the catalog:

```sh
curl -X GET 'https://localhost:7000/oslc/service_provider/e526fe19bd024f2ea7c84b9bccaf1243' \
  --header 'Accept: text/turtle;q=0.9, application/rdf+xml;q=0.7, application/ld+json;q=0.5, application/n-triples;q=0.3' \
  --header 'OSLC-Core-Version: 2.0'
```

We can proceed to query all requirements within a given OSLC provider (i.e, within a StrictDoc document):

```sh
curl -X GET 'https://localhost:7000/oslc/service_provider/e526fe19bd024f2ea7c84b9bccaf1243/requirements' \
  --header 'Accept: text/turtle;q=0.9, application/rdf+xml;q=0.7, application/ld+json;q=0.5, application/n-triples;q=0.3' \
  --header 'OSLC-Core-Version: 2.0'
```

That would produce the following response graph:


```turtle
@prefix rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>.
@prefix rdfs: <http://www.w3.org/2000/01/rdf-schema#>.
@prefix xsd: <http://www.w3.org/2001/XMLSchema#>.
@prefix dcterms: <http://purl.org/dc/terms/>.
@prefix oslc: <http://open-services.net/ns/core#>.

<https://localhost:7000/?a=SDOC-HIGH-REQS-DECOMP>
    a <http://open-services.net/ns/rm#Requirement>;
    <http://open-services.net/ns/rm#decomposes> <https://localhost:7000/?a=SDOC-HIGH-REQS-MANAGEMENT>;
    dcterms:identifier "SDOC-HIGH-REQS-DECOMP";
    dcterms:title "Requirements decomposition"^^rdf:XMLLiteral;
    dcterms:description "StrictDoc shall support requirement decomposition."^^rdf:XMLLiteral.

<https://localhost:7000/?a=SDOC-HIGH-REQS-MANAGEMENT>
    a <http://open-services.net/ns/rm#Requirement>;
    dcterms:identifier "SDOC-HIGH-REQS-MANAGEMENT";
    dcterms:title "Requirements management"^^rdf:XMLLiteral;
    dcterms:description "StrictDoc shall enable requirements management."^^rdf:XMLLiteral.
```

Opening the `https://localhost:7000/?a=SDOC-HIGH-REQS-DECOMP` in the browser navigates directly to the requirement within the corresponding document:

![](./docs/static/req-2-open.png)

## OSLC Resource Preview Support

This server implements [OSLC Resource Preview v3.0](https://docs.oasis-open-projects.org/oslc-op/core/v3.0/ps01/resource-preview.html), allowing client applications to display rich HTML previews of requirements without leaving their application context.

### Discovering Compact Resources

When requesting a requirement, the server includes a `Link` header pointing to its Compact resource:

```sh
curl -I 'https://localhost:7000/?a=SDOC-HIGH-REQS-DECOMP' \
  --header 'Accept: text/turtle'
```

Response includes:
```
Link: <https://localhost:7000/?a=SDOC-HIGH-REQS-DECOMP&compact>; rel="http://open-services.net/ns/core#Compact"
```

### Getting the Compact Resource

Request the Compact resource to get preview URLs and display metadata:

```sh
curl -X GET 'https://localhost:7000/?a=SDOC-HIGH-REQS-DECOMP&compact' \
  --header 'Accept: application/json'
```

Response:
```json
{
  "title": "Requirements decomposition",
  "shortTitle": "SDOC-HIGH-REQS-DECOMP",
  "icon": "https://localhost:7000/icons/requirement.svg",
  "iconTitle": "Requirement",
  "iconAltLabel": "Requirement",
  "smallPreview": {
    "document": "https://localhost:7000/?a=SDOC-HIGH-REQS-DECOMP&preview=small",
    "hintWidth": "320px",
    "hintHeight": "200px"
  },
  "largePreview": {
    "document": "https://localhost:7000/?a=SDOC-HIGH-REQS-DECOMP&preview=large",
    "hintWidth": "600px",
    "hintHeight": "400px"
  }
}
```

### Displaying Previews

Previews are HTML documents designed to be embedded in iframes. They use PicoCSS for clean, minimal styling:

**Small Preview** - Compact view with truncated description:
```sh
curl 'https://localhost:7000/?a=SDOC-HIGH-REQS-DECOMP&preview=small'
```

**Large Preview** - Full view with metadata and relationships:
```sh
curl 'https://localhost:7000/?a=SDOC-HIGH-REQS-DECOMP&preview=large'
```

### Implementation Details

- **Models**: `OslcCompactModels.cs` defines `Compact` and `Preview` resources with OSLC4Net annotations
- **Controller**: `RequirementController.cs` provides endpoints for Compact resources and HTML previews
- **Views**: Razor views in `Views/Requirement/` render small and large previews using PicoCSS v2
- **Standards Compliance**: Implements OSLC Core 3.0 Part 3: Resource Preview specification

### Client Integration Example

Client applications can embed previews in iframes:

```html
<iframe
  src="https://localhost:7000/?a=SDOC-HIGH-REQS-DECOMP&preview=small"
  width="320"
  height="200"
  style="border: 1px solid #ccc;">
</iframe>
```

