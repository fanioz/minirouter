# Epic 11: OpenAI Responses API Adapter (Codex CLI Support)

## Epic Goal

Enable Codex CLI (v0.122+) to use MiniRouter as a drop-in provider by implementing the OpenAI Responses API (`POST /v1/responses`) as a translation layer over MiniRouter's existing Chat Completions routing infrastructure.

**Created by:** @pm (Morgan)  
**Date:** 2026-08-31  
**Priority:** 1 — High (closes the last gap blocking Codex CLI from using MiniRouter)

---

## Background

Story 10.1 added `x-api-key` header support, `AUTH_PASSTHROUGH` mode, and the `/v1/models` endpoint, bringing Claude Code to full working status against MiniRouter. Codex CLI was listed as a target but left blocked by a hard protocol incompatibility: Codex CLI ≥ 0.122 (released February 2026) **removed `wire_api = "chat"` entirely** — all custom model providers must now declare `wire_api = "responses"`, which means Codex POSTs exclusively to `/v1/responses` using the OpenAI Responses API shape. MiniRouter does not implement this endpoint.

The Responses API is meaningfully different from Chat Completions:

| Dimension | Chat Completions (`/v1/chat/completions`) | Responses (`/v1/responses`) |
|---|---|---|
| Input field | `messages[]` with `role`/`content` | `input` — string or `InputItem[]` with `type:message` items |
| System prompt | `messages[{role:system}]` | `instructions` top-level field |
| Response shape | `choices[0].message.content` | `output[0].content[0].text` (when `type=output_text`) |
| Response object | `object: "chat.completion"` | `object: "response"`, `status: "completed"` |
| Streaming events | `data: {"choices":[{"delta":{...}}]}` | Named SSE events: `response.created`, `response.output_text.delta`, `response.completed`, etc. |
| Tool calls | `choices[0].message.tool_calls[]` | `output[]` items with `type: "function_call"` |
| Usage | `prompt_tokens` / `completion_tokens` | `input_tokens` / `output_tokens` |
| Statefulness | Stateless | Optionally stateful via `previous_response_id` + `store` |

MiniRouter's approach — identical to how it handles Anthropic Messages — is to translate inbound Responses API requests to Chat Completions, forward to the selected upstream provider, then translate the response back into Responses API shape. No statefulness is implemented in the first story (Codex CLI sends `previous_response_id: null` by default in stateless mode).

---

## Scope

### In Scope (Story 11.1)

- `POST /v1/responses` endpoint accepting the Responses API request shape
- **Request translation:** `input` (string or `InputItem[]`) + `instructions` → `messages[]`
- **Response translation (non-streaming):** `choices[0].message` → `output[0]` with `type: message`, `content[0].type: output_text`
- **Response translation (streaming):** Chat Completions SSE chunks → Responses API SSE event sequence (`response.created` → `response.output_item.added` → `response.output_text.delta` → `response.output_text.done` → `response.output_item.done` → `response.completed`)
- **Tool call translation:** `function_call` output items ↔ `tool_calls` (same pattern as existing Anthropic translation)
- **Usage translation:** `prompt_tokens`/`completion_tokens` → `input_tokens`/`output_tokens`
- `GET /api/config/responses-api` — read-only status endpoint for the UI
- Frontend: update `CliTool`'s Codex card chip from "Needs Responses API" (amber/warn) to "Supported" (green) and replace the warning note with working `config.toml`
- Frontend: update `Integrations.svelte` Codex section with accurate working copy
- Auth filter: `/v1/responses` added to the `ApiKeyEndpointFilter` routes (same as `/v1/messages` and `/v1/chat/completions`)
- Tests: `ResponsesApiTranslatorTests.cs` (unit), `ResponsesApiEndpointTests.cs` (integration-lite)

### Out of Scope (future stories)

- Server-side statefulness (`store: true`, `previous_response_id` linking): MiniRouter is stateless; these fields are accepted and silently ignored
- Built-in Responses API tools (`web_search`, `file_search`, `code_interpreter`, `computer_use`): not relayed; if Codex sends them they will pass through to the upstream and fail naturally
- WebSocket streaming mode (Responses API supports WS in addition to SSE): not implemented; HTTP SSE only
- `GET /v1/responses/{id}` and `DELETE /v1/responses/{id}` — stateful retrieval endpoints; out of scope

---

## User Story

As a developer using Codex CLI (v0.122+),  
I want to add MiniRouter as a custom model provider in `~/.codex/config.toml` with `wire_api = "responses"`,  
So that all Codex LLM calls are routed through MiniRouter's load-balancing, circuit-breaking, and cost-logging infrastructure.

---

## Acceptance Criteria

1. `POST /v1/responses` accepts the Responses API request body (`model`, `input`, `instructions`, `stream`, `max_output_tokens`, `temperature`, `tools`, `tool_choice`, `previous_response_id`).
2. A string `input` is treated as a single user message.
3. An `input` array of `InputItem` objects (with `type: "message"`, `role`, `content`) is correctly translated to Chat Completions `messages[]`.
4. An `instructions` field is prepended as a `system` role message.
5. Non-streaming response returns `{ id, object: "response", status: "completed", output: [{ type: "message", role: "assistant", content: [{ type: "output_text", text: "..." }] }], usage: { input_tokens, output_tokens } }`.
6. Streaming response emits the correct SSE event sequence terminating with `response.completed`.
7. Tool call outputs in `input` array (`type: "function_call_output"`) are translated to `tool` role messages in Chat Completions.
8. Function call responses from the model are translated back to `output[]` items with `type: "function_call"`.
9. `previous_response_id` and `store` fields are accepted without error and silently ignored.
10. The endpoint is protected by `ApiKeyEndpointFilter` (same rules as `/v1/chat/completions`).
11. Codex CLI `config.toml` with `wire_api = "responses"` and `base_url = "http://localhost:8080/v1"` works end-to-end (manual smoke test: `codex exec "say hello"`).
12. `CliTool.svelte` Codex card shows green "Supported" chip and working `config.toml` copy.
13. `Integrations.svelte` Codex section shows working setup commands.
14. All new and existing tests pass; `dotnet build` clean; `npm run build` clean.

---

## Technical Approach

The implementation mirrors the existing `HandleAnthropicMessagesAsync` pattern in `Services/ProxyService.cs`:

```
Codex CLI
  POST /v1/responses
    → ResponsesRequestTranslator.ToOpenAI(reqNode)   [new: Services/Translation/]
    → ProxyService.ExecuteCompletionAsync(...)        [existing]
    → ResponsesResponseTranslator.ToResponses(resp)  [new: Services/Translation/]
    → or ResponsesStreamTranslator.Translate(chunk)  [new: Services/Translation/]
  ← SSE stream or JSON response in Responses API shape
```

New files parallel the existing Anthropic translation triple:
- `Services/Translation/ResponsesRequestTranslator.cs`
- `Services/Translation/ResponsesResponseTranslator.cs`
- `Services/Translation/ResponsesStreamTranslator.cs`

All translators use `JsonNode`-based manipulation (no typed DTOs for wire shapes) — consistent with the existing Anthropic translators and AOT-safe.

New AOT-registered types for the response envelope (non-streaming) and streaming state will be added to `AppJsonContext`.

---

## Story Breakdown

This epic is a single story — the translation layer is self-contained and testable as a unit.

| Story | Title | Complexity | Dependencies |
|-------|-------|------------|--------------|
| **11.1** | Responses API Translation Layer | Medium-High | Story 10.1 (auth filter, `/v1/models`) |

---

## Definition of Done (Epic)

- [ ] `POST /v1/responses` live and routing traffic through MiniRouter
- [ ] Both streaming and non-streaming paths tested
- [ ] Codex CLI smoke test passes
- [ ] UI updated — no more amber warning on Codex card
- [ ] All 102+ tests pass
- [ ] `dotnet build` and `npm run build` clean

---

*Epic created by @pm (Morgan) · 2026-08-31*
