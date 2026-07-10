#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
APPHOST="$ROOT_DIR/src/StrictDocOslcRmServer/StrictDocOslcRm.AppHost/StrictDocOslcRm.AppHost.csproj"
RESOURCE_NAME="strictdoc-oslc-rm"

require_command() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Missing required command: $1" >&2
    exit 1
  fi
}

require_aspire_isolated_mode() {
  if ! aspire start --help 2>/dev/null | grep -q -- "--isolated"; then
    cat >&2 <<'EOF'
Aspire CLI 13.2+ is required for worktree-safe isolated smoke tests.

Install/update:
  curl -sSL https://aspire.dev/install.sh | bash

Then run:
  scripts/aspire-smoke.sh
EOF
    exit 1
  fi
}

extract_first_http_endpoint() {
  python3 - "$1" <<'PY'
import json
import sys

data = json.load(open(sys.argv[1], encoding="utf-8"))

for resource in data.get("resources", []):
    urls = resource.get("urls") or []
    for url in urls:
        if isinstance(url, dict):
            value = url.get("url") or url.get("endpointUrl") or url.get("displayText")
        else:
            value = url
        if isinstance(value, str) and value.startswith("http://"):
            print(value.rstrip("/"))
            raise SystemExit(0)
    for url in urls:
        if isinstance(url, dict):
            value = url.get("url") or url.get("endpointUrl") or url.get("displayText")
        else:
            value = url
        if isinstance(value, str) and value.startswith("https://"):
            print(value.rstrip("/"))
            raise SystemExit(0)

print("")
PY
}

require_command aspire
require_command curl
require_command python3
require_aspire_isolated_mode

tmp_dir="$(mktemp -d)"
describe_json="$tmp_dir/describe.json"
trap 'aspire stop --apphost "$APPHOST" --non-interactive >/dev/null 2>&1 || true; rm -rf "$tmp_dir"' EXIT

aspire start \
  --apphost "$APPHOST" \
  --isolated \
  --format Json \
  --non-interactive

aspire describe "$RESOURCE_NAME" \
  --apphost "$APPHOST" \
  --format Json \
  --non-interactive > "$describe_json"

base_url="$(extract_first_http_endpoint "$describe_json")"
if [[ -z "$base_url" ]]; then
  echo "Could not find an HTTP endpoint for $RESOURCE_NAME in Aspire describe output:" >&2
  cat "$describe_json" >&2
  exit 1
fi

echo "Smoke testing $RESOURCE_NAME at $base_url"

rootservices="$(curl -fsS "$base_url/.well-known/oslc/rootservices.xml" \
  --header "Accept: application/rdf+xml" \
  --show-error)"
grep -q "rmServiceProviders" <<<"$rootservices"

shape="$(curl -fsS "$base_url/oslc/shapes/requirement" \
  --header "Accept: text/turtle" \
  --show-error)"
grep -q "affectedBy" <<<"$shape"

requirement="$(curl -fsS "$base_url/?a=SDOC-HIGH-REQS-DECOMP" \
  --header "Accept: application/ld+json" \
  --show-error)"
grep -q "SDOC-HIGH-REQS-DECOMP" <<<"$requirement"

echo "Aspire isolated smoke test passed."
