# Jazz log relay

`jazz_log_relay.py` is a Python 3.9+ standard-library-only, read-only relay
for a Jazz deployment. It implements the same contract as the OSLC4Net relay
in [`../log-relay/PROTOCOL.md`](../log-relay/PROTOCOL.md).

Run it as the Jazz service account, which already has read access to the
application logs:

```bash
cd /opt/jazz-log-relay
export JAZZ_LOG_RELAY_API_KEY="$(openssl rand -hex 32)"
export JAZZ_LOG_RELAY_SOURCES='{
  "gc":"/opt/jazz/server/logs/gc.log",
  "jts":"/opt/jazz/server/logs/jts.log",
  "messages":"/opt/jazz/server/logs/messages.log",
  "trace":"/opt/jazz/server/logs/trace.log",
  "ffdc":"/opt/jazz/server/logs/ffdc",
  "rm":"/opt/jazz/server/logs/rm.log",
  "ldx":"/opt/jazz/server/logs/ldx.log"
}'
export JAZZ_LOG_RELAY_BIND=127.0.0.1
export JAZZ_LOG_RELAY_PORT=8742
export JAZZ_LOG_RELAY_SECURE_COOKIES=true
python3 jazz_log_relay.py
```

The UI at `/` uses a short-lived HttpOnly cookie. Keep
`JAZZ_LOG_RELAY_SECURE_COOKIES=true` when accessing it through HTTPS. Set it to
`false` only when the browser itself connects to a loopback HTTP listener.

The relay can read configured files and top-level regular files in a configured
directory. It never runs a caller-provided shell command, reads a caller-
provided path, changes log levels, or calls a Jazz debug endpoint. Bind it to
loopback and expose it only through authenticated private networking while a
diagnostic session is active.
