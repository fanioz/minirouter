# 9Router Reference Gap Analysis

**Project:** MiniRouter (.NET Native AOT)
**Reference:** 9Router v0.5.50 (`docs/reff`)
**Date:** 2026-08-11
**Version:** 1.0

---

## Executive Summary

MiniRouter and 9Router are not the same class of product. 9Router is ~129,000 LOC of
JavaScript spanning 119 provider definitions, 29 upstream executors, and a bidirectional
protocol translator. MiniRouter is ~2,878 LOC of C# plus ~2,183 LOC of Svelte,
implementing one inbound protocol against generic OpenAI-compatible upstreams.
That is roughly a **25x difference in surface area**.

The single structural gap is **protocol translation**. Every other gap is additive —
more providers, more strategies, more dashboards. Translation is different in kind:
without it, MiniRouter can only ever route to upstreams that already speak OpenAI Chat
Completions.

This report is an inventory, not a backlog. MiniRouter's stated value is a 35 MB single
binary that survives a $5/month VPS. 9Router requires Node plus a 129k-LOC dependency
tree. Importing its surface area wholesale would delete the reason MiniRouter exists.
Section 10 separates what is worth adopting from what should be explicitly declined.

### Key Metrics

| Metric | Value |
|---|---|
| Feature domains compared | 8 |
| Structural gaps (require redesign) | 1 |
| Gaps recommended for adoption | 7 |
| Gaps recommended for explicit decline | 13 |
| Pre-existing defects found | 6 |
| Critical defects (security / build) | 2 |

### Scale Comparison

| Measure | MiniRouter | 9Router |
|---|---|---|
| Backend LOC | 2,878 (C#) | ~129,000 (JS) |
| Frontend LOC | 2,183 (Svelte) | Next.js dashboard, 30+ routes |
| Provider definitions | 0 (fully generic) | 119 registry files |
| Upstream executors | 1 generic path | 29 |
| OAuth integrations | 0 | 24 |
| Protocol translators | 0 | 22 (12 request, 10 response) |
| DB tables | 2 | 11 |
| Unit tests | 12 C# (do not compile) + 4 Svelte | 163 + snapshot baselines |
| Runtime footprint | ~35 MB RSS, single binary | Node runtime + node_modules |

---

## 1. Protocol Support — Structural Gap

| | MiniRouter | 9Router |
|---|---|---|
| Inbound formats | OpenAI Chat Completions only | 13 formats |
| Translation | **None** | Bidirectional pivot + direct routes |
| LLM endpoints | 1 | 14+ |

9Router's 13 formats: `openai`, `openai-responses`, `openai-response`, `claude`,
`gemini`, `gemini-cli`, `vertex`, `codex`, `antigravity`, `kiro`, `cursor`, `ollama`,
`commandcode` (`docs/reff/open-sse/translator/formats.js:2`).

### Endpoints we do not have

`/v1/responses`, `/v1/responses/compact`, `/v1/messages`, `/v1/messages/count_tokens`,
`/v1beta/models/*:generateContent`, `/v1/embeddings`, `/v1/images/generations`,
`/v1/audio/{speech,transcriptions,voices}`, `/v1/videos/{generations,edits,extensions,[id]}`,
`/v1/search`, `/v1/web/fetch`, `/v1/models/{kind}`, `/v1/models/info`.

### Translator architecture (`docs/reff/open-sse/translator/`)

- **Pivot design** — OpenAI is the intermediate format. `source -> openai -> target`
  for requests, `target -> openai -> source` for responses.
- **Direct routes** — a translator registered on an exact `source:target` pair skips the
  lossy double hop. Used for fragile pairs involving thinking blocks, tool IDs,
  non-base64 images, and `is_error` propagation (e.g. `claude:kiro`, `kiro:claude`).
- **Self-registration** — `register(from, to, reqFn, resFn)` as an import side effect.
- **`concerns/`** — shared cross-format logic: thinking blocks, tool calls, modality,
  usage, finish reasons, chunking, images, JSON.
- **`schema/`** — ROLE / BLOCK / finishReason enums; format strings are never hardcoded.

Non-pivotable upstreams are handled inside their own executor rather than the
translator: Kiro AWS EventStream, Cursor protobuf, CommandCode NDJSON.

### Consequence for MiniRouter

Every configured provider must itself speak OpenAI Chat Completions.
`providers.example.json` lists `https://api.anthropic.com` as a sample entry — that
provider would be called at `https://api.anthropic.com/v1/chat/completions` with a
`Bearer` header, which is not the Anthropic API contract. **The sample config cannot
work as written.**

MiniRouter is a same-protocol load balancer. 9Router is a translating gateway.

---

## 2. Provider and Credential Model

| Dimension | MiniRouter | 9Router |
|---|---|---|
| Provider definition | 7-field record, fully generic | 119 declarative registry files |
| Upstream adapters | 1 generic path | 29 executors |
| OAuth | **None** | 24 providers |
| Token refresh | **None** | Proactive + background + reactive |
| Credentials per provider | Exactly 1 static `apiKey` | N connections |
| Custom endpoints | providers.json rows | `providerNodes` with routing prefix |
| Auth header styles | `Bearer` only | Per-registry `auth: {header, scheme}` |
| Model capabilities | None | Per-model capability matrix |
| Pricing metadata | None | Per-model / provider / pattern |

### Registry entry shape (`docs/reff/open-sse/providers/REGISTRY_TEMPLATE.js`)

Identity (`id`, `alias`, `category`, `authType`, `hasOAuth`, `noAuth`), `display`,
`transport` (`baseUrl`, `format`, static headers, auth scheme, `forceStream`, `quirks`,
per-status `retry`, `modelsFetcher`, regions), `oauth` (client ID, authorize/token/device
/refresh URLs, PKCE method, redirect port, refresh encoding), and `media`
(`serviceKinds: llm | tts | stt | embedding | image | imageToText | webSearch`).

The `quirks` and per-status `retry` fields are the notable part: provider-specific
misbehavior is **declarative data**, not branching code.

### OAuth providers (24)

`claude`, `codex`, `xai`, `grok-cli`, `gemini-cli`, `antigravity`, `iflow`, `qoder`,
`github`, `kiro`, `cursor`, `kimi`, `kilocode`, `cline`, `clinepass`, `gitlab`,
`codebuddy-cn`, `codebuddy-intl`, `kimchi`, `trae`, `windsurf`, `zed` (+2 aliases).
Flow types: `authorization_code_pkce`, `device_code`, plain authorization code.

Token refresh runs on three cooperating layers — proactive pre-request check,
periodic background refresh, and reactive 401/403 retry with up to 3 attempts.
Rotating refresh tokens (xAI, grok-cli issue a new RT per refresh) are handled by
mutating credentials between attempts.

### Provider vs Connection — worth adopting independently

- **Provider** = a *type* definition. Endpoint, format, auth scheme, models, pricing,
  capabilities. No secrets.
- **Connection** = one *credential instance* — a row in `providerConnections` carrying
  `priority`, `isActive`, `lastUsedAt`, `consecutiveUseCount`, `backoffLevel`,
  `testStatus`, `lastError`, `modelLock_*`, and a `providerSpecificData` JSON blob.

This split is what makes multi-account rotation possible. MiniRouter's one-key-per-provider
shape cannot express it, and this constraint propagates into the routing layer (section 3).

---

## 3. Routing and Resilience

The closest domain, but the resilience layer is materially weaker.

| Feature | MiniRouter | 9Router |
|---|---|---|
| Explicit `provider/model` | Yes | Yes |
| Fallback chain | Comma-separated in `model` field | Named, persisted **combos** |
| Round-robin | Per-model counter over providers | Combo-level **and** account-level, with sticky limits |
| Named model groups | No | `combos` table, per-combo strategy override |
| Fusion / multi-model | No | Parallel fan-out + judge model synthesis |
| Capability routing | No | Turn scanned for vision/pdf/audio/video, candidates re-tiered |
| Capacity fallback pools | No | Global per-capability pools appended behind candidates |
| Model aliases | Dormant code only (`README.md:84`) | `aliasRepo` + `/api/models/alias` |
| Circuit breaker scope | Per `(provider, model)` | Per `(account, model)` |
| Failure classification | Fixed threshold | Config-driven `ERROR_RULES`, text-then-status |
| Backoff | None — flat cooldown | Exponential, 15 levels, 5 min cap |
| Honors `Retry-After` / `resets_at` | **No** | Yes, capped at 30 min |
| Half-open probing | Nominal only | Per-model lock expiry |
| Warmup/naming request bypass | No | `bypassHandler` short-circuits before rotation |

### Combo strategies (`docs/reff/open-sse/services/combo.js`)

| Strategy | Behavior |
|---|---|
| `fallback` (default) | Try models in order until one returns 2xx |
| `round-robin` | Rotate start index, with sticky limit of N consecutive requests |
| `fusion` | Fan out to all panel models in parallel, judge model synthesizes one answer |

Fusion uses quorum-grace collection (`minPanel: 2`, `stragglerGraceMs: 8000`,
`panelHardTimeoutMs: 90000`), degrading to a direct answer at 1 response and 503 at 0.

### Error classification (`docs/reff/open-sse/config/errorConfig.js:59`)

Text rules matched first, then status rules:

- text: `no credentials` (2 min), `request not allowed` (5 s),
  `improperly formed request` (2 min), `rate limit` / `too many requests` /
  `quota exceeded` / `capacity` / `overloaded` -> exponential backoff
- status: 401/402/403/404 -> 2 min; 429 -> backoff
- unmatched -> 30 s transient cooldown

Backoff is `2000ms * 2^(level-1)`, capped at 5 min, max level 15. `backoffLevel` persists
on the connection and resets to 0 on success.

Provider-reported reset times **override** computed backoff — Codex
`usage_limit_reached.resets_at`, capped at 30 min. A GitHub-specific rule treats premium
request exhaustion (402 + matching text) as account-wide until the next UTC month.

### MiniRouter circuit breaker findings

Reviewing `Services/ProviderService.cs:280-361`:

- `GetCircuitStatus` returns only `"healthy"` or `"open ({n}s)"`. There is no
  `"half-open"` value — and the explicit-target path rejects anything not exactly
  `"healthy"` (`Services/ProxyService.cs:143`).
- After cooldown expires the provider re-enters rotation with `ConsecutiveFailures`
  still at or above the threshold. One further failure re-trips it immediately; one
  success fully closes it. There is no probe-count gate, so the "half-open" comment
  overstates the actual behavior.
- Ignoring `Retry-After` on 429 is the most likely source of real-world rotation
  thrash against rate-limited providers.

---

## 4. Observability

| Feature | MiniRouter | 9Router |
|---|---|---|
| Per-request log | `request_log` table | `usageHistory` + `requestDetails` |
| Full request/response capture | No | Yes — client raw -> body -> OpenAI intermediate -> target -> error |
| Cost / pricing | **Absent** | Model / provider / pattern tables + user overrides |
| Provider quota polling | No | Live quota from 15+ provider APIs |
| Aggregation | 2 unbounded `GROUP BY` | `usageDaily` rollups + `getUsageStats(period)` + `getChartData` |
| Live updates | No | SSE `/api/usage/stream` with in-flight tracking |
| Latency detail | Total only | `ttft` + total |
| Token accounting | `prompt_tokens` / `completion_tokens` | Normalize -> max-merge -> canonicalize -> per-format filter |
| Cache token breakdown | No | Cache read + cache creation |
| Estimator fallback | No | `~4 chars/token`, +2000 buffer tokens |
| Retention | 30-day prune, hourly | Configurable observability caps |

### Usage extraction (`docs/reff/open-sse/utils/usageTracking.js`)

Handles every upstream shape: Claude `message_start`/`message_delta` (with cache tokens),
OpenAI Responses `response.completed`, OpenAI/DeepSeek `prompt_cache_hit_tokens`,
Gemini `usageMetadata` including Antigravity's nested wrapper, Ollama NDJSON
`prompt_eval_count`.

Four-stage pipeline:

1. `normalizeUsage` — coerce to finite numbers
2. `mergeUsage` — field-wise **max** merge, because Anthropic splits usage across events
3. `canonicalizeUsage` — one storage convention (`prompt_tokens` includes cache read +
   creation); idempotent, with a discriminator so Claude folds once and OpenAI passes through
4. `filterUsageForFormat` — emit only fields legal for the client's format

### Bearing on our known `stream_options` issue

`README.md:285-296` defers the streaming usage-injection problem. The reference confirms
the shape of the fix: **9Router does not inject globally.** It declares per-provider
quirks in the registry and falls back to `estimateUsage` when a provider reports nothing.
That is the pattern to copy — see section 10.

---

## 5. Authentication and Security — Weakest Area

| | MiniRouter | 9Router |
|---|---|---|
| Dashboard auth | **None** | bcrypt login + `jose` JWT session |
| Login rate limiting | N/A | `loginLimiter` |
| SSO | None | Full OIDC (start / callback / test) |
| Client API keys | SHA-256, unsalted | Stored key + optional `machineId` binding |
| Admin endpoint protection | **None** | Session-guarded |
| Key revocation latency | Up to 5 minutes | Immediate |

### Exposure detail

Only `POST /v1/chat/completions` is protected (`Program.cs:453-456`). Open to anyone who
can reach the port:

- `GET/POST/PUT/DELETE /api/providers` — including overwriting `baseUrl`
- `GET/POST/PUT/DELETE /api/keys` — including minting new keys
- `GET /api/logs`, `/api/analytics/*`
- `POST /_shutdown` — stops the process

`Dockerfile:26` sets `ASPNETCORE_URLS=http://+:8080`, binding all interfaces. `README.md:89`
acknowledges the management endpoints are unauthenticated, but the risk is larger than the
note implies: `PUT /api/providers/{id}` can repoint `baseUrl` to an attacker-controlled
host, turning the router into a prompt-harvesting relay while continuing to serve traffic.

API key hashing (SHA-256, unsalted, single round) is defensible here — the token is a
256-bit random value, so brute force is infeasible regardless of KDF. It would not be
acceptable for user-chosen secrets.

---

## 6. Persistence

| | MiniRouter | 9Router |
|---|---|---|
| Provider storage | `providers.json`, full-file rewrite, **non-atomic** | SQLite `providerConnections` |
| Engine | SQLite for logs + keys only | SQLite for everything |
| Driver strategy | `Microsoft.Data.Sqlite` | 4-adapter fallback chain |
| Tuning | Defaults | WAL, `synchronous=NORMAL`, mmap 30 MB, 64 MB cache, `busy_timeout` |
| Migrations | Ad-hoc `ALTER TABLE` in try/catch | `SCHEMA_VERSION` + pre-change backup + declarative sync |
| Tables | 2 | 11 |

9Router's adapter chain: `bun:sqlite` -> `better-sqlite3` (optional native) ->
`node:sqlite` (Node >= 22.5) -> `sql.js` (pure JS, always works). Not relevant to us —
.NET has no equivalent problem — but the **schema versioning with pre-change backup** is.

### 9Router tables

`_meta`, `settings`, `providerConnections`, `providerNodes`, `proxyPools`, `apiKeys`,
`combos`, `kv` (scoped key-value for aliases / disabled models / custom models / pricing
overrides), `usageHistory`, `usageDaily`, `requestDetails`.

The recurring pattern: a few indexed scalar columns plus a `data TEXT` JSON blob for
everything else. Worth noting for AOT — it keeps the source-generated JSON context small.

### MiniRouter risks

- Every provider CRUD operation re-serializes the full list and calls
  `File.WriteAllTextAsync` (`Services/ProviderService.cs:176,211,242`). No
  temp-file-and-rename, so a crash mid-write truncates the config.
- `providers.json` in the repo root holds plaintext provider API keys, and
  `Dockerfile:12` does `COPY . .`.

---

## 7. Auxiliary Subsystems (Absent Entirely)

Listed for completeness. All are peripheral to a router's core job.

| Subsystem | What it is | Default state in 9Router |
|---|---|---|
| RTK token savers | Compresses `tool_result` content (git-diff, grep, ls, tree, truncate) | **ON** |
| pxpipe | Renders bulky context as images to cut tokens | Off |
| headroom | External `/v1/compress` proxy, fails open | Off |
| caveman / ponytail | System-prompt injectors for terser output | Off |
| MITM | Local HTTPS intercepting proxy with cert generation | Off |
| Tunnels | Cloudflare + Tailscale, with watchdog and health checks | Off |
| Proxy pools | Outbound relays + Deno/Vercel/Cloudflare deploy targets | Off |
| MCP bridge | stdio <-> SSE bridge | Off |
| Skills | 9 packaged skills | N/A |
| CLI-tools injection | Writes settings for ~17 coding agents | N/A |
| Media providers | TTS / STT / image / video / search catalogs | N/A |
| Self-updater | In-app version check and update | On |
| Translator playground | Interactive format-conversion UI | N/A |

**RTK token savers is the only one enabled by default**, making it the only arguably-core
item in this table. It is fail-open (errors return null, body untouched) and skips
`is_error` content to preserve traces.

---

## 8. Testing

| | MiniRouter | 9Router |
|---|---|---|
| Unit tests | 12 C# (**do not compile**) + 4 Svelte smoke | 163 |
| Regression harness | None | `tests/__baseline__/` snapshots |

9Router's baseline harness snapshots provider configs, model aliases, and OAuth URLs,
then verifies no regression against a `known-fails.txt` allowlist. This is the mechanism
that makes a 119-provider registry maintainable.

### MiniRouter coverage gaps

No tests for: circuit breaker (trip threshold, cooldown, recovery), `provider/model`
parsing, the comma-separated fallback chain, retry exhaustion, streaming, the
`include_usage` injection and suppression, `LogService` queries or pruning, the endpoint
filter, the CLI, or any HTTP route end-to-end.

---

## 9. Pre-Existing Defects

Found during comparison, independent of the gap analysis.

| # | Severity | Defect | Location |
|---|---|---|---|
| 1 | **Critical** | Entire admin surface unauthenticated, including `POST /_shutdown`, on an all-interfaces bind | `Program.cs:519`, `Dockerfile:26` |
| 2 | **Critical** | Test project does not compile — `ApiKeyService` gained an `IMemoryCache` parameter; 6 call sites use the old constructor | `Tests/ApiKeyServiceTests.cs:40,56,69,85,98,112` |
| 3 | High | Streaming sets `StatusCode`/`ContentType` after the body has started; no `HasStarted` guard | `Services/ProxyService.cs:82-83` |
| 4 | Medium | `Models.svelte` reads `json.data` / `owned_by`; `/models` returns a flat provider-group array. Page permanently empty | `frontend/src/Models.svelte:19,71` |
| 5 | Medium | `Analytics.svelte` reads `requestCount`; API sends `totalRequests`. Always renders 0 | `frontend/src/Analytics.svelte:105` |
| 6 | Medium | API key cache has no invalidation — disable/delete lags up to 5 minutes on the proxy path | `Services/ApiKeyService.cs:174` |

Defect 3 was found by inspection, not by running the code. `OnStreamChunkAsync` writes and
flushes to `BodyWriter` before the assignments at lines 82-83 execute, and ASP.NET Core
throws `InvalidOperationException` when response state is mutated after start. Worth
confirming with a single streaming request.

### Lower-severity items

- `docker-compose.yml` maps host `5050` to container `5000`; the image listens on `8080`.
  The published port does not reach the app.
- `Tests/ProxyServiceTests.cs` asserts auth behavior that has since moved to
  `ApiKeyEndpointFilter`. Two of three tests will fail once compilation is restored.
- Transitive `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 carries advisory NU1903 (high).
- `AGENTS.md` prescribes `npm run lint` / `typecheck` / `test` at the repo root, but there
  is no root `package.json`. Actual gates are `dotnet test Tests/MiniRouter.Tests.csproj`
  and `npm test` inside `frontend/`.

---

## 10. Recommendations

### Tier 1 — Fix First

No design work required. Days, not weeks.

1. Repair the test project constructor drift (defect 2). Quality gates are currently
   unenforceable, which blocks everything below.
2. Add dashboard auth and protect the admin surface; remove or guard `/_shutdown`
   (defect 1). The only item in this report that is a genuine liability rather than a
   missing feature.
3. Fix the streaming response-state ordering (defect 3).
4. Fix the two frontend field-name mismatches (defects 4, 5).
5. Invalidate the API key cache on update/delete (defect 6).

### Tier 2 — Worth Adopting

Real design work, but each preserves the low-footprint value proposition. Rough priority
order:

1. **Anthropic `/v1/messages` inbound -> OpenAI outbound.** Highest leverage single
   feature; unlocks Claude Code and compatible clients. Adopt the pivot pattern, but
   resist the full 13-format matrix — one translator pair, not twenty-two.
2. **Honor `Retry-After` / `resets_at`, add exponential backoff.** Smallest change with
   the largest resilience return. Directly addresses the rotation-thrash risk in section 3.
3. **Provider -> connection split**, so one provider can hold multiple credentials.
   Prerequisite for any account-level rotation.
4. **Move `providers.json` into SQLite**, or at minimum make writes atomic via
   temp-file-and-rename. Also removes plaintext keys from the repo root.
5. **Per-provider capability flags.** Resolves the `include_usage` known issue
   (`README.md:285-296`) properly rather than by global injection.
6. **Cost calculation** from a static per-model pricing table.
7. **Tests for the circuit breaker and routing paths**, which are currently the highest-risk
   untested logic.

### Tier 3 — Explicitly Decline

MITM, tunnels, proxy pools, pxpipe, headroom, caveman/ponytail, MCP bridge, skills,
media providers, CLI-tools injection, self-updater, translator playground, i18n.

Each is a separate product. Recording this decision in the `README.md` "Out of Scope"
section would stop these resurfacing in future planning.

### Explicitly Not Recommended

Do not pursue provider-registry parity. 119 declarative provider files is the correct
design *for a Node application with no binary-size constraint*. For a Native AOT binary,
every registry entry is compiled weight, and the source-generated JSON context grows with
each model. MiniRouter's generic-provider approach is the right trade for its constraints —
the gap is intentional and should stay that way.

---

## Appendix: Reference File Map

Key paths in `docs/reff` for follow-up work:

| Area | Path |
|---|---|
| Routing engine | `src/sse/handlers/chat.js`, `open-sse/handlers/chatCore.js` |
| Combos | `open-sse/services/combo.js` |
| Account selection | `src/sse/services/auth.js` |
| Circuit breaking | `open-sse/services/accountFallback.js` |
| Error rules | `open-sse/config/errorConfig.js` |
| Translation | `open-sse/translator/` (`index.js`, `request/`, `response/`, `concerns/`, `schema/`) |
| Format detection | `open-sse/translator/formats.js` |
| Provider registry | `open-sse/providers/registry/`, `REGISTRY_TEMPLATE.js` |
| Executors | `open-sse/executors/` |
| Pricing | `open-sse/providers/pricing.js` |
| Usage extraction | `open-sse/utils/usageTracking.js` |
| OAuth | `src/lib/oauth/providers/`, `src/lib/oauth/services/` |
| Token refresh | `open-sse/services/tokenRefresh.js`, `src/sse/services/backgroundTokenRefresh.js` |
| DB schema | `src/lib/db/schema.js`, `src/lib/db/repos/` |
| Token savers | `open-sse/rtk/` |
| Test baselines | `tests/__baseline__/` |
