---
url: "https://strictdoc-rm.oslc.ldsw.eu/.well-known/oslc/rootservices.xml"
title: "https://strictdoc-rm.oslc.ldsw.eu/.well-known/oslc/rootservices.xml"
fetched_at: "2026-06-23T17:55:57.199738+00:00"
status: 200
content_type: "application/rdf+xml; charset=utf-8"
---

# https://strictdoc-rm.oslc.ldsw.eu/.well-known/oslc/rootservices.xml

```text
<?xml version="1.0" encoding="UTF-8"?>
<rdf:Description
        xmlns:oslc_rm="http://open-services.net/xmlns/rm/1.0/"
        xmlns:oslc="http://open-services.net/ns/core#"
        xmlns:dc="http://purl.org/dc/terms/"
        xmlns:jfs="http://jazz.net/xmlns/prod/jazz/jfs/1.0/"
        xmlns:jd="http://jazz.net/xmlns/prod/jazz/discovery/1.0/"
        xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"
        rdf:about="https://strictdoc-rm.oslc.ldsw.eu/.well-known/oslc/rootservices.xml">
    <dc:title>OSLC Requirements Management server for StrictDoc</dc:title>
    <oslc_rm:rmServiceProviders rdf:resource="https://strictdoc-rm.oslc.ldsw.eu/oslc/catalog" />
    <jd:oslcCatalogs>
        <oslc:ServiceProviderCatalog rdf:about="https://strictdoc-rm.oslc.ldsw.eu/oslc/catalog">
            <oslc:domain rdf:resource="http://open-services.net/ns/rm#" />
        </oslc:ServiceProviderCatalog>
    </jd:oslcCatalogs>
    <jd:jsaSsoEnabled>false</jd:jsaSsoEnabled>
    <jfs:oauthRealmName>OSLC RM for StrictDoc</jfs:oauthRealmName>
    <jfs:oauthDomain>https://strictdoc-rm.oslc.ldsw.eu</jfs:oauthDomain>
    <jfs:oauthRequestConsumerKeyUrl rdf:resource="https://strictdoc-rm.oslc.ldsw.eu/oauth/request_consumer_key"/>
    <jfs:oauthApprovalModuleUrl rdf:resource="https://strictdoc-rm.oslc.ldsw.eu/oauth/approve_consumer_key"/>
    <jfs:oauthRequestTokenUrl rdf:resource="https://strictdoc-rm.oslc.ldsw.eu/oauth/request_token"/>
    <jfs:oauthUserAuthorizationUrl rdf:resource="https://strictdoc-rm.oslc.ldsw.eu/oauth/authorize"/>
    <jfs:oauthAccessTokenUrl rdf:resource="https://strictdoc-rm.oslc.ldsw.eu/oauth/access_token"/>
</rdf:Description>
```
