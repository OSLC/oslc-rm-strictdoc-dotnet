# OSLC4Net log relay

This is a separate, read-only NativeAOT diagnostics server. It implements the
shared HTTP and SSE contract in [`tools/log-relay/PROTOCOL.md`](../../../tools/log-relay/PROTOCOL.md).
It is not part of the StrictDoc application process and can be configured for
any OSLC4Net server.

## Configuration

```sh
export LOG_RELAY_API_KEY="$(openssl rand -hex 32)"
export LOG_RELAY_SOURCES='{"captures":"/data/oslc-integration-diagnostics"}'
export LOG_RELAY_URLS='http://127.0.0.1:8742'
export LOG_RELAY_SECURE_COOKIES=true
./OSLC4Net.Server.LogRelay
```

Sources are fixed file or directory paths. A directory exposes top-level log
files in timestamp-friendly filename order. The server does not accept paths,
commands, or mutations from a caller. It binds to loopback by default; publish
it through a private reverse proxy or Tailscale listener rather than binding it
directly to a public interface.

## Container build

The Dockerfile publishes a self-contained NativeAOT executable for the target
Linux architecture:

```sh
cd src/StrictDocOslcRmServer
podman build --platform linux/amd64 \
  -f OSLC4Net.Server.LogRelay/Dockerfile \
  -t oslc4net-log-relay:local .
```

Mount diagnostic files read-only and pass configuration through environment
variables. For example, the StrictDoc host's capture directory is a suitable
source named `captures`; a Docker/Compose application log should be exported to
a separate read-only file before it is configured as a source.

An on-host Compose service can use the existing capture mount directly:

```yaml
  oslc-log-relay:
    image: oslc4net-log-relay:local
    ports:
      - "127.0.0.1:8742:8742"
    volumes:
      - ./data/config:/data:ro
    environment:
      LOG_RELAY_API_KEY: "replace-with-a-long-random-value"
      LOG_RELAY_SOURCES: '{"captures":"/data/oslc-integration-diagnostics"}'
      LOG_RELAY_SECURE_COOKIES: "true"
    restart: unless-stopped
```

## Browser UI and API

`GET /` serves a deliberately thin browser view. It exchanges an entered API
key for a four-hour HttpOnly cookie, lists the configured sources, then uses
the authenticated SSE stream. API clients use `Authorization: Bearer <key>`.
Run `agent-context` for the versioned capability description.
