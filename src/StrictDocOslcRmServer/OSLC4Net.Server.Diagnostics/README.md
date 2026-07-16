# OSLC4Net.Server.Diagnostics

ASP.NET Core middleware for investigating OSLC client/server HTTP exchanges
without putting raw RDF or HTML payloads into application logs.

Register the middleware after forwarded-header/public-origin normalization and
before OSLC endpoints:

```csharp
builder.Services.AddOslcIntegrationDiagnostics(builder.Configuration);

var app = builder.Build();
app.UseOslcIntegrationDiagnostics();
```

It writes Trace-level request and response summaries for every exchange. Set
`OslcIntegrationDiagnostics:CapturePayloads` to capture paired `*_req.log` and
`*_resp.log` files for 4xx and 5xx responses. Set
`CaptureSuccessfulResponses` to capture 2xx responses too; redirects are never
captured. The files include ISO-8601 and Unix timestamps, headers, trace IDs,
and raw bodies.

Sensitive header values are redacted by default. Use
`IncludeSensitiveHeaders` only in a protected, local diagnostic directory.
