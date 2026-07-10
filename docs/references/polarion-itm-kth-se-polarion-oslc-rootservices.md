---
url: "https://polarion.itm.kth.se/polarion/oslc/rootservices"
title: "https://polarion.itm.kth.se/polarion/oslc/rootservices"
fetched_at: "2026-06-23T17:55:57.199738+00:00"
status: 200
content_type: "application/rdf+xml;charset=UTF-8"
---

# https://polarion.itm.kth.se/polarion/oslc/rootservices

```text
<?xml version="1.0"?>









<rdf:Description
    xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"
	xmlns:dc="http://purl.org/dc/terms/"
	xmlns:jfs="http://jazz.net/xmlns/prod/jazz/jfs/1.0/"
	xmlns:jd="http://jazz.net/xmlns/prod/jazz/discovery/1.0/"
	xmlns:oslc="http://open-services.net/ns/core#"
	xmlns:oslc_pol="http://polarion.plm.automation.siemens.com/oslc#"
	rdf:about="https://polarion.itm.kth.se/polarion/oslc/rootservices">

  	<dc:title xml:lang="en">Polarion OSLC Root Services</dc:title>

	
	
	<jfs:oauthRealmName>Polarion</jfs:oauthRealmName>
	<jfs:oauthDomain>https://polarion.itm.kth.se</jfs:oauthDomain>
	<jfs:oauthRequestConsumerKeyUrl rdf:resource="https://polarion.itm.kth.se/polarion/oslc/services/oauth/requestKey"/>
	<jfs:oauthApprovalModuleUrl rdf:resource="https://polarion.itm.kth.se/polarion/oslc/services/oauth/approveKey"/>
	<jfs:oauthRequestTokenUrl rdf:resource="https://polarion.itm.kth.se/polarion/oslc/services/oauth/requestToken"/>
	<jfs:oauthUserAuthorizationUrl rdf:resource="https://polarion.itm.kth.se/polarion/oslc/services/oauth/authorize"/>
	<jfs:oauthAccessTokenUrl rdf:resource="https://polarion.itm.kth.se/polarion/oslc/services/oauth/accessToken"/>	
 
	
  	<oslc_cm:cmServiceProviders 
  		xmlns:oslc_cm="http://open-services.net/xmlns/cm/1.0/" 
  		rdf:resource="https://polarion.itm.kth.se/polarion/oslc/services/catalog" />
	
  		 		
	
  	<oslc_rm:rmServiceProviders 
  		xmlns:oslc_rm="http://open-services.net/xmlns/rm/1.0/" 
  		rdf:resource="https://polarion.itm.kth.se/polarion/oslc/services/catalog" />
	

	
  	<oslc_qm:qmServiceProviders 
  		xmlns:oslc_qm="http://open-services.net/xmlns/qm/1.0/" 
  		rdf:resource="https://polarion.itm.kth.se/polarion/oslc/services/catalog" />
	

 	<jd:oslcCatalogs>
		<oslc:ServiceProviderCatalog rdf:about="https://polarion.itm.kth.se/polarion/oslc/services/catalog">
			
				<oslc:domain rdf:resource="http://open-services.net/ns/cm#"/>
			
				<oslc:domain rdf:resource="http://open-services.net/ns/rm#"/>
			
				<oslc:domain rdf:resource="http://www.plm.automation.siemens.com/ldf#"/>
			
				<oslc:domain rdf:resource="http://www.plm.automation.siemens.com/ldf/esm#"/>
			
				<oslc:domain rdf:resource="http://open-services.net/ns/qm#"/>
			
		</oslc:ServiceProviderCatalog>
	</jd:oslcCatalogs>
</rdf:Description>
```
