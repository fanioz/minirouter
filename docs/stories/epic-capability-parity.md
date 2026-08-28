# Epic 8: Capability Parity with 9Router Reference

## Objective

Close the subset of the 9Router capability gap that unlocks real client compatibility and
routing resilience, **without** adopting the reference's surface area or sacrificing
MiniRouter's Native AOT value proposition (~35 MB binary, ~50 MB idle RSS, single
self-contained artifact).

Source analysis: [`docs/reports/9router-gap-analysis.md`](../reports/9router-gap-analysis.md).

## Scope

Five capability items, drawn from Tier 2 of the gap analysis:

1. Error classification, exponential backoff, and `Retry-After` honoring
2. Per-provider capability flags (closes the streaming `include_usage` known issue)
3. Anthropic `/v1/messages` inbound → OpenAI outbound translation
4. Cost calculation from a static pricing table
5. Circuit breaker and routing test coverage (distributed across stories, not standalone)

**Cut (2026-08-11):** the provider → connection split and `providers.json` → SQLite migration
(Story 8.5). No current deployment needs pooled keys, per-key rate-limit rotation, or
per-credential quotas, so 32h of pure enablement was declined. Revisit if multiple keys per
upstream become an actual requirement.

### Explicitly Out of Scope

Per gap analysis section 10, Tier 3. These are separate products and are **not** deferred —
they are declined:

MITM proxy, Cloudflare/Tailscale tunnels, proxy pools, pxpipe, headroom, caveman/ponytail
prompt injectors, MCP bridge, skills packages, media providers (TTS/STT/image/video/search),
CLI-tools settings injection, self-updater, translator playground, i18n.

Also explicitly not pursued: **provider registry parity**. 119 declarative provider files is
correct for Node with no binary-size constraint. For Native AOT every registry entry is
compiled weight and grows the source-generated JSON context. MiniRouter's generic-provider
design is the right trade for its constraints.

## Baseline Correction

Epic `epic-technical-debt.md` marks Stories 1.1 and 1.3 as complete. Verification against
the code shows neither landed:

- **Story 1.1** (body buffering) — all four tasks ticked `[x]`, but
  `Services/ProxyService.cs:41` still calls `new StreamReader(ctx.Request.Body).ReadToEndAsync(ct)`.
  The story's own Status field reads "Ready for Review", contradicting the epic checkbox.
- **Story 1.3** (file persistence) — three non-atomic `File.WriteAllTextAsync` calls remain
  at `Services/ProviderService.cs:176,211,242`.

Story 8.2 in this epic closes 1.3. Story 1.1 is **not** absorbed here — it is a memory NFR
item, not a capability item, and belongs to the technical-debt epic. It should be reopened
there.

## Sequencing Rationale

The gap report listed items by priority. This epic sequences them by **dependency**, which
produces a different order:

- Everything is gated on the test project compiling (defect 2 in the gap report). There is
  currently no way to verify a routing change has not broken routing.
- Backoff state shares `CircuitState` (`Services/ProviderService.cs:45`) with the circuit
  breaker. Story 8.3 lands the backoff level on that key, and with Story 8.5 cut the key
  stays `{providerId}::{modelName}` permanently.
- Anthropic translation has no dependents, so despite being highest-leverage it sequences
  last.

Resulting order: **unblock → resilience → protocol**.

## Success Criteria

- Idle RSS remains < 50 MB; peak < 150 MB under the existing `load-test.sh` profile
- Zero new Native AOT trimming warnings; all new types registered in `AppJsonContext`
- `dotnet test Tests/MiniRouter.Tests.csproj` compiles and passes
- Circuit breaker has test coverage for trip threshold, cooldown, backoff, and recovery
- A `Retry-After` or `resets_at` response defers the provider for the stated duration
- Anthropic-format clients can complete both streaming and non-streaming requests
- Provider config survives a process kill mid-write without truncation

## Timeline

Wave 2 removed after Story 8.5 was cut (2026-08-11).

| Wave | Focus | Stories | Est |
|---|---|---|---|
| 0 | Unblock | 8.1, 8.2 | 6h |
| 1 | Resilience | 8.3, 8.4 | 24h |
| 2 | Protocol + cost | 8.6, 8.7 | 48h |
| | **Total** | | **78h (~2 weeks)** |

## Stories

- [x] [Story 8.1: Restore Test Project Compilation](8.1-restore-test-compilation.story.md) (Wave 0, 3h)
- [x] [Story 8.2: Atomic Provider Config Writes](8.2-atomic-provider-writes.story.md) (Wave 0, 3h)
- [x] [Story 8.3: Error Classification and Exponential Backoff](8.3-error-classification-backoff.story.md) (Wave 1, 16h)
- [x] [Story 8.4: Per-Provider Capability Flags](8.4-provider-capability-flags.story.md) (Wave 1, 8h)
- [x] ~~[Story 8.5: Provider to Connection Split](8.5-provider-connection-split.story.md)~~ — **CUT** (2026-08-11, no multi-key need confirmed)
- [x] [Story 8.6: Anthropic Messages Protocol Translation](8.6-anthropic-messages-translation.story.md) (Wave 2, 40h — spike first)
- [x] [Story 8.7: Cost Calculation from Pricing Table](8.7-cost-calculation.story.md) (Wave 2, 8h)

## Open Decisions

Two items require a call before or during execution. Recorded here rather than buried in
stories because each changes the plan's shape.

### 1. ~~Is Wave 2 (Story 8.5) actually required?~~ — RESOLVED

**Decision (2026-08-11): cut.** Confirmed there is no current need for pooled keys, per-key
rate-limit rotation, or per-credential quotas. Story 8.5 is deferred indefinitely; Story 8.3's
circuit-state key (`{providerId}::{modelName}`) is final.

### 2. Story 8.6's 40h estimate is the least reliable number in this epic

It was sized from reference LOC, which is a weak proxy. Streaming event-model translation is
where this class of work overruns — Anthropic's `message_start` / `content_block_delta` /
`message_delta` model does not map 1:1 onto OpenAI chunks.

Recommend a 1-day spike on the streaming path alone before committing the full wave.

### 3. AOT cost is unestimated across all stories

Every new type needs `AppJsonContext` registration (`Models/Provider.cs:63-88`), and the
Anthropic message schema is substantially larger than anything currently registered. No
measurement exists of the binary-size or RSS impact, yet the success criteria include
idle < 50 MB.

Recommend a measurement checkpoint after Story 8.6 rather than discovering the cost at the end.

## Prerequisite Not In This Epic

The gap analysis rates the unauthenticated admin surface as **Critical** (section 9, defect 1):
`/api/providers`, `/api/keys`, `/api/logs`, and `POST /_shutdown` are all open, on an
all-interfaces bind (`Dockerfile:26`). `PUT /api/providers/{id}` can repoint `baseUrl` to an
attacker-controlled host, turning the router into a prompt-harvesting relay.

This is a security liability, not a capability gap, so it is out of scope here — but it should
be scheduled ahead of or alongside Wave 0.
