#!/usr/bin/env bash
# AOT smoke test for issue #10 — proves the /v1/messages translation endpoint
# handles a real request on a Release Native AOT publish of MininRouter
# (not just that it links). Repeats the ticket's manual smoke run:
#   1. dotnet publish -c Release (skipped when the binary is newer than sources)
#   2. mock OpenAI upstream on 127.0.0.1:18099 (scripts/mock_openai_upstream.py)
#   3. router on 127.0.0.1:18080 with AUTH_PASSTHROUGH=1
#   4. POST /v1/messages -> assert HTTP 200 + Anthropic "type":"message" +
#      the mock's content round-tripped back from Chat Completions shape.
#
# Startup quirk: with empty args the router binary first probes
# http://localhost:8080/health and EXITS 0 if anything answers, so this script
# fails fast when port 8080 is already in use.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PUBLISH_ROOT="$ROOT/bin/Release/net10.0"
ROUTER_PORT=18080
MOCK_PORT=18099

# Native AOT publish output lives under the RID directory (e.g. osx-arm64,
# linux-x64). Resolve it by glob instead of hardcoding one RID so the smoke
# works on any host; with several RIDs present, take the most recent build.
find_binary() {
    ls -t "$PUBLISH_ROOT"/*/publish/MininRouter 2>/dev/null | head -1 || true
}
BINARY="$(find_binary)"

MOCK_PID=""
ROUTER_PID=""
TMPDIR_SMOKE="$(mktemp -d /tmp/aot-smoke.XXXXXX)"

cleanup() {
    [ -n "$ROUTER_PID" ] && kill "$ROUTER_PID" 2>/dev/null || true
    [ -n "$MOCK_PID" ] && kill "$MOCK_PID" 2>/dev/null || true
    rm -rf "$TMPDIR_SMOKE"
}
trap cleanup EXIT

fail() {
    echo "aot-smoke: FAIL: $*" >&2
    exit 1
}

port_in_use() {
    if command -v lsof >/dev/null 2>&1; then
        lsof -nP -iTCP:"$1" -sTCP:LISTEN >/dev/null 2>&1
    else
        nc -z 127.0.0.1 "$1" >/dev/null 2>&1
    fi
}

# The router exits 0 at startup if ANYTHING answers on localhost:8080 — refuse to run.
if port_in_use 8080; then
    fail "port 8080 is already in use; the router binary would probe localhost:8080/health and exit instead of starting. Free the port and retry."
fi
for p in "$ROUTER_PORT" "$MOCK_PORT"; do
    if port_in_use "$p"; then
        fail "port $p is already in use; aot-smoke needs it free."
    fi
done

# Publish unless the AOT binary already exists and is newer than the sources.
needs_publish=1
if [ -n "$BINARY" ] && [ -x "$BINARY" ]; then
    newer_sources="$(find "$ROOT" \( -path "$ROOT/bin" -o -path "$ROOT/obj" -o -path "$ROOT/frontend" \) -prune \
        -o \( -name '*.cs' -o -name '*.csproj' \) -newer "$BINARY" -print -quit)"
    [ -z "$newer_sources" ] && needs_publish=0
fi
if [ "$needs_publish" -eq 1 ]; then
    echo "aot-smoke: publishing Release AOT build..."
    dotnet publish -c Release "$ROOT/MininRouter.csproj" || fail "dotnet publish failed"
    BINARY="$(find_binary)"
else
    echo "aot-smoke: reusing existing AOT binary at $BINARY"
fi
[ -n "$BINARY" ] && [ -x "$BINARY" ] || fail "publish output binary not found under $PUBLISH_ROOT/*/publish/"

# Mock OpenAI upstream.
python3 "$ROOT/scripts/mock_openai_upstream.py" "$MOCK_PORT" &
MOCK_PID=$!

# Router config: one enabled provider pointing at the mock.
cat > "$TMPDIR_SMOKE/providers.json" <<'EOF'
[{"id":"mock","name":"Mock","baseUrl":"http://127.0.0.1:18099","apiKey":"sk-mock","enabled":true,"models":["mock-model"]}]
EOF

ASPNETCORE_ENVIRONMENT=Development \
AUTH_PASSTHROUGH=1 \
PROVIDERS_CONFIG_PATH="$TMPDIR_SMOKE/providers.json" \
MODEL_CHAINS_CONFIG_PATH="$TMPDIR_SMOKE/model_chains.json" \
DbPath="$TMPDIR_SMOKE/minirouter.db" \
ASPNETCORE_URLS="http://127.0.0.1:$ROUTER_PORT" \
"$BINARY" > "$TMPDIR_SMOKE/router.log" 2>&1 &
ROUTER_PID=$!

# Wait for the router to come up (startup probes localhost:8080 first, ~1s).
ready=""
for _ in $(seq 1 60); do
    if ! kill -0 "$ROUTER_PID" 2>/dev/null; then
        fail "router exited during startup. Log:\n$(cat "$TMPDIR_SMOKE/router.log")"
    fi
    if curl -sf "http://127.0.0.1:$ROUTER_PORT/health" >/dev/null 2>&1; then
        ready=1
        break
    fi
    sleep 0.5
done
[ -n "$ready" ] || fail "router did not become ready on port $ROUTER_PORT. Log:\n$(cat "$TMPDIR_SMOKE/router.log")"

# The actual smoke request: Anthropic /v1/messages -> OpenAI mock -> Anthropic response.
http_code="$(curl -s -o "$TMPDIR_SMOKE/response.json" -w '%{http_code}' \
    -X POST "http://127.0.0.1:$ROUTER_PORT/v1/messages" \
    -H 'content-type: application/json' \
    -d '{"model":"mock-model","max_tokens":16,"messages":[{"role":"user","content":"hi"}]}')" \
    || fail "curl to /v1/messages failed"

body="$(cat "$TMPDIR_SMOKE/response.json")"
echo "aot-smoke: HTTP $http_code"
echo "aot-smoke: body: $body"

[ "$http_code" = "200" ] || fail "expected HTTP 200, got $http_code"
case "$body" in
    *'"type":"message"'*) ;;
    *) fail 'response does not contain "type":"message"' ;;
esac
case "$body" in
    *"Hello from mock"*) ;;
    *) fail 'response does not contain the mock content "Hello from mock"' ;;
esac

echo "aot-smoke: PASS — /v1/messages served correctly on the Native AOT binary."
