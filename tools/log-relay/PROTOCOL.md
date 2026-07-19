# Log relay protocol v1

The Jazz relay and the OSLC4Net relay implement the same local, read-only HTTP
contract. A relay exposes only named sources supplied by its local
configuration; a client cannot supply a host path or command. A source path can
name a file or a directory. Directory sources expose top-level regular files in
ordinal filename order, with a `--- filename ---` marker before each file.

All API endpoints require either `Authorization: Bearer <key>` or the
`relay_session` cookie created by `POST /v1/session`. Responses are JSON unless
the endpoint is an SSE stream.

| Endpoint | Purpose |
| --- | --- |
| `GET /v1/agent-context` | Versioned machine-readable capability description. |
| `GET /v1/sources` | Lists configured source names and kinds. |
| `GET /v1/logs?source={name}&limit={1..1000}&cursor={optional}&contains={optional}` | Returns bounded log lines and a cursor. |
| `GET /v1/stream?source={name}&cursor={optional}&contains={optional}` | Emits bounded `line` SSE events while the client remains connected. |
| `POST /v1/session` | Exchanges a valid Bearer key for an HttpOnly browser session cookie. |

`GET /v1/logs` returns this envelope:

```json
{
  "protocolVersion": "v1",
  "source": "application",
  "lines": [{ "cursor": "42", "text": "example" }],
  "nextCursor": "43",
  "truncated": false,
  "reset": false
}
```

`cursor` is opaque to clients. `contains` is a case-sensitive literal
substring, not a regular expression. A relay can set `reset` when log rotation
invalidates an earlier cursor.

## Local configuration

Both relays bind to loopback by default. Put a reverse proxy or a Tailscale
listener in front of them when browser access from another machine is needed;
do not bind a diagnostic relay directly to a public interface.

The OSLC4Net relay uses:

```text
LOG_RELAY_API_KEY=<long random value>
LOG_RELAY_SOURCES={"application":"/path/to/application.log","captures":"/path/to/capture-directory"}
LOG_RELAY_URLS=http://127.0.0.1:8742
LOG_RELAY_SECURE_COOKIES=true
```

The Jazz relay uses the corresponding `JAZZ_LOG_RELAY_*` names, plus optional
`JAZZ_LOG_RELAY_BIND` and `JAZZ_LOG_RELAY_PORT`. `*_SECURE_COOKIES` defaults to
`true`; set it to `false` only for a loopback HTTP development session. API-key
authentication remains required for every API and SSE endpoint.

The relays do not interpret log content, invoke Jazz debug endpoints, run
commands supplied by a caller, or mutate the observed system.
