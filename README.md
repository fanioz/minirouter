# MiniRouter - Multi-Provider Native AOT LLM Router

A minimal ASP.NET Core reverse proxy supporting **multiple OpenAI-compatible providers** with round-robin routing, published as a Native AOT binary.

## Why MiniRouter?

Built for developers who want robust LLM routing without the bloat. Thanks to .NET Native AOT, MiniRouter is incredibly lightweight and lightning-fast:

- **Minimal Footprint:** Idles at just **~50 MB** RAM and peaks at **~75 MB** under sustained load (500 concurrent connections).
- **Micro-VPS Ready:** Tested and proven to run flawlessly on tiny $5/month VPS instances (512MB RAM, 0.5 vCPU) with 0% OOM kills.
- **Zero Dependencies:** Compiles to a single, self-contained native binary. No .NET runtime, Node.js, or fat containers required.
- **Blazing Fast:** No JIT warmup overhead and no reflection, ensuring instant startup times and minimal latency.

Drop heavy proxies that consume 600MB+ of RAM. Deploy MiniRouter and save your server resources for your actual workloads.

## Configuration

Copy the example config and fill in your keys (the real `providers.json` is gitignored):

```bash
cp providers.example.json providers.json
```

Set `PROVIDERS_CONFIG_PATH` (default: `./providers.json`) to point to your providers JSON file.

```bash
export PROVIDERS_CONFIG_PATH="./providers.json"
export MAX_RETRIES="2"
export CIRCUIT_FAILURE_THRESHOLD="3"
export CIRCUIT_COOLDOWN_SECONDS="30"
./publish/MininRouter
```

### Environment Variables

| Variable | Default | Description |
|---|---|---|
| `PROVIDERS_CONFIG_PATH` | `./providers.json` | Path to the JSON file storing providers |
| `MAX_RETRIES` | `2` | Maximum number of times to retry a failed request against another provider |
| `CIRCUIT_FAILURE_THRESHOLD` | `3` | Number of consecutive failures before a provider is temporarily excluded from routing |
| `CIRCUIT_COOLDOWN_SECONDS` | `30` | Number of seconds a failing provider is kept out of rotation before half-opening |
| `MODELS_CACHE_TTL_SECONDS` | `300` | TTL in seconds for the aggregated `/models` response cache |

## CLI Usage

MiniRouter can be invoked as a lightweight CLI to query the status of an already running server. The CLI acts as a client hitting the localhost API and prints plain text.

```bash
# Check if the server is running
./MininRouter status

# List all providers (requires the server to be running)
./MininRouter list providers

# List all available models aggregated from upstream providers
./MininRouter list models
```

## Data Model

### Provider Record
- `id`: Unique identifier (slug)
- `name`: Display label  
- `baseUrl`: OpenAI-compatible API base URL
- `apiKey`: API key (masked in responses)
- `enabled`: Whether included in round-robin rotation
- `model`: (optional) Model name to force on this provider. When set, the router
  rewrites the request's `model` field to this value before forwarding. When
  omitted/null, the client's requested model passes through unchanged.
- `models`: (optional) List of model names this provider can serve. When set,
  the router only routes requests for those models to this provider. Empty or
  omitted means the provider is a wildcard and can serve any model.

### Explicit Routing (Replaces Model-Aware Round-Robin)

By default, MiniRouter uses EXPLICIT routing. The router expects the `model` field in a chat completion request to follow the format `providerId/modelName`. 

For example, `"model": "openai-primary/gpt-4o-mini"`. The router will directly forward this request to the provider with ID `openai-primary`, stripping the prefix so the upstream receives just `"model": "gpt-4o-mini"`.
- If the `/` delimiter is missing, it will be rejected with a 400 error.
- If the provider is disabled or not found, it returns a 400 or 404 error respectively.
- If the provider's circuit breaker is open, it returns a 503 error immediately.
- There are NO cross-provider retries in this mode.

*Note: The automatic round-robin logic previously implemented is retained in the codebase for a future "virtual model" / alias feature, but is currently bypassed by the default route.*

## CRUD Endpoints

> [!NOTE]
> The management endpoints below (`/providers`, `/keys`, `/logs`, `/analytics/*`, `/models`) are currently **unauthenticated**. This allows the local dashboard to function without a separate admin-auth layer. Do not expose these endpoints directly to the internet without your own auth proxy in front.

### List Upstream Models (Aggregated)
```bash
curl http://localhost:8080/models
```
Returns a list of all models available from all currently enabled and healthy upstream providers. Responses are cached according to `MODELS_CACHE_TTL_SECONDS`.

### List Providers
```bash
curl http://localhost:8080/providers
```
Returns all providers with masked API keys (e.g., `sk-***abc123`).

### Get Single Provider
```bash
curl http://localhost:8080/providers/openai-primary
```

### Create Provider
```bash
curl -X POST http://localhost:8080/providers \
  -H "Content-Type: application/json" \
  -d '{
    "id": "my-provider",
    "name": "My Custom Provider",
    "baseUrl": "https://api.example.com",
    "apiKey": "sk-test-123456",
    "enabled": true,
    "model": "gpt-4o-mini"
  }'
```

### Update Provider
```bash
curl -X PUT http://localhost:8080/providers/my-provider \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Updated Name",
    "baseUrl": "https://new-api.example.com",
    "apiKey": "sk-new-key-789012",
    "enabled": false
  }'
```

### Delete Provider
```bash
curl -X DELETE http://localhost:8080/providers/my-provider
```

## API Key Management

### Create API Key
Creates a new API key. **The plaintext key is returned only once in this response.** Keep it safe!
```bash
curl -X POST http://localhost:8080/api/keys \
  -H "Content-Type: application/json" \
  -d '{"name": "test-client-1"}'
```

### List API Keys
```bash
curl http://localhost:8080/api/keys
```

### Update API Key
```bash
curl -X PUT http://localhost:8080/api/keys/ak_12345 \
  -H "Content-Type: application/json" \
  -d '{"name": "test-client-1-updated", "enabled": false}'
```

### Delete API Key
```bash
curl -X DELETE http://localhost:8080/api/keys/ak_12345
```

## Explicit Provider Routing

The `/v1/chat/completions` endpoint requires you to specify the provider and model explicitly in the `model` field, separated by a forward slash (`/`). **It requires authentication using an API key generated above.**

```bash
# Send request to a specific provider
curl -s http://localhost:8080/v1/chat/completions \
  -X POST \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer sk-your-api-key-here" \
  -d '{"model":"openai-primary/gpt-4o-mini","messages":[{"role":"user","content":"Ping"}],"stream":false}' \
  | jq .
```

Logs will show exactly one row per request with the selected provider, its latency, and success status.

### Disabling a Provider
```bash
# Disable a provider (removes it immediately)
curl -X PUT http://localhost:8080/providers/provider-x \
  -H "Content-Type: application/json" \
  -d '{"name":"Old Name","baseUrl":"https://old.com","apiKey":"xxx","enabled":false}'
```

Subsequent chat completion requests asking for `provider-x/model` will return a 400 error.

## Publish Command

Native AOT cross-compilation from macOS to Linux requires a Linux linker, so build
the `linux-x64` binary on a Linux host (or in Docker):

```bash
dotnet publish -c Release -r linux-x64 --self-contained -p:PublishAot=true -o ./publish
```

On macOS, publish for the host platform (verified clean, zero trimming warnings):

```bash
dotnet publish -c Release -r osx-arm64 --self-contained -p:PublishAot=true -o ./publish-osx
```

## Run

```bash
cd publish
./MininRouter
```

Default port is 8080. Change with:
```bash
PORT=9000 ./MininRouter
```

> [!NOTE]
> MiniRouter binds to port `8080` by default in both the local binary and the Docker container. Since it is designed as a single-instance service, running both simultaneously on the default port will cause an "Address already in use" conflict. If you need to run both at the same time, override the local port using `PORT=9000 ./MininRouter` or change the Docker host mapping (`-p 8081:8080`).

## Memory Usage & Architecture Experiments

During the development of the UI dashboard, we measured the idle RSS footprint of four different architectural approaches on macOS ARM64:

| # | Configuration | Idle RSS | Status |
|---|---------------|----------|--------|
| 1 | Native AOT — Minimal API only (no UI) | **~40 MB** | ✅ Baseline |
| 2 | JIT — Minimal API + Razor Pages + Spectre.Console CLI | **~134 MB** | ✅ Works, but 3.4× baseline |
| 3 | Native AOT — Razor Pages (precompiled views) | **N/A** | ❌ Hard failure on startup |
| 4 | Native AOT — Minimal API + static Svelte SPA | **~34 MB** | ✅ **Below baseline** |

### Key Findings

- **Spike #3 (Razor + AOT)** fails at startup with `System.TypeLoadException` — MVC/Razor's `ApplicationParts` system requires runtime reflection that Native AOT strips. This is a hard limitation in .NET 10 with no workaround.
- **Spike #4 (Static Svelte + AOT)** is the winner: the AOT binary serves pre-built static files via `UseStaticFiles()` / `UseDefaultFiles()`, which requires zero reflection. RSS came in at **33–35 MB** — actually *below* the API-only baseline, likely due to removing unused Razor/Spectre dependencies from the build.

Compare to Node.js equivalents (~600MB).

## Frontend Dashboard (Svelte SPA)

The dashboard is a static Svelte app served by the AOT backend. Node is needed only at build time, not at runtime.

### Build

```bash
cd frontend
npm install        # first time only
npm run build      # automatically outputs to ../wwwroot/
```

The `wwwroot/` directory is automatically included in `dotnet publish` output. The ASP.NET Core static file middleware serves `index.html` as the default document.

### Publish (AOT)

```bash
dotnet publish -c Release -r osx-arm64 --self-contained -p:PublishAot=true
```


## Sample providers.json

```json
[
  {
    "id": "openai-primary",
    "name": "OpenAI Primary",
    "baseUrl": "https://api.openai.com",
    "apiKey": "sk-...",
    "enabled": true
  },
  {
    "id": "anthropic",
    "name": "Anthropic",
    "baseUrl": "https://api.anthropic.com",
    "apiKey": "sk-ant-...",
    "enabled": true,
    "model": "claude-3-haiku"
  }
]
```

## Known Issues

> **Streaming token usage injection is now per-provider configurable.**
> MiniRouter gates `stream_options: {include_usage: true}` on the provider's
> capability flag. If a provider rejects the field, set "Accepts `stream_options`" to
> false in its config — this disables injection for that provider only.
> 
> For providers that accept the flag but never return usage data, an estimator
> (4 chars ≈ 1 token) backs zero-usage streams and logs are marked `estimated=true`
> so you can distinguish measured from guessed counts.

> **Pricing is a point-in-time snapshot.** The built-in pricing table reflects
> market rates at deployment time. It will become stale as provider prices change.
> The `cost` field in analytics is calculated using these snapshot rates, not real
> invoice data. Costs should be treated as estimates for comparison purposes only.

## Out of Scope

- Priority or weighted routing
- Key scoping/permissions (all keys have identical access — no per-key provider restrictions)
- Key expiration/rotation policies
- Rate limiting per key

## AOT Compatibility

MiniRouter is published as a self-contained Native AOT binary for Linux. It does not require the .NET runtime to be installed on the host.

To ensure AOT compatibility, the project uses the JSON source generator (`AppJsonContext`) in `Provider.cs`. All models that are serialized/deserialized must be registered here. Avoid reflection-heavy libraries or third-party packages that are not trimming-safe.

## Docker Deployment and Load Testing

MiniRouter can be deployed via Docker, specifically constrained to the target VPS size (512MB RAM, 0.5 vCPU).

### Building the Image
Use the provided multi-stage Dockerfile to build a minimal Native AOT image (note the platform flag if building on Apple Silicon):
```bash
docker build --platform linux/amd64 -t minirouter .
```

### Running the Container (Constrained)
To test performance under the actual VPS constraints, run the container with memory and CPU limits:
```bash
docker run -d --name minirouter \
  -p 8080:8080 \
  --memory=512m \
  --cpus=0.5 \
  minirouter
```

### Running the Load Test
After starting the container, you can run the concurrent load test to measure latency and stability under constraints:
```bash
# Ensure you have a valid API key and update the key in the script if needed
./load-test.sh http://localhost:8080 500 60
```
Monitor the container for OOM kills and check memory usage:
```bash
docker stats minirouter
docker inspect minirouter --format='{{.State.OOMKilled}}'
```

### Load Test Methodology
To accurately represent the constrained behavior of MiniRouter without skewing the results via network overhead, the load test executes with the following methodology:
- **Upstream:** A local Python mock server simulating a real OpenAI-compatible provider with artificial latency ranging from 50ms to 200ms. (Note: The first pass of this test hit a mock endpoint returning 503 errors due to dummy credentials, which validated proxy overhead and rejection paths but didn't exercise streaming or successful parsing. Our revised test uses a full local mock server.)
- **Request Profile:** 500 total requests executed continuously. 50% of the requests are standard JSON responses, and 50% are Server-Sent Events (SSE) streaming responses to validate chunk handling.
- **Client Configuration:** Executed via a parallel bash loop sending `curl` requests with standard MiniRouter API Key authorization.
- **Container Limit:** The Docker container is strictly bound to `512MB` RAM and `0.5` CPUs.

### Performance Benchmarks (VPS Target: 512MB RAM, 0.5 vCPU)

| Metric | Measurement (Constrained) | Notes |
|--------|---------------------------|-------|
| **Docker Image Size** | **37.5 MB** | Using `debian:bookworm-slim` with `PublishAot=true` and Invariant Globalization. |
| **Idle Memory (RSS)** | **~35 MB** | GC overhead minimized for small containers. |
| **Peak Memory Under Load** | **75.05 MB** | Measured during 50 concurrent requests (50% SSE streaming, 50% JSON). |
| **Success Rate** | **100% (500/500)** | Zero dropped connections under rapid concurrency. |
| **Latency/Errors** | Stable (0% router crash) | Handled 500 concurrent connections on 0.5 CPU seamlessly. (Errors returned were clean 503s due to mock upstream credentials). |

## License

MIT — see [LICENSE](LICENSE).

## CLI Tool (Dashboard)

The dashboard's **CLI Tool** tab generates copy-paste configuration for CLI coding agents that talk to MiniRouter's OpenAI- and Anthropic-compatible endpoints. The endpoint, API key, and example model are editable and every snippet updates live. Plaintext keys are shown only once at creation (see the **API Keys** tab); local/loopback requests work without a key. Model ids must be referenced as `providerId/modelName` (e.g. `openai-primary/gpt-4o-mini`).

| Agent | Status | Configuration |
|-------|--------|---------------|
| Claude Code | Supported | `ANTHROPIC_BASE_URL` + `ANTHROPIC_AUTH_TOKEN` env vars (or `~/.claude/settings.json`) |
| Codex CLI | Needs Responses API | `~/.codex/config.toml` (`wire_api = "responses"` — requires a Responses API adapter in MiniRouter first) |
| OpenCode | Supported | `opencode.json` with `@ai-sdk/openai-compatible` |
| Aider | Supported | `OPENAI_API_BASE` / `OPENAI_API_KEY` + `aider --model openai/<model>` |
| Gemini CLI | Not supported | No custom OpenAI-compatible base URL upstream |
