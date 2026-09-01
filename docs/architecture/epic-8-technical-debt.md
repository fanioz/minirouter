# Epic 8 — Technical Debt Resolution (Architecture)

> **Epic goal:** Resolve the high-impact technical-debt items identified during brownfield analysis. The epic ran as a series of stories; this document captures the **resulting architecture** of each.

This epic's stories:

| # | Story | Outcome |
|---|-------|---------|
| 8.1 | Restore test compilation | xUnit test project re-enabled; `MiniRouter.Tests.csproj` runs `dotnet test` clean |
| 8.2 | Atomic provider writes | `ProviderService` writes `providers.json` via temp-file + rename; no partial state on crash |
| 8.3 | Error classification + backoff | `Services/ErrorClassifier.cs` — fixed vs backoff cooldowns, `Retry-After` / `resets_at` parsing |
| 8.4 | Provider capability flags | `Provider.SupportsStreamOptions` + `Provider.ReportsStreamUsage` toggles `stream_options` injection per provider |
| 8.5 | Provider / connection split | `TestConnectionDto`, `POST /api/providers/test` endpoint, `CreateProviderDto` lost direct `ApiKey` validation responsibility |
| 8.6 | Anthropic messages translation | `Services/Translation/*` — JsonNode-based request/response/stream translators |
| 8.7 | Cost calculation | `Services/PricingTable.cs` — three-tier lookup, `cost_estimated` flag on logs |

---

## 8.1 — Restore Test Compilation

The test project was previously excluded from the main `MininRouter.csproj` to keep the AOT binary small. This story re-enabled the test project as a **separate** csproj that the AOT publish does not consume.

- `Tests/MiniRouter.Tests.csproj` — xUnit, references the main project as a project reference.
- `MininRouter.csproj` keeps `<Compile Remove="Tests\**" />` and similar entries for `Pages\` and `frontend\`.

## 8.2 — Atomic Provider Writes

`ProviderService` previously read-modify-wrote `providers.json` under a single lock. The risk was a partial file on crash mid-write. The fix is the standard write-temp + rename pattern, all under `_fileLock`:

```csharp
var tempPath = _configPath + ".tmp";
File.WriteAllText(tempPath, json);
File.Move(tempPath, _configPath, overwrite: true);
```

This is atomic on POSIX and best-effort atomic on Windows (same-volume `Move` with `overwrite:true`).

## 8.3 — Error Classification & Backoff

`Services/ErrorClassifier.cs` is a pure-function module. Inputs: `int status`, `string? errorText`. Output: `ErrorClassification(ErrorKind Kind, TimeSpan Cooldown)`.

**Rule precedence:** text rules first (case-insensitive `Contains`), then status rules, then default (30s transient). The text and status rule tables are private static arrays — easy to extend.

**`ErrorKind` semantics:**

| Kind | Cooldown | Used for |
|------|----------|----------|
| `Fixed` | 2 min for auth/permission/not-found, 5s for "request not allowed" | 401/402/403/404, "no credentials", "improperly formed request", "request not allowed" |
| `Backoff` | Exponential: 2s × 2^level, capped at 5 min, 15 levels | 429, "rate limit", "too many requests", "quota exceeded", "capacity", "overloaded" |

**Provider-reported resets:** `ResolveProviderReportedReset(retryAfterHeader, errorBody, now)` parses both `Retry-After` (seconds or HTTP-date) and JSON `resets_at` (Unix seconds or ms), caps at 30 min, and returns the larger of the two.

**Circuit-breaker integration:** `RecordFailure` calls `ErrorClassifier.Classify(...)`. `Fixed` errors set `CooldownUntil = now + rule.Cooldown`. `Backoff` errors call `ComputeBackoffCooldown(BackoffLevel)` and `NextBackoffLevel(...)` to advance the exponential ladder. The circuit's `ConsecutiveFailures` only decrements on a success, not on cooldown expiry.

## 8.4 — Provider Capability Flags

`Provider` gained two nullable booleans:

| Field | Meaning | Behaviour when `null` (default) |
|-------|---------|---------------------------------|
| `SupportsStreamOptions` | Whether the upstream accepts `stream_options: {include_usage: true}` | Inject the flag — opt-out per provider |
| `ReportsStreamUsage` | Whether the upstream **returns** usage data when asked | Treat as true; rows may be marked `estimated=1` later |

The `ProxyService` reads these flags when constructing the request body. If `SupportsStreamOptions == false`, it strips the `stream_options` block before forwarding. If the upstream is then observed not to return usage (no `usage` field in the final chunk), the `LogService` writes the log row with `estimated=1` and uses a 4-chars-per-token estimator for token counts.

The per-provider `PresetId` field is also a product of this epic (used by Epic 9 presets).

## 8.5 — Provider / Connection Split

Before 8.5, `CreateProviderDto` carried validation responsibilities. The story split them:

- `TestConnectionDto(BaseUrl, ApiKey)` — sent to `POST /api/providers/test` only.
- `CreateProviderDto` keeps the create shape but trusts the `baseUrl` is well-formed at write time (the endpoint validates with `Uri.TryCreate` and rejects non-http/https schemes).

This means the form can validate the connection before submit without round-tripping through a real provider record.

## 8.6 — Anthropic Messages Translation

The translation layer lives in `Services/Translation/`. Three classes, all `JsonNode`-based:

### `AnthropicRequestTranslator.ToOpenAI(JsonNode req)`

Converts an Anthropic Messages body to an OpenAI Chat Completions body:

- `system` (string or array of `{type:"text", text:"..."}` blocks) → first message with `role: "system"`, content flattened.
- `messages[]` with `content` as string → pass-through. `content` as array (text / image / tool_use / tool_result blocks) → expand into the appropriate OpenAI shape. `tool_use` blocks become assistant messages with `tool_calls[]`. `tool_result` blocks become follow-up `role:"tool"` messages referencing the tool call id.
- `tools[]` — Anthropic `input_schema` becomes OpenAI `function.parameters` (a `JsonElement` carried through unchanged).
- `max_tokens` (Anthropic requires this) → `max_tokens` (OpenAI optional).
- `stream: true` is preserved.

### `AnthropicResponseTranslator.ToAnthropic(JsonNode openAiResp)`

Converts an OpenAI non-streaming response to the Anthropic `Message` shape:

- `id`, `type:"message"`, `role:"assistant"`, `model`, `content: [{type:"text", text:"..."}]` (built from `choices[0].message.content`).
- `stop_reason` mapped from `choices[0].finish_reason` (`stop` → `end_turn`, `length` → `max_tokens`, `tool_calls` → `tool_use`).
- `usage.input_tokens` / `output_tokens` from `usage.prompt_tokens` / `usage.completion_tokens`. `cache_read_input_tokens` / `cache_creation_input_tokens` left null (OpenAI doesn't surface these).

### `AnthropicStreamTranslator`

SSE re-emitter. The class holds no state between chunks; per-stream state lives in `AnthropicStreamState` (a POCO carried on a `ConcurrentDictionary<string, AnthropicStreamState>` keyed by a per-request id).

For each OpenAI `data: {...}` chunk:

- `choices[0].delta.role == "assistant"` → emit `message_start`.
- `choices[0].delta.content` → ensure a text content block is open (emitting `content_block_start` once), then emit `content_block_delta` with `delta:{type:"text_delta", text:"..."}`.
- `choices[0].delta.tool_calls` → open a `content_block_start` with `type:"tool_use"`, buffer the streamed `arguments` JSON string, emit a `content_block_delta` for each new `arguments` chunk, then `content_block_stop` once the openai stream marks `finish_reason:"tool_calls"`.
- `choices[0].finish_reason` → emit `message_delta` with `delta:{stop_reason, ...}` and (if present) `usage`.
- Stream end (`data: [DONE]`) → emit `message_stop` and close any open content blocks.

**Why `JsonNode` instead of typed DTOs?** The wire keys are `snake_case` and the round-trip has subtle differences (e.g. `function.parameters` is an arbitrary JSON Schema, not a typed object). Carrying `JsonNode` keeps the field names explicit and bypasses `System.Text.Json`'s source-generated naming policy for the wire layer. The trade-off is some manual `obj["x"]?.ToString()` calls.

## 8.7 — Cost Calculation

`Services/PricingTable.cs` is a static lookup table of `(Model, InputRatePerMillion, OutputRatePerMillion)`. Resolution is three-tier:

1. **Exact match** — `model == rate.Model` (case-sensitive).
2. **Pattern match** — `rate.Model.EndsWith("*")` → strip the `*` and check `model.StartsWith(prefix, OrdinalIgnoreCase)`.
3. **Provider default** — `(provider_default, 0.50, 2.00)` always matches.

If no match, `CalculateCost(...)` returns `null`, not zero. The `LogService` writes a `null` cost and sets `cost_estimated=1` in the `request_log` row.

`PricingTable` is documented as a point-in-time snapshot. Future stories may move it to a per-provider config field or a JSON file loaded at startup.
