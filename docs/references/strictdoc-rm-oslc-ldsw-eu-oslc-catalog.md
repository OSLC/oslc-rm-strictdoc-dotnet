---
url: "https://strictdoc-rm.oslc.ldsw.eu/oslc/catalog"
title: "https://strictdoc-rm.oslc.ldsw.eu/oslc/catalog"
fetched_at: "2026-06-23T17:55:57.199738+00:00"
status: 200
content_type: "application/rdf+xml; charset=utf-8"
---

# https://strictdoc-rm.oslc.ldsw.eu/oslc/catalog

```text
<?xml version="1.0" encoding="utf-8"?>
<rdf:RDF xmlns:rdfs="http://www.w3.org/2000/01/rdf-schema#" xmlns:xsd="http://www.w3.org/2001/XMLSchema#" xmlns:dcterms="http://purl.org/dc/terms/" xmlns:oslc="http://open-services.net/ns/core#" xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
  <oslc:ServiceProviderCatalog rdf:about="https://strictdoc-rm.oslc.ldsw.eu/oslc/catalog">
    <oslc:domain rdf:resource="http://open-services.net/ns/rm#" />
    <oslc:serviceProvider rdf:resource="https://strictdoc-rm.oslc.ldsw.eu/oslc/service_provider/e526fe19bd024f2ea7c84b9bccaf1243" />
    <dcterms:description rdf:parseType="Literal">Service provider catalog for the StrictDoc Requirements Management server</dcterms:description>
    <dcterms:title rdf:parseType="Literal">StrictDoc Requirements Management Service Provider Catalog</dcterms:title>
  </oslc:ServiceProviderCatalog>
  <oslc:ServiceProvider rdf:about="https://strictdoc-rm.oslc.ldsw.eu/oslc/service_provider/e526fe19bd024f2ea7c84b9bccaf1243">
    <oslc:details rdf:resource="https://strictdoc-rm.oslc.ldsw.eu/oslc/service_provider/e526fe19bd024f2ea7c84b9bccaf1243" />
    <oslc:service rdf:nodeID="autos67" />
    <dcterms:description rdf:parseType="Literal">OSLC Requirements Management service for StrictDoc document: StrictDoc</dcterms:description>
    <dcterms:identifier rdf:datatype="http://www.w3.org/2001/XMLSchema#string">e526fe19bd024f2ea7c84b9bccaf1243</dcterms:identifier>
    <dcterms:title rdf:parseType="Literal">StrictDoc</dcterms:title>
  </oslc:ServiceProvider>
  <oslc:Service rdf:nodeID="autos67">
    <oslc:domain rdf:resource="http://open-services.net/ns/rm#" />
    <oslc:queryCapability rdf:nodeID="autos68" />
    <oslc:selectionDialog rdf:nodeID="autos69" />
  </oslc:Service>
  <oslc:QueryCapability rdf:nodeID="autos68">
    <oslc:label rdf:datatype="http://www.w3.org/2001/XMLSchema#string">StrictDoc Requirements Query Capability</oslc:label>
    <oslc:queryBase rdf:resource="https://strictdoc-rm.oslc.ldsw.eu/oslc/service_provider/e526fe19bd024f2ea7c84b9bccaf1243/requirements" />
    <oslc:resourceShape rdf:resource="http://open-services.net/ns/rm/shapes/3.0#RequirementShape" />
    <oslc:resourceType rdf:resource="http://open-services.net/ns/rm#Requirement" />
    <dcterms:title rdf:parseType="Literal">StrictDoc Requirements Query Capability</dcterms:title>
  </oslc:QueryCapability>
  <oslc:Dialog rdf:nodeID="autos69">
    <oslc:dialog rdf:resource="https://strictdoc-rm.oslc.ldsw.eu/oslc/service_provider/e526fe19bd024f2ea7c84b9bccaf1243/requirements/selector" />
    <oslc:hintHeight rdf:datatype="http://www.w3.org/2001/XMLSchema#string">500px</oslc:hintHeight>
    <oslc:hintWidth rdf:datatype="http://www.w3.org/2001/XMLSchema#string">500px</oslc:hintWidth>
    <oslc:label rdf:datatype="http://www.w3.org/2001/XMLSchema#string">Select Requirement</oslc:label>
    <oslc:resourceType rdf:resource="http://open-services.net/ns/rm#Requirement" />
    <dcterms:title rdf:parseType="Literal">Requirement Selection Dialog</dcterms:title>
  </oslc:Dialog>
</rdf:RDF>
```
