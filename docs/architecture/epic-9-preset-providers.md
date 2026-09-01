# Epic 9 — Preset Providers (Architecture)

> **Epic goal:** Add a curated catalog of preset providers that users can enable with one click. Inspired by [OmniRoute's](docs/reff) preset registry.

This epic's stories:

| # | Story | Outcome |
|---|-------|---------|
| 9.1 | Add preset providers | `Services/Presets/` + `/api/presets` + `/api/presets/{id}/enable` + `/api/presets/{id}/models` |

---

## 9.1 — Preset Providers

A **preset** is a template, not a live provider. Enabling a preset creates a regular `Provider` record pre-filled from the template. The same preset can be enabled multiple times — each instance is fully editable and independent of the template.

### Data model (`Services/Presets/ProviderPreset.cs`)

```csharp
public enum PresetCategory { Free, ApiKey }

public record PresetDisplay(
    string ColorHex,
    string TextIcon,
    string? WebsiteUrl = null,
    string? ApiKeyUrl = null,
    string? Notice = null
);

public record ProviderPreset(
    string Id,
    string Name,
    PresetCategory Category,
    string BaseUrl,
    bool ApiKeyRequired,
    string? DefaultApiKey,
    string? ModelsUrl,
    string? ModelsFilter,
    List<string> DefaultModels,
    PresetDisplay Display
);

public record PresetInfo(
    string Id,
    string Name,
    PresetCategory Category,
    string BaseUrl,
    bool ApiKeyRequired,
    string? ModelsUrl,
    List<string> DefaultModels,
    PresetDisplay Display,
    int ConnectedCount    // <-- populated by PresetService.GetAllAsync
);

public record EnablePresetRequest(string? ApiKey, string? Name);
```

A new nullable field on `Provider` — `PresetId` — records the template a provider was created from. This is the join key for `ConnectedCount` and is what the UI uses to badge providers that came from a preset.

### Catalog (`Services/Presets/PresetCatalog.cs`)

Static, hardcoded list of 4 providers:

| Id | Name | Category | Auth | Models |
|---|---|---|---|---|
| `opencode-free` | OpenCode Free | `Free` | None — uses `"public"` placeholder | Dynamic fetch from `https://opencode.ai/zen/v1/models`, filtered to `-free` suffix + `big-pickle` |
| `openrouter` | OpenRouter | `ApiKey` | Required | Dynamic fetch from `https://openrouter.ai/api/v1/models`, filtered to `pricing.prompt == 0 && pricing.completion == 0` |
| `deepseek` | DeepSeek | `ApiKey` | Required | Static: `deepseek-chat`, `deepseek-reasoner` |
| `groq` | Groq | `ApiKey` | Required | Static: `llama-3.3-70b-versatile`, `meta-llama/llama-4-maverick-17b-128e-instruct`, `qwen/qwen3-32b`, `openai/gpt-oss-120b` |

The catalog comment notes the model follows OmniRoute's `docs/reff/open-sse/providers/registry/*.js` pattern.

### Service (`Services/Presets/PresetService.cs`)

Three operations:

#### `GetAllAsync() → List<PresetInfo>`

1. Read all providers via `IProviderService.ListProvidersUnmaskedAsync()`.
2. Group by `PresetId`, count.
3. Map each `ProviderPreset` to a `PresetInfo` with the `ConnectedCount` (0 if the preset isn't in the group).

#### `EnablePresetAsync(presetId, apiKey?, name?) → Provider`

1. Look up the preset by id; throw `KeyNotFoundException` if missing.
2. **If `ApiKeyRequired` and `apiKey` is empty/whitespace → `ArgumentException`.**
3. **Validate the connection:** `GET {BaseUrl}/v1/models` with the effective key, 5s timeout. On failure → `InvalidOperationException` with the error message (e.g., "Connection failed with status 401. ...").
4. Generate a unique provider id — `presetId` if free, otherwise `presetId-2`, `presetId-3`, etc.
5. Build a `CreateProviderDto` with the preset's defaults and call `IProviderService.CreateProviderAsync(...)`. The new `Provider` carries `PresetId = preset.Id`.

#### `GetSuggestedModelsAsync(presetId) → List<string>`

1. Look up the preset. Throw `KeyNotFoundException` if missing.
2. If `ModelsFilter == null` → return the hardcoded `DefaultModels` list. (DeepSeek, Groq.)
3. Otherwise:
   - Build the URL: `preset.ModelsUrl ?? "{BaseUrl}/v1/models"`.
   - Check `IMemoryCache` under key `preset_models_{presetId}`. Return cached if present.
   - GET the URL with the preset's `DefaultApiKey` (if any) as Bearer, 10s timeout.
   - Apply the filter:
     - `opencode-free` → keep ids ending in `-free` or in the `KnownFreeOpenCodeModels` set (currently `{ "big-pickle" }`).
     - `openrouter-free` → keep ids where `pricing.prompt == 0 && pricing.completion == 0` (parse with `InvariantCulture`).
     - Unknown filter → keep everything.
   - Cache the result for 10 minutes, return.

### Endpoints

| Method | Path | Body | Behaviour |
|--------|------|------|-----------|
| `GET` | `/api/presets` | — | `PresetService.GetAllAsync()` |
| `POST` | `/api/presets/{presetId}/enable` | `EnablePresetRequest` | Validates → creates provider → returns `201 Created` with the masked `Provider` |
| `GET` | `/api/presets/{presetId}/models` | — | Cached suggested models list |

`POST /api/presets/{presetId}/enable` also evicts the `aggregated_models` cache so the next `/models` call reflects the new provider.

### Why hardcoded instead of a config file?

- The catalog is small and changes infrequently; the release cadence matches `docs/reff/open-sse/providers/registry/*.js` (a JSON file there, versioned with the code).
- AOT-safe — no JSON file load, no schema migration.
- Trivial to add a new preset: one entry in `PresetCatalog.All`, the rest of the system picks it up automatically.

If the catalog grows beyond ~20 presets or starts to vary per-deployment, the natural next step is to move it to a JSON file loaded at startup with a `PresetService` override of the in-memory list.
