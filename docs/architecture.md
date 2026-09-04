# MiniRouter — System Architecture

This document describes the **current state** of the MiniRouter codebase. It is the canonical architectural reference for new contributors and AI agents. It is maintained alongside the code — when behaviour changes, update this file in the same change.

> **Scope:** the live C# backend (`Program.cs`, `Services/`, `Models/`, `Cli/`, `Filters/`) and the pre-built Svelte SPA in `wwwroot/`. For the historical "what was added in epic X" view, see the story files in `docs/stories/`.

---

## 1. What MiniRouter Is

MiniRouter is a self-contained reverse proxy for LLM APIs. A single Native AOT binary:

- Terminates HTTP at a configurable port (default `8080`).
- Exposes both **OpenAI Chat Completions** (`/v1/chat/completions`) and **Anthropic Messages** (`/v1/messages`) ingress endpoints.
- Translates between wire formats on demand — an Anthropic client can hit MiniRouter and have its request transparently forwarded to an OpenAI-compatible upstream.
- Routes requests to one of N pre-configured providers based on the `model` field (`providerId/modelName`).
- Persists providers in a JSON file, API keys + request logs in a SQLite database, and serves a static Svelte SPA dashboard for management.
- Authenticates inbound requests with hashed bearer tokens via an `IEndpointFilter`.

**Non-goals (explicitly out of scope):** priority/weighted routing, per-key provider restrictions, key rotation policies, per-key rate limiting.

---

## 2. Tech Stack

| Layer | Technology | Notes |
|-------|------------|-------|
| Runtime | .NET 10 / ASP.NET Core 10 | `PublishAot=true`, `InvariantGlobalization=true` |
| HTTP | Minimal APIs | No MVC, no Razor — both fail AOT in .NET 10 |
| Serialization | `System.Text.Json` | **Source generators only** — reflection stripped by AOT |
| Persistence (providers) | JSON file | `providers.json` (path via `PROVIDERS_CONFIG_PATH`) |
| Persistence (state) | SQLite via `Microsoft.Data.Sqlite` 10.0.10 | `minirouter.db` — `api_keys`, `request_log` |
| Cache | `IMemoryCache` | Aggregated `/models` response + per-preset model lists |
| CLI | `Spectre.Console.Cli` 0.49.0 | Used for `restart` and `providers *` subcommands |
| Frontend | Svelte 5 + Tailwind 4 + bits-ui | Built to `wwwroot/`, served by `UseStaticFiles()`. Visual language governed by the [MiniRouter Neutral Modern design system](./frontend/design/README.md). |

### Critical AOT constraints

- **Never** use `JsonSerializer.Serialize(obj)` without a `JsonTypeInfo` — must go through `AppJsonContext.Default.*`.
- **No reflection** — no `Activator.CreateInstance`, no `Type.GetType`, no `Assembly.LoadFrom`. The reason MVC/Razor was removed.
- **No Razor Pages** — `System.TypeLoadException` at startup due to MVC's runtime `ApplicationParts` discovery.
- Every DTO that crosses a wire boundary must be registered in `AppJsonContext` (see `Models/Provider.cs:83-112`).

---

## 3. Solution Layout

```text
minirouter/
├── Program.cs                         # Entry point, DI, all routes, CLI dispatch
├── MininRouter.csproj                 # net10.0, AOT, frontend build hook
├── appsettings.json                   # ASP.NET defaults (PORT, DbPath)
├── providers.example.json             # Sample providers (real one is gitignored)
│
├── Models/
│   ├── Provider.cs                    # Provider, DTOs, MaskedProvider, AppJsonContext, Anthropic types
│   ├── ApiKey.cs                      # ApiKey, CreateApiKeyDto, CreateApiKeyResponse, etc.
│   ├── RequestLog.cs                  # Per-request log + AnalyticsResponse
│   ├── ProxyExecutionRequest.cs       # Internal proxy invocation carrier
│   └── ProxyExecutionResult.cs        # Internal proxy result carrier
│
├── Services/
│   ├── ProviderService.cs             # CRUD + round-robin + per-(provider,model) circuit breaker
│   ├── ApiKeyService.cs               # API key creation, SHA-256 hash, validation, caching
│   ├── ProxyService.cs                # Inbound proxy logic, streaming, model routing
│   ├── LogService.cs                  # SQLite request log, analytics rollups
│   ├── LogRetentionService.cs         # IHostedService — prunes old logs on a timer
│   ├── ErrorClassifier.cs             # Status code / body text → ErrorKind + cooldown
│   ├── PricingTable.cs                # Static cost model (snapshot rates)
│   ├── IProviderService.cs / IApiKeyService.cs / ILogService.cs / IProxyService.cs
│   │
│   ├── Translation/                   # Anthropic ↔ OpenAI wire format
│   │   ├── AnthropicRequestTranslator.cs   # Anthropic → OpenAI request
│   │   ├── AnthropicResponseTranslator.cs  # OpenAI → Anthropic response
│   │   └── AnthropicStreamTranslator.cs     # OpenAI SSE → Anthropic SSE
│   │
│   └── Presets/                       # Curated provider templates
│       ├── PresetCatalog.cs           # 4 hardcoded presets (OpenCode, OpenRouter, DeepSeek, Groq)
│       ├── PresetService.cs           # Enable / list / discover models
│       ├── ProviderPreset.cs          # Records + DTOs
│       └── IPresetService.cs
│
├── Cli/
│   ├── Commands/                      # Spectre.Console.Cli commands
│   │   ├── RestartCommand.cs
│   │   ├── ProvidersListCommand.cs
│   │   ├── ProvidersAddCommand.cs
│   │   ├── ProvidersEditCommand.cs
│   │   └── ProvidersDeleteCommand.cs
│   ├── Infrastructure/                # DI bridge (TypeRegistrar, TypeResolver) for Spectre
│   └── ProcessManagement/             # Locates the running server, restarts it gracefully
│
├── Filters/
│   └── ApiKeyEndpointFilter.cs        # IEndpointFilter on /v1/chat/completions and /v1/messages
│
├── wwwroot/                           # Built Svelte SPA (gitignored output, served as static)
├── frontend/                          # Svelte source — built before dotnet build
│
├── Tests/                             # xUnit tests (excluded from main csproj)
│   ├── ApiKeyServiceTests.cs
│   ├── ProviderServiceTests.cs
│   ├── ProxyServiceTests.cs
│   ├── PricingTableTests.cs
│   ├── ErrorClassifierTests.cs
│   ├── PresetServiceTests.cs
│   └── ApiKeyEndpointFilterTests.cs
│
├── docs/                              # This directory
├── scripts/                           # Helper shell scripts
├── Dockerfile                         # Multi-stage AOT build, debian:bookworm-slim
├── docker-compose.yml
├── load-test.sh                       # 500 concurrent requests, 50% streaming
└── memory-measure.sh                  # RSS benchmarks across configs
```

---

## 4. Request Flow

### 4.1 Outbound (client → upstream)

```text
Client
  │
  ├── POST /v1/chat/completions   (OpenAI body, model="openai-primary/gpt-4o-mini")
  │     or
  └── POST /v1/messages           (Anthropic body, model="claude-3-haiku")
        │
        ▼
  ApiKeyEndpointFilter
    - reads Authorization: Bearer sk-...
    - SHA-256 hashes, looks up in IApiKeyService (MemoryCache fast path)
    - injects apiKeyId into HttpContext.Items["ApiKeyId"]
        │
        ▼
  ProxyService.HandleChatCompletionAsync(ctx)       (or HandleAnthropicMessagesAsync)
    1. Buffer request body, parse JsonNode
    2. Extract requestedModel + stream flag
    3. Look up provider by prefix: "openai-primary/gpt-4o-mini" → provider[openai-primary]
    4. Verify circuit-breaker state is not Open
    5. If Anthropic ingress → AnthropicRequestTranslator.ToOpenAI(req)
    6. If provider.Model is set → rewrite the request's `model` field
    7. Add Authorization: Bearer <provider.ApiKey>
    8. Forward via HttpClient to {BaseUrl}/v1/chat/completions
    9. For streaming: pipe raw bytes via ctx.Response.BodyWriter
       For non-streaming: buffer JSON, optionally translate to Anthropic shape, write
    10. On response: classify via ErrorClassifier, record success/failure
    11. Log to LogService (with apiKeyId, providerId, model, tokens, latency, cost)
```

### 4.2 Inbound (management)

- `GET /health` — liveness probe.
- `GET/POST/PUT/DELETE /api/providers[/{id}]` — CRUD. `CreateProviderDto` validates `baseUrl` is http/https.
- `POST /api/providers/test` — live `GET {baseUrl}/v1/models` check (10s timeout, never persists).
- `GET /api/presets` — list curated providers with their `ConnectedCount`.
- `POST /api/presets/{presetId}/enable` — validate connection, then create a provider pre-filled from the preset (auto-suffix if the ID is taken).
- `GET /api/presets/{presetId}/models` — cached list (10 min) of suggested models for that preset, filtered by `ModelsFilter`.
- `GET/POST/PUT/DELETE /api/keys[/{id}]` — API key management.
- `GET /api/logs?providerId=&apiKeyId=&model=&success=&limit=` — last N log rows.
- `GET /api/analytics/tokens?providerId=` — token rollup per provider.
- `GET /api/analytics/keys?apiKeyId=` — token rollup per key.
- `GET /models` — aggregated upstream model list, cached for `MODELS_CACHE_TTL_SECONDS`.
- `POST /_shutdown` — graceful self-stop (used by the CLI `restart` command).

The Svelte SPA calls the `/api/*` paths directly. **All management endpoints are currently unauthenticated** (see README "Known Issues" / security note).

---

## 5. Routing & Circuit Breaker

`ProviderService.GetNextProviderAsync` performs the routing:

1. **Explicit routing (default):** `model` must be `providerId/modelName`. The provider is looked up directly, the prefix is stripped from the upstream call, and the provider's own `Model` override (if set) is applied as a second rewrite.
2. **No cross-provider retries** in explicit mode — a failure on `openai-primary/gpt-4o-mini` is reported back to the client as-is.

A **per-(providerId, modelName) circuit breaker** is maintained in `ConcurrentDictionary<string, CircuitState>`. Each request:

- Returns `Healthy` or `HalfOpen` immediately.
- `Open` ⇒ rejects with `503 Service Unavailable` without an upstream call.
- On success: resets `ConsecutiveFailures = 0`, closes the circuit.
- On failure: `RecordFailure(providerId, modelName, status, body, retryAfter)` runs `ErrorClassifier.Classify(status, body)`:
  - **Fixed** errors (401/402/403/404, "no credentials", "improperly formed request", etc.) — set `CooldownUntil = now + 2 minutes`.
  - **Backoff** errors (429, "rate limit", "quota exceeded", "capacity", "overloaded") — exponential backoff starting at 2s, capped at 5 minutes and 15 levels. `Retry-After` headers and provider-reported `resets_at` are honoured, capped at 30 minutes.
  - Anything else — 30s transient cooldown.

`ErrorClassifier.ComputeBackoffCooldown(level)` returns the cooldown for a given backoff level. The circuit transitions back to `HalfOpen` when `CooldownUntil` elapses; the next request is allowed through, and success closes the circuit.

---

## 6. Translation Layer (Anthropic ↔ OpenAI)

Implemented in `Services/Translation/`. **JsonNode-based** rather than typed DTOs, so wire keys stay explicit and AOT-safe (no reflection on `JsonElement`, no DTO field-name collision with `snake_case` ↔ `camelCase`).

- `AnthropicRequestTranslator.ToOpenAI(JsonNode)` — converts `system` (string or array of text blocks), `messages` (string or content-block arrays, including tool_use/tool_result), and `tools` (Anthropic input_schema → OpenAI `function.parameters`).
- `AnthropicResponseTranslator.ToAnthropic(JsonNode)` — converts an OpenAI non-streaming response into Anthropic's `Message` shape (`id`, `type`, `role`, `content[]`, `stop_reason`, `usage`).
- `AnthropicStreamTranslator` — converts OpenAI SSE `data: {...}` chunks into Anthropic SSE events (`message_start`, `content_block_start`, `content_block_delta`, `content_block_stop`, `message_delta`, `message_stop`, `ping`). Tracks per-stream state in `AnthropicStreamState` (text block index, thinking block, tool calls with arg buffers, usage).

The two ingress paths share `ProxyService` plumbing — the only difference is the `Ingress` enum on `ProxyExecutionRequest` and which translator is invoked.

---

## 7. Persistence

### 7.1 Providers (`providers.json`)

`ProviderService` keeps the active list in memory behind a `ReaderWriterLockSlim`. CRUD operations:

- Acquire a `SemaphoreSlim(1,1)` (`_fileLock`) for the entire read-modify-write cycle so concurrent writes can't interleave.
- Read the file, deserialize via `AppJsonContext.Default.ListProvider`.
- Apply the mutation in memory.
- Serialize the **full** list back via `AppJsonContext.Default.ListProvider` and write atomically (write-temp + rename).
- `_modelIndices` and `_circuitStates` are updated in-memory only.

The file is read once at startup via `LoadProvidersAsync`. Hot edits to the JSON are not picked up — restart the server.

### 7.2 API keys & logs (`minirouter.db`)

SQLite via `Microsoft.Data.Sqlite`. Two tables:

- `api_keys(id PK, name, key_hash, key_prefix, created_at, enabled, last_used_at)` — `key_hash` is SHA-256 of the plaintext token. The plaintext is returned exactly once at creation.
- `request_log(id PK, timestamp, provider_id, success, error_message, tokens_in, tokens_out, latency_ms, api_key_id, model, estimated, cost, cost_estimated)` — one row per request.

`LogRetentionService` (an `IHostedService`) runs on a timer and prunes old rows. `LogService.GetAnalyticsAsync` rolls up tokens and cost per provider; `GetKeyAnalyticsAsync` per key.

---

## 8. Preset System

`Services/Presets/PresetCatalog.cs` is a **hardcoded** list of four curated providers:

| Id | Name | Category | Auth |
|---|---|---|---|
| `opencode-free` | OpenCode Free | `Free` | None (uses `"public"` placeholder) |
| `openrouter` | OpenRouter | `ApiKey` | Required |
| `deepseek` | DeepSeek | `ApiKey` | Required |
| `groq` | Groq | `ApiKey` | Required |

`PresetService.EnablePresetAsync(presetId, apiKey?, name?)`:

1. Look up the preset.
2. If `ApiKeyRequired`, reject an empty key with `ArgumentException`.
3. `ValidateConnectionAsync` — `GET {BaseUrl}/v1/models` with 5s timeout. On failure, return the error string to the caller as `InvalidOperationException`.
4. Generate a unique provider ID — `presetId` if free, `presetId-2`, `presetId-3`, etc.
5. Build a `CreateProviderDto` and call `ProviderService.CreateProviderAsync`.

`GetSuggestedModelsAsync` is preset-aware:

- For `opencode-free` and `openrouter` (`ModelsFilter` set), it fetches `{ModelsUrl}/v1/models` live and applies a filter (`-free` suffix for OpenCode, `pricing.prompt == 0 && pricing.completion == 0` for OpenRouter). Results are cached for 10 minutes under `preset_models_{id}`.
- For DeepSeek and Groq, returns the hardcoded `DefaultModels` list.

`GetAllAsync` decorates each preset with `ConnectedCount` — how many `Provider` records currently have `PresetId == preset.Id`. This powers the dashboard "you already have N of these configured" UI.

---

## 9. CLI

The single binary is also a CLI. `Program.cs:142-241` dispatches:

- `-m <model> [-p <prompt>]` (or stdin pipe) → one-shot completion, prints the model's reply to stdout. In this mode the binary **starts an in-process Minimal API host** (no listening socket), loads providers, runs the request, prints, exits. Reuses the production DI graph so behaviour is identical to the server path.
- `restart` (Spectre command) — locate the running server via `.minirouter.pid`, send `POST /_shutdown`, wait for the process to exit, then spawn a fresh copy.
- `providers list|add|edit|delete` (Spectre branch) — talk to the running server's `/api/providers` endpoints. These are **client** commands; the server must already be running.
- `status` — `GET /health`, prints RUNNING / STOPPED.
- `list providers` — `GET /api/providers`, prints a table (id, name, enabled).
- `list models` — `GET /models`, prints aggregated upstream models grouped by provider with circuit-status badges.

The CLI uses `Spectre.Console.Cli`'s `ITypeRegistrar` bridge in `Cli/Infrastructure/TypeRegistrar.cs` to wire `IServiceCollection` registrations.

---

## 10. Configuration

Environment variables (read in `Program.cs` and `appsettings.json`):

| Variable | Default | Purpose |
|---|---|---|
| `PORT` | `8080` | ASP.NET Core listen port |
| `PROVIDERS_CONFIG_PATH` | `./providers.json` | Provider JSON file |
| `MAX_RETRIES` | `2` | (Reserved — current code uses explicit routing; no cross-provider retries) |
| `CIRCUIT_FAILURE_THRESHOLD` | `3` | Consecutive failures before opening circuit |
| `CIRCUIT_COOLDOWN_SECONDS` | `30` | Default transient cooldown |
| `MODELS_CACHE_TTL_SECONDS` | `300` | TTL for aggregated `/models` cache |
| `DbPath` (in appsettings) | `minirouter.db` | SQLite path |

---

## 11. Testing

- `Tests/` — xUnit. Excluded from the main `MininRouter.csproj` so it doesn't bloat the AOT binary.
- Coverage: `ApiKeyServiceTests`, `ProviderServiceTests` (CRUD, circuit breaker, round-robin), `ProxyServiceTests` (streaming, model routing, error mapping), `PricingTableTests`, `ErrorClassifierTests` (text + status rules, backoff math, retry-after parsing), `PresetServiceTests`, `ApiKeyEndpointFilterTests`.
- `load-test.sh` — bash + curl, 500 concurrent requests, 50% streaming, against `scripts/mock_upstream.py`.
- `memory-measure.sh` — RSS measurement across build configurations.

---

## 12. Build & Deployment

```bash
# macOS (host)
dotnet publish -c Release -r osx-arm64 --self-contained -p:PublishAot=true -o ./publish-osx

# Linux (native build or Docker)
dotnet publish -c Release -r linux-x64 --self-contained -p:PublishAot=true -o ./publish

# Docker
docker build --platform linux/amd64 -t minirouter .
docker run -d --name minirouter -p 8080:8080 --memory=512m --cpus=0.5 minirouter
```

The csproj has a `BuildFrontend` target that runs `npm run build` before `BeforeBuild`, so `dotnet build` always produces a fresh `wwwroot/`. The frontend source files are listed in `FrontendSourceFiles` so MSBuild's up-to-date check skips the rebuild when nothing changed.

AOT cross-compile from macOS to Linux requires a Linux linker — build on Linux or in Docker.

---

## 13. Known Constraints (live in code, not "debt")

- **Management endpoints are unauthenticated.** Acceptable for local dev; needs an auth proxy before internet exposure.
- **Pricing is a static snapshot.** `PricingTable` is hardcoded; stale by design. `cost_estimated` flags the cost as estimated whenever a model isn't in the table.
- **Streaming token usage estimation.** Providers that accept `stream_options: {include_usage: true}` but never return usage get a `4 chars ≈ 1 token` estimator and the log row is flagged `estimated=1`.
- **Body buffering at ingress.** The proxy reads the full request body before forwarding — fine for chat, problematic for huge multimodal payloads.
- **Single-instance only.** Provider JSON is file-locked; running two processes against the same file will fight.

---

## 14. Pointers

- `docs/archive/brownfield-architecture.md` — earlier snapshot of the system before API keys / presets / Anthropic translation.
- `docs/architecture-dark-mode.md` — frontend dark-mode epic.
- `docs/architecture-restart-server.md` — CLI restart epic.
- `docs/stories/` — per-story write-ups (Epic 5 = API keys, Epic 7 = comma-separated fallback, Epic 8 = technical debt round 1, Epic 9 = presets).
- `docs/po-validation-report.md` — recent PO validation of the current docs.
