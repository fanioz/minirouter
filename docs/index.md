# MiniRouter Documentation Index

> Auto-generated documentation index. Top-level categories; each category has its own `index.md` for deeper navigation. **Canonical architectural reference:** [`architecture.md`](./architecture.md). **Live design system:** [`frontend/design/`](./frontend/design/README.md).

## Root Documents

### [Architecture — System (Canonical)](./architecture.md)

Current state of the MiniRouter backend (C# + SQLite + Svelte SPA) with AOT constraints, request flow, routing + circuit breaker, Anthropic↔OpenAI translation, persistence, presets, CLI, and known constraints. **Start here.**

### [Architecture — System (Brownfield Snapshot)](./architecture/system-architecture.md)

Historical 2026-08-06 brownfield pass — kept as a reference for what was here before the API keys / presets / Anthropic translation work.

### [Architecture — Dark Mode Epic](./architecture-dark-mode.md)

Technical architecture for the dark-mode UI overhaul (theme tokens, Tailwind configuration, `mode-watcher` integration).

### [Architecture — CLI Restart Server](./architecture-restart-server.md)

Architecture for the `restart` CLI command: locating the running server, sending `POST /_shutdown`, spawning a fresh process.

### [Brownfield Architecture](./brownfield-architecture.md)

Earlier pre-preset / pre-API-key snapshot of the system.

### [Epic 1 Retrospective](./epic-retrospective.md)

Retrospective on Epic 1: Provider Management Dashboard.

### [Epic 5 Retrospective](./epic-5-retrospective.md)

Retrospective on Epic 5: Dark Mode Support.

### [Front-End Specification](./front-end-spec.md)

UI/UX specification for the original Provider Management dashboard.

### [PO Validation Report — Dark Mode](./po-validation-report.md)

Product Owner validation report for the dark-mode epic.

### [PRD — Brownfield Enhancement](./prd.md)

Product Requirements Document for the original brownfield enhancement (provider dashboard + management UI).

### [PRD — Dark Mode](./prd-dark-mode.md)

PRD for the dark-mode epic.

### [PRD — Models Navigation](./prd-models-nav.md)

PRD for the models navigation tab in the dashboard.

### [PRD — Restart Server](./prd-restart-server.md)

PRD for the CLI `restart` command.

### [PRD — Combined Home and About](./prd-combined-home-about.md)

Combined PRD for the home/about dashboard pages.

### [UX Spec — Terminal Restart Server](./ux-terminal-restart-server.md)

Terminal UX specification for the restart server CLI.

## Architecture

Documents within the `architecture/` directory — see [`architecture/index.md`](./architecture/index.md) for the per-document breakdown. Includes system/component/API design docs plus the Epic 5/8/9 architecture writeups.

## Frontend

Frontend specifications, accessibility/responsiveness requirements, and the **MiniRouter Neutral Modern** design system. See [`frontend/index.md`](./frontend/index.md) for the per-document breakdown.

The design system itself (tokens, previews, applied kit) lives at [`frontend/design/`](./frontend/design/README.md) — single coral accent, warm paper + ink palette, Inter-only type.

## PRD

Product Requirements Documents. See [`prd/index.md`](./prd/index.md) for the per-document breakdown.

## Stories

Development stories grouped by epic. Per the index convention, this folder is a top-level section — browse its contents directly under `docs/stories/`.

Active and recent epics:

- **Epic 1** — Provider Management Dashboard (stories 1.1–1.5)
- **Epic 2** — Razor + CLI provider management (stories 2.1–2.4)
- **Epic 4** — Playground chatbox (story 4.1)
- **Epic 5** — Dark mode
- **Epic 7** — Comma-separated fallback (story 7.1)
- **Epic 8** — Technical debt round 1 (stories 8.1–8.7) — atomic writes, error classification, capability flags, Anthropic translation, pricing
- **Epic 9** — Preset providers (story 9.1)
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
