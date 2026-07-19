# Development guide

## Regenerating demo data

From the repository root, regenerate the StrictDoc JSON database and the
matching SPDX and HTML artifacts after changing the demo `.sdoc` or `.sgra`
files:

```sh
uvx strictdoc export src/hellow-requirements/ --output-dir src/hellow-requirements/output/ --formats json,spdx,html,reqif-sdoc
```

The configuration-enabled development server reads the selected snapshot under
`src/hellow-requirements/output/{branch}/{tag}/strictdoc.json`; refresh that
snapshot from the generated JSON as part of a demo-data change. Commit the
regenerated demo artifacts together with their source changes.

## Local server

Run the server from the application project:

```sh
cd src/StrictDocOslcRmServer/StrictDocOslcRm
dotnet run
```

The development configuration accepts `localhost` and the developer's configured
Tailscale MagicDNS name. Keep the real name in the local configuration only; do
not add it to documentation, examples, commits, or issue reports.

The server data root is `src/hellow-requirements/output`. The initial
configuration-management snapshots are laid out as:

```text
{data-root}/main/HEAD/strictdoc.json
{data-root}/main/HEAD/sidecar.json
{data-root}/main/v0.1.0/strictdoc.json
{data-root}/argicultural/HEAD/strictdoc.json
```

Run the test suite from the solution directory:

```sh
cd src/StrictDocOslcRmServer
dotnet test --solution StrictDocOslcRm.slnx --no-restore
```

## OSLC remote-integration diagnostics

`OSLC4Net.Server.Diagnostics` logs an incoming request and completed response
at .NET `Trace` level when the request sends `OSLC-Core-Version` or its
`Accept` header does not advertise `text/html`. This retains Jazz and other
machine-client exchanges while omitting ordinary browser navigation. Set
`OslcIntegrationDiagnostics:LogAllRequests` to `true` for unfiltered request
logging. The request event includes the method, public URL,
`Configuration-Context`, `X-Com-Ibm-Team-Trace-Identifier`, `User-Agent`,
`Accept`, content type, ASP.NET trace identifier, and OpenTelemetry trace ID.
The response event includes the status and duration. A redirect additionally
gets a one-line Trace event with its `Location` target. When the IBM trace
header is present, its filename-safe value is also appended as
`_JazzTrace_<id>` to the paired capture files.

For exchanges that need raw evidence, configure the following section. When
`CapturePayloads` is true, the server creates a timestamped pair of files for
each 4xx/5xx response; `CaptureSuccessfulResponses` extends that to 2xx
responses. Redirects are never written as payload captures.

```json
{
  "OslcIntegrationDiagnostics": {
    "CapturePayloads": true,
    "CaptureSuccessfulResponses": false,
    "CaptureDirectory": "/absolute/local/path/oslc-integration-diagnostics",
    "IncludeSensitiveHeaders": false,
    "LogAllRequests": false
  }
}
```

Each `*_req.log` and `*_resp.log` contains both ISO-8601 and Unix timestamps,
the trace identifiers, headers, and the unmodified payload. Sensitive headers
are redacted unless `IncludeSensitiveHeaders` is enabled. The development
profile enables it for Jazz troubleshooting, so treat the capture directory as
credential-bearing local data and do not commit or attach its files to issues.
It also enables `Trace` only for the `OSLC4Net.Server.Diagnostics` logger
category, without enabling framework-wide Trace logging.

For a Docker Compose deployment, add these environment variables and retain a
`/data` volume so captures survive container recreation:

```yaml
environment:
  Logging__LogLevel__OSLC4Net.Server.Diagnostics: Trace
  OslcIntegrationDiagnostics__CapturePayloads: "true"
  OslcIntegrationDiagnostics__CaptureSuccessfulResponses: "true"
  OslcIntegrationDiagnostics__CaptureDirectory: /data/oslc-integration-diagnostics
  OslcIntegrationDiagnostics__IncludeSensitiveHeaders: "false"
```

This captures 2xx baseline-creation POSTs as paired files under
`/data/oslc-integration-diagnostics`. Do not enable sensitive headers on a
long-lived host unless the directory is protected and promptly cleaned up.

## Deployment

Locally:

```sh
cd ~/code/a/oslc/oslc4net-misc/oslc-rm-strictdoc-dotnet/src ; export manifest=localhost/rm-strictdoc-oslc4net:latest-multi ; export image=sv-forge.berezovskyi.me/smarx721/rm-strictdoc-oslc4net:latest ; podman manifest rm "$manifest" 2>/dev/null || true ; podman build --platform linux/amd64,linux/arm64 --manifest "$manifest" -f StrictDocOslcRmServer/StrictDocOslcRm/Dockerfile StrictDocOslcRmServer/ && podman manifest push --all "$manifest" "docker://$image" && podman manifest rm "$manifest"
```

Remotely:

```sh
cd /opt/strictdoc ; docker compose --compatibility up --build -d --remove-orphans --pull always
```

## Tailscale Serve debugging

Use Tailscale Serve to make a locally running development server available to
other devices in the tailnet. The examples deliberately use anonymized names:

```sh
tailscale serve --bg --https=443 http://127.0.0.1:<local-port>
tailscale serve status
```

When starting through the Aspire AppHost, set the public OSLC base URI on the
_AppHost_ so that the generated OSLC resource URIs use the Tailscale HTTPS URL
rather than Aspire's local HTTP endpoint:

```sh
OSLC__PublicBaseUri='https://<node-name>.<tailnet-name>.ts.net/' \
  aspire start \
  --apphost src/StrictDocOslcRmServer/StrictDocOslcRm.AppHost/StrictDocOslcRm.AppHost.csproj \
  --isolated
```

`OSLC__PublicBaseUri` maps to the `OSLC:PublicBaseUri` configuration key. If it
is absent, the AppHost retains its normal generated local HTTP endpoint.

When set, this is the authoritative public origin for every OSLC-facing URI:
root services, SCR, publisher metadata, service/provider/catalog resources,
configuration resources, OAuth endpoints, and formatter-generated query response
subjects. It must therefore be the external HTTPS URI, not the local proxy target.

The expected status is equivalent to:

```text
https://<node-name>.<tailnet-name>.ts.net (tailnet only)
|-- / proxy http://127.0.0.1:<local-port>
```

Confirm that the application is listening locally before diagnosing Tailscale:

```sh
lsof -nP -iTCP:<local-port> -sTCP:LISTEN
curl -i \
  -H 'Host: <node-name>.<tailnet-name>.ts.net' \
  -H 'X-Forwarded-Proto: https' \
  http://127.0.0.1:<local-port>/rootservices
```

The second command should return `200`. Supplying the public Host header is
important: ASP.NET Core host filtering can reject a request sent to
`127.0.0.1:<local-port>` with `400` even when the reverse-proxied request will
be accepted.

Validate Serve from a _different_ online device in the tailnet:

```sh
curl -i https://<node-name>.<tailnet-name>.ts.net/rootservices
```

Do not treat a request from the Serve host to its own Tailscale IP or MagicDNS
name as the sole test. On macOS that self-originated path can be reset by the
Tailscale networking layer before it reaches the proxied application. When this
happens, the daemon log reports a TLS handshake error ending in
`socket is not
connected`; it is not an application response. Use
`http://localhost:<local-port>/` to browse locally. The local curl above
verifies the application; a second tailnet node verifies Serve.

### Common checks

```sh
tailscale status
tailscale netcheck
tailscale serve status --json
```

If `tailscale serve status` shows an obsolete endpoint, remove that endpoint by
its configured HTTPS port, then configure the desired proxy again:

```sh
tailscale serve http://127.0.0.1:<old-port> off
tailscale serve --bg --https=443 http://127.0.0.1:<local-port>
```

On macOS, check the Application Firewall without disabling it:

```sh
/usr/libexec/ApplicationFirewall/socketfilterfw --getglobalstate
/usr/libexec/ApplicationFirewall/socketfilterfw --getappblocked \
  /Library/SystemExtensions/<extension-id>/io.tailscale.ipn.macsys.network-extension.systemextension/Contents/MacOS/io.tailscale.ipn.macsys.network-extension
```

The active extension path can be obtained with:

```sh
ps aux | rg '[t]ailscale'
```

If the local service and Serve configuration are correct but a second tailnet
device cannot connect, check the tailnet ACL or grant permitting that source to
reach TCP port 443 on this node. Tailscale Serve is private to the tailnet; use
Funnel only when public internet exposure is explicitly intended.
