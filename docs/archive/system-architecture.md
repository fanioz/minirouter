# MiniRouter — System Architecture (Brownfield Snapshot)

> **Note:** The canonical, up-to-date architecture is now in [`docs/architecture.md`](../architecture.md). This file is kept as a historical "what was here when the brownfield analysis was done" reference, with the live behaviour appended as a delta.

This document captured the **current state** of the MiniRouter codebase as a brownfield asset — including technical debt, workarounds, and real-world patterns — when Aria (Architect) did the initial pass on 2026-08-06. It serves as a reference for AI agents working on enhancements or bug fixes.

## Document Scope

Comprehensive documentation of the entire system as a multi-provider Native AOT LLM Router.

## Change Log

| Date | Version | Description | Author |
|------|---------|-------------|--------|
| 2026-08-06 | 1.0 | Initial brownfield analysis | Aria (Architect) |
| 2026-08-28 | 1.1 | Live delta: API keys, presets, Anthropic translation, circuit breaker, log retention | Sync with code |

---

## Quick Reference — Key Files and Entry Points

### Critical Files for Understanding the System

- **Main Entry & Endpoints**: `Program.cs` — Handles DI setup, configuration, **all** routes (provider CRUD, presets, keys, logs, analytics, `/models`, `/v1/chat/completions`, `/v1/messages`), and the CLI dispatch.
- **Project Configuration**: `MininRouter.csproj` — `net10.0`, `PublishAot=true`, frontend build hook, tests excluded.
- **Provider Management**: `Services/ProviderService.cs` — Thread-safe in-memory provider list, per-(provider,model) circuit breaker, atomic JSON file writes.
- **API Key Management**: `Services/ApiKeyService.cs` — SHA-256-hashed keys, `IMemoryCache` fast path, SQLite-backed.
- **Proxy**: `Services/ProxyService.cs` — Streaming-aware forwarder, Anthropic↔OpenAI translation dispatch, error classification, cost logging.
- **Translation**: `Services/Translation/` — Three `JsonNode`-based translators (request, response, stream).
- **Presets**: `Services/Presets/` — Hardcoded `PresetCatalog` + `PresetService` (enable, list, suggest models).
- **Logging**: `Services/LogService.cs` + `Services/LogRetentionService.cs` — SQLite request log + pruning hosted service.
- **Auth Filter**: `Filters/ApiKeyEndpointFilter.cs` — `IEndpointFilter` on ingress endpoints.
- **Data Models**: `Models/Provider.cs` (and `ApiKey.cs`, `RequestLog.cs`, `ProxyExecution*.cs`) — Records, DTOs, and the `AppJsonContext` for AOT-safe source-generated JSON.

## High Level Architecture

### Technical Summary

MiniRouter is a minimalistic API reverse proxy built with ASP.NET Core Minimal APIs. It routes requests for **OpenAI Chat Completions** (`/v1/chat/completions`) and **Anthropic Messages** (`/v1/messages`) to various backend LLM providers. The model field `providerId/modelName` selects the target provider explicitly; the prefix is stripped before forwarding. It stores provider configurations in a flat JSON file on disk, API keys and request logs in SQLite. Published as a self-contained Native AOT binary.

### Actual Tech Stack

| Category | Technology | Version | Notes |
|----------|------------|---------|-------|
| Runtime | .NET / ASP.NET Core | 10.0 | Native AOT enabled (`PublishAot=true`), `InvariantGlobalization=true` |
| Serialization | System.Text.Json | 10.0 | **Source generators only** — no reflection |
| Database | SQLite via `Microsoft.Data.Sqlite` | 10.0.10 | `minirouter.db` for `api_keys` and `request_log` |
| Config storage | JSON file | N/A | `providers.json` with `ReaderWriterLockSlim` + `SemaphoreSlim` for atomic writes |
| CLI framework | Spectre.Console.Cli | 0.49.0 | `restart`, `providers list\|add\|edit\|delete` |
| Cache | `IMemoryCache` | built-in | API key lookups, aggregated `/models`, per-preset model lists |
| Frontend | Svelte 5 + Tailwind 4 + bits-ui | — | Built to `wwwroot/`, served as static files |

### Repository Structure Reality Check

- Type: Monorepo (Single App + Tests project)
- Package Manager: NuGet
- Notable: Endpoints live directly in `Program.cs`. No MVC, no Razor (both fail AOT in .NET 10).

## Source Tree and Module Organization

### Project Structure (Actual, 2026-08-28)

```text
minirouter/
├── Program.cs                            # Entry point, DI, all routes, CLI dispatch
├── MininRouter.csproj                    # net10.0, AOT, frontend build hook
├── appsettings.json                      # PORT, DbPath
├── providers.example.json                # Sample providers
│
├── Models/
│   ├── Provider.cs                       # Provider, DTOs, MaskedProvider, AppJsonContext, Anthropic types
│   ├── ApiKey.cs                         # ApiKey, CreateApiKeyDto, etc.
│   ├── RequestLog.cs                     # Per-request log + AnalyticsResponse
│   ├── ProxyExecutionRequest.cs          # Internal proxy carrier
│   └── ProxyExecutionResult.cs           # Internal proxy result carrier
│
├── Services/
│   ├── ProviderService.cs                # CRUD + round-robin + circuit breaker
│   ├── ApiKeyService.cs                  # Key create/validate, SHA-256, caching
│   ├── ProxyService.cs                   # Inbound proxy, streaming, routing
│   ├── LogService.cs                     # SQLite log, analytics rollups
│   ├── LogRetentionService.cs            # IHostedService — prunes old logs
│   ├── ErrorClassifier.cs                # Status/text → ErrorKind + cooldown math
│   ├── PricingTable.cs                   # Static cost model
│   ├── IProviderService.cs / IApiKeyService.cs / ILogService.cs / IProxyService.cs
│   ├── Translation/
│   │   ├── AnthropicRequestTranslator.cs # Anthropic → OpenAI request
│   │   ├── AnthropicResponseTranslator.cs
│   │   └── AnthropicStreamTranslator.cs
│   └── Presets/
│       ├── PresetCatalog.cs              # 4 hardcoded presets
│       ├── PresetService.cs              # Enable / list / discover
│       ├── ProviderPreset.cs
│       └── IPresetService.cs
│
├── Cli/
│   ├── Commands/                         # Spectre commands
│   ├── Infrastructure/                   # DI bridge
│   └── ProcessManagement/                # Locates + restarts the running server
│
├── Filters/
│   └── ApiKeyEndpointFilter.cs           # IEndpointFilter on ingress routes
│
├── wwwroot/                              # Built Svelte SPA
├── frontend/                             # Svelte source
│
├── Tests/                                # xUnit (excluded from main csproj)
│
├── docs/                                 # This directory
├── Dockerfile                            # Multi-stage AOT, debian:bookworm-slim
├── docker-compose.yml
├── load-test.sh
└── memory-measure.sh
```

### Key Modules and Their Purpose

- **Router Proxy**: `Services/ProxyService.cs`. Buffers the request, parses `model`, looks up the provider by prefix, optionally rewrites the `model` field per the provider override, optionally translates Anthropic→OpenAI, forwards via `HttpClient`, pipes the raw response (streaming-safe) back. Classifies errors via `ErrorClassifier`, records success/failure on the circuit, and writes a `request_log` row.
- **Provider Service**: `Services/ProviderService.cs`. Thread-safe CRUD on `providers.json` with a `ReaderWriterLockSlim` (reads) + `SemaphoreSlim(1,1)` (write section). Maintains `ConcurrentDictionary<string, int>` for per-(provider,model) round-robin indices and `ConcurrentDictionary<string, CircuitState>` for the circuit breaker. Atomic writes via temp-file + rename.
- **API Key Service**: `Services/ApiKeyService.cs`. Generates `sk-` + 64 hex chars. Stores SHA-256 hash + 8-char prefix. Validates via `IMemoryCache` (sliding 5 min) → falls back to SQLite.
- **Log Service**: `Services/LogService.cs`. SQLite-backed. Schema includes `api_key_id`, `model`, `estimated` (token count source flag), `cost`, `cost_estimated`. Analytics rollups per provider and per key.
- **Presets**: `Services/Presets/`. Hardcoded catalog + service that validates connection, generates a unique ID (`presetId` or `presetId-N`), and creates a regular `Provider` record.
- **Translation**: `Services/Translation/`. Three `JsonNode`-based translators. No typed DTOs for wire shapes — `JsonNode` keeps `snake_case`/`camelCase` explicit and AOT-safe.

## Data Models and APIs

### Data Models

See `Models/`. Highlights:

- **Provider** — `Id, Name, BaseUrl, ApiKey, Enabled, Model?, Models?, SupportsStreamOptions?, ReportsStreamUsage?, PresetId?`
- **MaskedProvider** — Same shape, `ApiKey` replaced by `***<last4>` for safe exposure.
- **ApiKey** — `Id, Name, KeyHash, KeyPrefix, CreatedAt, Enabled, LastUsedAt?`. Plaintext is never stored.
- **RequestLog** — `Id, Timestamp, ProviderId, Success, ErrorMessage?, TokensIn?, TokensOut?, LatencyMs, ApiKeyId?, Model?, Estimated, Cost?, CostEstimated`.
- **JsonContext** — `AppJsonContext` (AOT) and `AnthropicAppJsonContext` (AOT). **Every** DTO that crosses a wire boundary must be registered here.

### API Specifications

Live in `Program.cs`:

**Proxy (authenticated via `Authorization: Bearer sk-...`):**
- `POST /v1/chat/completions` — OpenAI Chat Completions ingress.
- `POST /v1/messages` — Anthropic Messages ingress. Translated to OpenAI before forwarding.

**Provider CRUD (unauthenticated):**
- `GET /api/providers`, `GET /api/providers/{id}`, `POST /api/providers`, `PUT /api/providers/{id}`, `DELETE /api/providers/{id}`.
- `POST /api/providers/test` — `GET {baseUrl}/v1/models` with 10s timeout, no persistence.

**Presets:**
- `GET /api/presets` — list with `ConnectedCount` per preset.
- `POST /api/presets/{presetId}/enable` — validate + create provider.
- `GET /api/presets/{presetId}/models` — cached (10 min) suggested models.

**Keys (unauthenticated):**
- `GET /api/keys`, `POST /api/keys` (returns plaintext **once**), `PUT /api/keys/{id}`, `DELETE /api/keys/{id}`.

**Logs & analytics (unauthenticated):**
- `GET /api/logs?providerId=&apiKeyId=&model=&success=&limit=`
- `GET /api/analytics/tokens?providerId=`
- `GET /api/analytics/keys?apiKeyId=`

**Other:**
- `GET /health` — liveness.
- `GET /models` — aggregated upstream model list, cached for `MODELS_CACHE_TTL_SECONDS`.
- `POST /_shutdown` — graceful self-stop (used by the CLI restart command).

## Technical Debt and Known Issues

### Resolved (originally listed as debt in 1.0)

1. ~~**Global Round-Robin Counter**~~ — Now keyed per `(providerId, modelName)` via `ConcurrentDictionary`.
2. ~~**File-Based Persistence Concurrency**~~ — Atomic temp-file + rename. Still single-instance (file lock doesn't span processes).
3. ~~**Endpoint Logic Coupling**~~ — Proxy logic extracted to `Services/ProxyService.cs`. `Program.cs` is now thin route registration.
4. ~~**Body Buffering**~~ — Still buffers (required to inspect `model` and translate). Documented as a known constraint, not actively being removed.

### Active debt (2026-08-28)

1. **Management endpoints unauthenticated.** `/api/providers`, `/api/keys`, `/api/logs`, `/api/analytics/*` have no auth. Acceptable for local; needs reverse-proxy auth before internet exposure.
2. **Streaming token usage estimator.** When `SupportsStreamOptions=true` but `ReportsStreamUsage=false`, costs are estimated (`4 chars ≈ 1 token`) and flagged `estimated=1` in the log.
3. **Pricing snapshot.** `PricingTable` is a static point-in-time table. `cost_estimated` flag is set when the model is not in the table.
4. **No cross-provider retries.** Explicit routing makes one upstream attempt per request. Failures propagate to the client.
5. **Request body fully buffered at ingress.** Required for translation + model inspection; problematic for huge multimodal payloads.

## Integration Points and External Dependencies

### External Services

| Service | Purpose | Integration Type | Key Files |
|---------|---------|------------------|-----------|
| Any OpenAI-compatible API | Upstream chat completions | REST API Proxy | `ProxyService.cs` |
| Any Anthropic-compatible API | Upstream messages (via translation) | REST API Proxy | `Translation/*Translator.cs` |
| Preset upstreams (OpenCode, OpenRouter, DeepSeek, Groq) | Curated model sources | REST API | `Presets/PresetCatalog.cs` |

### Internal Integration Points

- **JSON file store** (`providers.json`) — `ProviderService`.
- **SQLite** (`minirouter.db`) — `ApiKeyService`, `LogService`.
- **Memory cache** — `ApiKeyService` (per-key 5 min), `/models` aggregate (configurable TTL), per-preset model lists (10 min).

## Development and Deployment

### Local Development Setup

1. `cp providers.example.json providers.json`
2. `dotnet run` (or `dotnet watch run` for hot reload)
3. Frontend: `cd frontend && npm install && npm run build` (auto-run by `dotnet build`)

### Build and Deployment Process

- **Native AOT Linux**: `dotnet publish -c Release -r linux-x64 --self-contained -p:PublishAot=true -o ./publish`
- **Native AOT macOS**: `dotnet publish -c Release -r osx-arm64 --self-contained -p:PublishAot=true -o ./publish-osx`
- **Docker**: `docker build --platform linux/amd64 -t minirouter .` then `docker run -d --memory=512m --cpus=0.5 -p 8080:8080 minirouter`
- Cross-compiling AOT from macOS to Linux requires a Linux linker.

## Testing Reality

### Current Test Coverage

- **Unit tests** (xUnit, in `Tests/`): `ApiKeyServiceTests`, `ProviderServiceTests` (CRUD, round-robin, circuit breaker), `ProxyServiceTests` (streaming, model routing, error mapping), `PricingTableTests`, `ErrorClassifierTests` (text + status rules, backoff math, retry-after parsing), `PresetServiceTests`, `ApiKeyEndpointFilterTests`.
- **E2E / load tests**: `load-test.sh` (500 concurrent, 50% streaming) + `scripts/mock_upstream.py`.
- **Memory benchmarks**: `memory-measure.sh` across build configurations.
- **Manual**: `curl` against running server, as documented in `README.md`.

## Appendix — Useful Commands and Scripts

### Frequently Used Commands

```bash
dotnet run                                       # dev mode
dotnet test                                      # all unit tests
dotnet publish -c Release -r osx-arm64 --self-contained -p:PublishAot=true
./publish-osx/MininRouter                        # run production binary
./publish-osx/MininRouter status                 # CLI: is server up?
./publish-osx/MininRouter list providers         # CLI: list configured providers
./publish-osx/MininRouter restart                # CLI: graceful restart
```

### Debugging and Troubleshooting

- **Proxy failures**: Watch stdout for `[Proxy]` messages. Check `LogRetentionService` hasn't pruned the relevant log yet.
- **Serialization issues**: Any new DTO must be registered in `AppJsonContext` in `Models/Provider.cs`.
- **AOT crash at startup**: Likely a reflection-based call. Use `JsonSerializer.Serialize/Deserialize` only with `AppJsonContext.Default.*`.
- **Provider JSON edits ignored**: Hot-reload not supported. Restart the server.
