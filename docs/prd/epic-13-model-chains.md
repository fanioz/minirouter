# Epic 13: Model Chains (Named Fallback Sequences)

## Epic Goal

Allow operators to define named virtual model aliases — called **model chains** — that map a single user-facing name (e.g. `tier1`) to an ordered sequence of real `providerId/modelId` targets. When a caller sends `model: "tier1"`, MiniRouter executes the sequence as a waterfall: tries the first target, falls back to the second on failure, then the third, and so on.

**Created by:** @pm (Morgan)
**Date:** 2026-08-31
**Priority:** 2 — Medium (power-user routing feature; does not block existing flows)

---

## Background

MiniRouter already supports two routing modes in `ProxyService.ExecuteCompletionAsync`:

1. **Explicit routing** — `model: "providerA/gpt-4o"` (or comma-separated `"provA/m1,provB/m2"`) routes to a specific provider and falls through on 5xx/429.
2. **Round-robin** — `model: "gpt-4o"` (no `/`) distributes across all enabled providers whose model whitelist includes that name.

Neither mode lets operators define a **stable, named, ordered fallback chain**. The comma-separated syntax works but is:
- Opaque to callers (they must know internal provider IDs)
- Verbose in client config
- Not manageable through the UI
- Not discoverable via `/v1/models`

Model chains close this gap. The operator defines `tier1 → [anthropic/claude-opus-4-5, openrouter/kimi-3.0, openrouter/glm-5.3]` in the UI once. Callers just use `model: "tier1"`.

---

## User Flow

1. **Operator opens "Chains" tab** (new nav entry in Operate group).
2. **Clicks "New Chain"** → dialog opens.
3. **Fills in** chain name (`tier1`), optional description, and ordered list of `providerId/modelId` targets.
4. **Saves** → stored to `model_chains.json`.
5. **Any caller** sends `POST /v1/chat/completions` with `model: "tier1"`.
6. **MiniRouter resolves** `tier1` → sequence, executes waterfall, returns first successful response.
7. **Chain name appears** in `GET /v1/models` as a discoverable model ID.

---

## Scope

### In Scope (Story 13.1)

- `Models/ModelChain.cs` — `ModelChain` record, DTOs, AOT JSON registrations
- `Services/IModelChainService.cs` + `Services/ModelChainService.cs` — CRUD + in-memory resolve, `model_chains.json` persistence (mirrors `ProviderService` file pattern)
- `Services/ProxyService.cs` — chain resolution intercept at top of `ExecuteCompletionAsync`; refactor explicit loop into shared private method
- `Program.cs` — DI registration; 4 CRUD endpoints + append chain names to `GET /v1/models`
- `frontend/src/ModelChains.svelte` — new tab: list chains, add/edit/delete dialog
- `frontend/src/App.svelte` — add "Chains" tab to `operateTabs`

### Out of Scope

- **Recursive chains** (a chain whose member is itself another chain name): not supported in this story; chains resolve only to `providerId/modelId` entries.
- **Per-chain circuit breaker** state: circuit state continues to be tracked at the underlying `(providerId, modelId)` level, not at the chain level.
- **Analytics per chain name**: logs record the resolved `providerId/modelId` that was actually used, not the chain name.
- **Chain import from CSV/JSON file**: UI only in this story.

---

## Acceptance Criteria

1. `GET /api/model-chains` returns all chains as `[{ name, description, models }]`.
2. `POST /api/model-chains` creates a new chain; returns `400` if `name` is empty or already exists.
3. `PUT /api/model-chains/{name}` updates description or models list; returns `404` if not found.
4. `DELETE /api/model-chains/{name}` removes a chain; returns `404` if not found.
5. All four endpoints are protected by `ApiKeyEndpointFilter`.
6. `POST /v1/chat/completions` with `model: "tier1"` resolves to the chain's model list and executes as a waterfall — identical semantics to the existing comma-separated explicit routing loop.
7. Waterfall fallback rules match existing explicit routing: fall through only on 5xx or 429; stop immediately on success or 4xx (except 429).
8. If all chain members are exhausted (all failed or circuit-open), return the last error response.
9. Chain name `tier1` appears in `GET /v1/models` response as `{ id: "tier1", object: "model", owned_by: "chain" }`.
10. A chain name must not shadow an existing `providerId/modelId` slug or round-robin model name (validation: `name` must not contain `/`).
11. `model_chains.json` is persisted atomically (temp-file + rename, same pattern as `providers.json`).
12. UI "Chains" tab lists all chains with name, description, member count, and an expandable ordered list of targets.
13. UI allows create, edit (name locked post-creation, description + models editable), and delete with confirm dialog.
14. All new and existing tests pass; `dotnet build` clean; `npm run build` clean.

---

## Technical Design Summary

**Intercept point in `ProxyService.ExecuteCompletionAsync`:**
```
model: "tier1"
  → does not contain '/'
  → IsModelChain("tier1") == true           ← new: check IModelChainService
  → resolve → ["anthropic/claude-opus-4-5", "openrouter/kimi-3.0", "openrouter/glm-5.3"]
  → ExecuteExplicitChainAsync(targets, ...)  ← refactored from existing explicit loop
  ← first successful response, or last error

model: "anthropic/claude-opus-4-5"          ← existing explicit routing — unchanged
model: "gpt-4o"                             ← existing round-robin — unchanged
```

**Persistence: `model_chains.json`**
```json
[
  {
    "name": "tier1",
    "description": "Production tier fallback chain",
    "models": [
      "anthropic/claude-opus-4-5",
      "openrouter/kimi-3.0",
      "openrouter/glm-5.3"
    ]
  }
]
```

---

*Epic created by @pm (Morgan) · 2026-08-31*
