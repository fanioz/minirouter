# MiniRouter Documentation Index

> Entry point for everything under `docs/`. The tree splits into **living documentation** (current architecture, current PRDs, stories, agent guidance, QA — what is true now) and **point-in-time layers** (`retrospectives/`, `reports/`, `reviews/`, `archive/` — kept for traceability, not maintained). **Canonical system architecture:** [`architecture.md`](./architecture.md). **Visual source of truth:** [`frontend/design/README.md`](./frontend/design/README.md).

## Start here

- **[Architecture](./architecture.md)** — canonical architectural reference: current state of the MiniRouter backend (C# + SQLite + Svelte SPA) with AOT constraints, request flow, routing + circuit breaker, Anthropic↔OpenAI translation, persistence, presets, CLI, and known constraints.
- **[Design system](./frontend/design/README.md)** — the **MiniRouter Neutral Modern** design system: tokens, previews, UI kit, and the design-agent workbench under `frontend/design/`.
- **`stories/`** — development work is driven by stories in `docs/stories/` (see the root [`AGENTS.md`](../AGENTS.md)). Current epics: 10 (Claude Code / Codex CLI), 11 (Responses API adapter), 12 (Apply Configuration Button), 13 (Model Chains). **Legacy:** the original Razor Pages UI stories — [`2.1-scaffold-razor-pages`](./stories/2.1-scaffold-razor-pages.story.md) and [`2.3-implement-razor-provider-forms`](./stories/2.3-implement-razor-provider-forms.story.md) — are superseded by the Svelte SPA (epic 1.x); the `Pages/` tree is deleted, and these stories are kept for history only.
- **Agent operating docs** — `docs/agents/` for AI agents: [`domain.md`](./agents/domain.md) (repo map, conventions, ADR policy), [`issue-tracker.md`](./agents/issue-tracker.md) (GitHub issue conventions), [`triage-labels.md`](./agents/triage-labels.md) (triage label roles).
- **[QA](./qa/qa-report-project-review-2026-08-29.md)** — full project review report (2026-08-29) plus quality gates under `qa/gates/` (live gate: [`gates/13.1-model-chains.yml`](./qa/gates/13.1-model-chains.yml)).

## Architecture

Current write-ups in `architecture/`:

- **[component-architecture.md](./architecture/component-architecture.md)** — maps the actual Svelte 5 + Tailwind 4 dashboard components onto the backend service layer (current structure, not proposed).
- **[epic-5-dark-mode.md](./architecture/epic-5-dark-mode.md)** — dark-mode epic architecture: theme tokens, Tailwind configuration, `mode-watcher` integration.
- **[epic-8-technical-debt.md](./architecture/epic-8-technical-debt.md)** — the **resulting architecture** of technical-debt round 1 (test compilation, atomic writes, error classification, capability flags, pricing).
- **[epic-9-preset-providers.md](./architecture/epic-9-preset-providers.md)** — preset provider catalog: `Services/Presets/` with one-click enable endpoints.
- **[api-key-management-architecture.md](./architecture/api-key-management-architecture.md)** — API key management: key CRUD, remote-management authentication, loopback exemption.

## PRDs (current)

Product requirements documents in `prd/`:

- **[epic-11-responses-api-adapter.md](./prd/epic-11-responses-api-adapter.md)** — Codex CLI support via an OpenAI Responses API translation layer.
- **[epic-12-apply-configuration-button.md](./prd/epic-12-apply-configuration-button.md)** — one-click CLI tool setup with secure config-file writes to Claude Code and Codex CLI user-level configs.
- **[epic-13-model-chains.md](./prd/epic-13-model-chains.md)** — named fallback sequences: define `tier1 → [opus, kimi, glm]` once; callers use a single stable model name.
- **[api-key-management.md](./prd/api-key-management.md)** — administrative interface to generate, view, edit, and revoke API keys; secures the proxy endpoint.
- **[api-key-copy-button.md](./prd/api-key-copy-button.md)** — quick "Copy" action for API key IDs from the API Keys list.
- **[models-tab.md](./prd/models-tab.md)** — surfacing the aggregated `/models` list as a dedicated dashboard tab.
- **[technical-debt-assessment.md](./prd/technical-debt-assessment.md)** — final technical-debt assessment with prioritized NFRs and decisions.

## Reports & reviews

Historical analyst/QA outputs of the Aug-6 brownfield discovery — no longer maintained, but still cross-referenced by stories.

`reports/`:

- **[9router-gap-analysis.md](./reports/9router-gap-analysis.md)** — gap analysis against the vendored 9Router reference.
- **[TECHNICAL-DEBT-REPORT.md](./reports/TECHNICAL-DEBT-REPORT.md)** — original debt report (superseded by [`prd/technical-debt-assessment.md`](./prd/technical-debt-assessment.md)).
- **[razor-cli-analysis.md](./reports/razor-cli-analysis.md)** — Razor + Spectre.Console CLI analysis.

`reviews/` — six Aug-6 discovery reviews: [qa-review.md](./reviews/qa-review.md), [qa-review-phase-2.md](./reviews/qa-review-phase-2.md), [qa-review-phase-3.md](./reviews/qa-review-phase-3.md), [qa-final-review.md](./reviews/qa-final-review.md), [db-specialist-review.md](./reviews/db-specialist-review.md), [ux-specialist-review.md](./reviews/ux-specialist-review.md).

## Retrospectives

Point-in-time records that were never superseded — first-class history, not archive:

- **[epic-retrospective.md](./retrospectives/epic-retrospective.md)** — Epic 1: Provider Management Dashboard.
- **[epic-5-retrospective.md](./retrospectives/epic-5-retrospective.md)** — Epic 5: Dark Mode Support.
- **[po-validation-report.md](./retrospectives/po-validation-report.md)** — Product Owner validation of the dark-mode epic.

## Reference

`reff/` — a vendored full clone of **9Router v0.5.50** (Node/Next.js reference implementation). **Gitignored and NOT project documentation** — kept for traceability only; consulted during the gap analysis and epics 7/8/9/11 (circuit breaker, presets, Responses API).

## Archive

Superseded generations and shipped-feature records under `archive/`. What moved where:

| Location | Contents |
|---|---|
| [`archive/architecture-scaffold/`](./archive/architecture-scaffold/index.md) | Aug-6 brownfield architecture TOC + 12 section stubs (pre-UI, no-auth snapshot) |
| [`archive/prd-brownfield/`](./archive/prd-brownfield/prd.md) | Aug-6 enhancement PRD (incl. former `docs/prd.md`) |
| `archive/prd-features/` | Shipped feature PRDs — former root `prd-*.md` files plus `prd/epic-5-dark-mode.md` |
| [`archive/frontend-spec/`](./archive/frontend-spec/index.md) | Aug-6 frontend spec fragments |
| [`archive/specs/front-end-spec.md`](./archive/specs/front-end-spec.md), [`ux-terminal-restart-server.md`](./archive/specs/ux-terminal-restart-server.md) | Original front-end and terminal-UX specifications |
| [`archive/brownfield-architecture.md`](./archive/brownfield-architecture.md) | Pre-preset / pre-API-key architecture snapshot |
| [`archive/system-architecture.md`](./archive/system-architecture.md) | Aug-6 system snapshot (superseded by [`architecture.md`](./architecture.md)) |
| [`archive/architecture-dark-mode.md`](./archive/architecture-dark-mode.md), [`architecture-restart-server.md`](./archive/architecture-restart-server.md) | Epic architecture write-ups for the shipped dark-mode / CLI-restart epics |
| [`archive/technical-debt-DRAFT.md`](./archive/technical-debt-DRAFT.md) | Draft debt assessment (superseded by [`prd/technical-debt-assessment.md`](./prd/technical-debt-assessment.md)) |
