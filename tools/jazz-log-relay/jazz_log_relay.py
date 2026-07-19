#!/usr/bin/env python3
"""Read-only Jazz log relay; compatible with tools/log-relay/PROTOCOL.md."""

import argparse
import hmac
import json
import os
import time
from collections import deque
from http import HTTPStatus
from http.cookies import SimpleCookie
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import parse_qs, urlparse

PROTOCOL_VERSION = "v1"
DEFAULT_LIMIT = 100
MAXIMUM_LIMIT = 1000


def parse_sources(raw):
    try:
        sources = json.loads(raw)
    except json.JSONDecodeError as error:
        raise ValueError("JAZZ_LOG_RELAY_SOURCES must be a JSON object") from error
    if not isinstance(sources, dict) or not sources:
        raise ValueError("JAZZ_LOG_RELAY_SOURCES must contain at least one source")
    resolved = {}
    for name, path in sources.items():
        if not isinstance(name, str) or not name or not isinstance(path, str) or not path:
            raise ValueError("each source needs a non-empty name and file path")
        resolved[name] = os.path.abspath(path)
    return resolved


def config_from_environment():
    api_key = os.environ.get("JAZZ_LOG_RELAY_API_KEY")
    sources = os.environ.get("JAZZ_LOG_RELAY_SOURCES")
    if not api_key:
        raise ValueError("JAZZ_LOG_RELAY_API_KEY is required")
    if not sources:
        raise ValueError("JAZZ_LOG_RELAY_SOURCES is required")
    secure_cookies = os.environ.get("JAZZ_LOG_RELAY_SECURE_COOKIES", "true").lower() != "false"
    return api_key, parse_sources(sources), secure_cookies


def source_lines(path):
    if os.path.isfile(path):
        with open(path, "r", encoding="utf-8", errors="replace") as handle:
            yield from (line.rstrip("\r\n") for line in handle)
        return
    if not os.path.isdir(path):
        return
    for name in sorted(os.listdir(path)):
        file_path = os.path.join(path, name)
        if not os.path.isfile(file_path):
            continue
        yield "--- %s ---" % name
        with open(file_path, "r", encoding="utf-8", errors="replace") as handle:
            yield from (line.rstrip("\r\n") for line in handle)


def bounded_page(path, source, cursor, contains, limit):
    try:
        start = int(cursor) if cursor is not None else -1
        if start < -1:
            raise ValueError
    except ValueError:
        return {"error": "invalid_cursor", "recovery": "Omit cursor or use the cursor returned by this relay."}, HTTPStatus.BAD_REQUEST
    if not os.path.exists(path):
        return envelope(source, [], "0", False, start > 0), HTTPStatus.OK

    tail = deque(maxlen=limit)
    page = []
    total = 0
    for text in source_lines(path):
        matches = not contains or contains in text
        if start >= 0 and total >= start and matches and len(page) < limit:
            page.append({"cursor": str(total + 1), "text": text})
        elif start < 0 and matches:
            tail.append({"cursor": str(total + 1), "text": text})
        total += 1
    lines = page if start >= 0 else list(tail)
    return envelope(source, lines, str(total), start >= 0 and total > start + len(page), False), HTTPStatus.OK


def envelope(source, lines, next_cursor, truncated, reset):
    return {"protocolVersion": PROTOCOL_VERSION, "source": source, "lines": lines,
            "nextCursor": next_cursor, "truncated": truncated, "reset": reset}


class RelayHandler(BaseHTTPRequestHandler):
    api_key = ""
    sources = {}
    secure_cookies = True

    def log_message(self, format_string, *args):
        return

    def is_authorized(self):
        authorization = self.headers.get("Authorization", "")
        candidate = authorization[7:] if authorization.lower().startswith("bearer ") else None
        if candidate is None:
            cookie = SimpleCookie(self.headers.get("Cookie"))
            candidate = cookie.get("relay_session").value if cookie.get("relay_session") else None
        return candidate is not None and hmac.compare_digest(candidate.encode(), self.api_key.encode())

    def write_json(self, body, status=HTTPStatus.OK, headers=None):
        encoded = json.dumps(body, separators=(",", ":")).encode()
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(encoded)))
        for name, value in (headers or {}).items():
            self.send_header(name, value)
        self.end_headers()
        self.wfile.write(encoded)

    def source_from_query(self, query):
        source = query.get("source", [""])[0]
        if not source:
            return None, None, ({"error": "source_required", "recovery": "Use a source returned by GET /v1/sources."}, HTTPStatus.BAD_REQUEST)
        path = self.sources.get(source)
        if not path:
            return None, None, ({"error": "unknown_source", "validSources": sorted(self.sources)}, HTTPStatus.NOT_FOUND)
        return source, path, None

    def do_POST(self):
        if self.path != "/v1/session":
            self.write_json({"error": "not_found"}, HTTPStatus.NOT_FOUND)
            return
        if not self.is_authorized():
            self.write_json({"error": "authentication_required"}, HTTPStatus.UNAUTHORIZED, {"WWW-Authenticate": "Bearer"})
            return
        cookie = "relay_session=%s; HttpOnly; SameSite=Strict; Path=/; Max-Age=14400" % self.api_key
        if self.secure_cookies:
            cookie += "; Secure"
        self.send_response(HTTPStatus.NO_CONTENT)
        self.send_header("Set-Cookie", cookie)
        self.end_headers()

    def do_GET(self):
        parsed = urlparse(self.path)
        if parsed.path == "/":
            self.send_response(HTTPStatus.OK)
            self.send_header("Content-Type", "text/html; charset=utf-8")
            self.end_headers()
            self.wfile.write(HTML.encode())
            return
        if not self.is_authorized():
            self.write_json({"error": "authentication_required", "recovery": "Supply Authorization: Bearer <API key>."}, HTTPStatus.UNAUTHORIZED, {"WWW-Authenticate": "Bearer"})
            return
        if parsed.path == "/v1/agent-context":
            self.write_json({"version": "1", "protocolVersion": PROTOCOL_VERSION, "relay": "jazz", "sourceKinds": ["file", "directory"], "maximumLimit": MAXIMUM_LIMIT, "supportsSse": True, "mutations": False})
            return
        if parsed.path == "/v1/sources":
            self.write_json({"protocolVersion": PROTOCOL_VERSION, "sources": [{"name": name, "kind": "directory" if os.path.isdir(self.sources[name]) else "file"} for name in sorted(self.sources)]})
            return
        query = parse_qs(parsed.query)
        source, path, error = self.source_from_query(query)
        if error:
            self.write_json(*error)
            return
        try:
            limit = min(MAXIMUM_LIMIT, max(1, int(query.get("limit", [DEFAULT_LIMIT])[0])))
        except ValueError:
            self.write_json({"error": "invalid_limit", "recovery": "Use an integer from 1 through 1000."}, HTTPStatus.BAD_REQUEST)
            return
        if parsed.path == "/v1/logs":
            body, status = bounded_page(path, source, query.get("cursor", [None])[0], query.get("contains", [None])[0], limit)
            self.write_json(body, status)
            return
        if parsed.path == "/v1/stream":
            self.stream(path, source, query.get("cursor", [None])[0], query.get("contains", [None])[0])
            return
        self.write_json({"error": "not_found"}, HTTPStatus.NOT_FOUND)

    def stream(self, path, source, cursor, contains):
        try:
            position = int(cursor) if cursor is not None else max(0, line_count(path) - DEFAULT_LIMIT)
        except ValueError:
            position = 0
        self.send_response(HTTPStatus.OK)
        self.send_header("Content-Type", "text/event-stream")
        self.send_header("Cache-Control", "no-cache")
        self.end_headers()
        try:
            while True:
                body, _ = bounded_page(path, source, str(position), contains, MAXIMUM_LIMIT)
                for line in body["lines"]:
                    self.wfile.write(("event: line\ndata: " + json.dumps(line, separators=(",", ":")) + "\n\n").encode())
                self.wfile.flush()
                position = int(body["nextCursor"])
                time.sleep(1)
        except (BrokenPipeError, ConnectionResetError):
            return


def line_count(path):
    return sum(1 for _ in source_lines(path))


HTML = """<!doctype html><meta charset=utf-8><title>Jazz log relay</title><style>body{font:14px monospace;margin:2rem}pre{white-space:pre-wrap;background:#111;color:#ddd;padding:1rem}</style><h1>Jazz log relay</h1><input id=k type=password placeholder='API key'><button id=b>Connect</button><select id=s></select><pre id=o></pre><script>const k=document.querySelector('#k'),s=document.querySelector('#s'),o=document.querySelector('#o');let e;async function q(u,x={}){return fetch(u,{...x,headers:{Authorization:'Bearer '+k.value,...x.headers}})}b.onclick=async()=>{let r=await q('/v1/session',{method:'POST'});if(!r.ok){o.textContent='Authentication failed';return}r=await q('/v1/sources');let d=await r.json();s.replaceChildren(...d.sources.map(x=>new Option(x.name,x.name)));s.onchange=t;t()};function t(){if(e)e.close();o.textContent='';e=new EventSource('/v1/stream?source='+encodeURIComponent(s.value));e.addEventListener('line',x=>{o.textContent+=JSON.parse(x.data).text+'\\n'})}</script>"""


def main():
    parser = argparse.ArgumentParser(description="Serve named, read-only Jazz log sources.")
    parser.add_argument("--bind", default=os.environ.get("JAZZ_LOG_RELAY_BIND", "127.0.0.1"))
    parser.add_argument("--port", type=int, default=int(os.environ.get("JAZZ_LOG_RELAY_PORT", "8742")))
    parser.add_argument("agent-context", nargs="?", choices=["agent-context"])
    args = parser.parse_args()
    if args.agent_context:
        print(json.dumps({"version": "1", "protocolVersion": PROTOCOL_VERSION, "relay": "jazz", "mutations": False}))
        return
    try:
        api_key, sources, secure_cookies = config_from_environment()
    except ValueError as error:
        parser.error(str(error))
    RelayHandler.api_key = api_key
    RelayHandler.sources = sources
    RelayHandler.secure_cookies = secure_cookies
    ThreadingHTTPServer((args.bind, args.port), RelayHandler).serve_forever()


if __name__ == "__main__":
    main()
