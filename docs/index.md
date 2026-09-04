# MiniRouter Documentation Index

> Auto-generated documentation index. Top-level categories; each category has its own `index.md` for deeper navigation. **Canonical architectural reference:** [`architecture.md`](./architecture.md). **Live design system:** [`frontend/design/`](./frontend/design/README.md).

## Root Documents

### [Architecture — System (Canonical)](./architecture.md)

Current state of the MiniRouter backend (C# + SQLite + Svelte SPA) with AOT constraints, request flow, routing + circuit breaker, Anthropic↔OpenAI translation, persistence, presets, CLI, and known constraints. **Start here.**

### [Architecture — System (Brownfield Snapshot)](./archive/system-architecture.md)

Historical 2026-08-06 brownfield pass — kept as a reference for what was here before the API keys / presets / Anthropic translation work.

### [Architecture — Dark Mode Epic](./archive/architecture-dark-mode.md)

Technical architecture for the dark-mode UI overhaul (theme tokens, Tailwind configuration, `mode-watcher` integration).

### [Architecture — CLI Restart Server](./archive/architecture-restart-server.md)

Architecture for the `restart` CLI command: locating the running server, sending `POST /_shutdown`, spawning a fresh process.

### [Brownfield Architecture](./archive/brownfield-architecture.md)

Earlier pre-preset / pre-API-key snapshot of the system.

### [Epic 1 Retrospective](./epic-retrospective.md)

Retrospective on Epic 1: Provider Management Dashboard.

### [Epic 5 Retrospective](./epic-5-retrospective.md)

Retrospective on Epic 5: Dark Mode Support.

### [Front-End Specification](./archive/specs/front-end-spec.md)

UI/UX specification for the original Provider Management dashboard.

### [PO Validation Report — Dark Mode](./po-validation-report.md)

Product Owner validation report for the dark-mode epic.

### [PRD — Brownfield Enhancement](./archive/prd-brownfield/prd.md)

Product Requirements Document for the original brownfield enhancement (provider dashboard + management UI).

### [PRD — Dark Mode](./archive/prd-features/prd-dark-mode.md)

PRD for the dark-mode epic.

### [PRD — Models Navigation](./archive/prd-features/prd-models-nav.md)

PRD for the models navigation tab in the dashboard.

### [PRD — Restart Server](./archive/prd-features/prd-restart-server.md)

PRD for the CLI `restart` command.

### [PRD — Combined Home and About](./archive/prd-features/prd-combined-home-about.md)

Combined PRD for the home/about dashboard pages.

### [Epic 11 — OpenAI Responses API Adapter](./prd/epic-11-responses-api-adapter.md)

PRD for Codex CLI support via OpenAI Responses API translation layer.

### [Epic 12 — Apply Configuration Button](./prd/epic-12-apply-configuration-button.md)

PRD for one-click CLI tool setup with secure config file writes to Claude Code and Codex CLI user-level configs.

### [Epic 13 — Model Chains](./prd/epic-13-model-chains.md)

PRD for named fallback sequences: define `tier1 → [opus, kimi, glm]` once; callers use a single stable model name.

### [UX Spec — Terminal Restart Server](./archive/specs/ux-terminal-restart-server.md)

Terminal UX specification for the restart server CLI.

## Architecture

Documents within the `architecture/` directory — see [`architecture/index.md (archived scaffold)`](./archive/architecture-scaffold/index.md) for the per-document breakdown. Includes system/component/API design docs plus the Epic 5/8/9 architecture writeups.

## Frontend

Frontend specifications, accessibility/responsiveness requirements, and the **MiniRouter Neutral Modern** design system. See [`frontend/index.md (archived)`](./archive/frontend-spec/index.md) for the per-document breakdown.

The design system itself (tokens, previews, applied kit) lives at [`frontend/design/`](./frontend/design/README.md) — single coral accent, warm paper + ink palette, Inter-only type.

## PRD

Product Requirements Documents. See [`prd/index.md (archived brownfield generation)`](./archive/prd-brownfield/index.md) for the per-document breakdown.

## Stories

Development stories grouped by epic. Per the index convention, this folder is a top-level section — browse its contents directly under `docs/stories/`.

Active and recent epics:

- **Epic 1** — Provider Management Dashboard (stories 1.1–1.5)
- **Epic 2** — Razor + CLI provider management (stories 2.1–2.4)
- **Epic 4** — Playground chatbox (story 4.1); model selector provider context (story 4.2)
- **Epic 5** — Dark mode
- **Epic 7** — Comma-separated fallback (story 7.1)
- **Epic 8** — Technical debt round 1 (stories 8.1–8.7) — atomic writes, error classification, capability flags, Anthropic translation, pricing
- **Epic 9** — Preset providers (story 9.1)
- **Epic 10** — Claude Code and Codex CLI support (story 10.1) — x-api-key header, AUTH_PASSTHROUGH, /v1/models endpoint
- **Epic 11** — OpenAI Responses API Adapter (story 11.1) — Codex CLI wire_api="responses" protocol translation
- **Epic 12** — Apply Configuration Button (story 12.1) — one-click CLI tool setup with secure config file writes
- **Epic 13** — Model Chains (story 13.1) — named fallback sequences: `tier1` → `[anthropic/opus, openrouter/kimi, openrouter/glm]`
- Plus retrospective `epic-5-dark-mode.story.md`, `epic-capability-parity.md`, `epic-technical-debt.md`, and historical `story-1.1`–`story-1.4` writeups.

## Reports

Technical reports (gap analyses, debt summaries, framework comparisons) under `docs/reports/`:

- `9router-gap-analysis.md`
- `TECHNICAL-DEBT-REPORT.md`
- `razor-cli-analysis.md`

## Reviews

QA / specialist reviews for brownfield discovery and quality gates under `docs/reviews/`:

- `db-specialist-review.md`
- `qa-final-review.md`, `qa-review.md`, `qa-review-phase-2.md`, `qa-review-phase-3.md`
- `ux-specialist-review.md`

## Reference

External reference implementations consulted during development, under `docs/reff/`. **Not project documentation** — provided for traceability only. Notable: `open-sse/` (an OmniRoute-style multi-provider router that informed the preset catalog, error classification, and circuit-breaker patterns).
