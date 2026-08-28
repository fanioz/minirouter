# MiniRouter — Neutral Modern Design System

> High-density observability system for the MiniRouter Native AOT edge LLM router. Warm paper + coral signal palette, Inter-only type, pill controls, and ink-active navigation — built from `minirouter-analytics-v2.html` (Admin Analytics v2).

---

## Product Overview

**Product:** MiniRouter — Native AOT edge router that proxies `chat.completions` across multiple LLM providers (openai-primary, anthropic, groq) with explicit `providerId/model` routing, no retries, and per-request circuit logic. Prototype is a Static Svelte SPA served by `UseStaticFiles()` on `PORT=8080`, idle ~34 MB.

**Product Context:** The dashboard in `minirouter-analytics-v2.html` exists so operators can answer, in seconds: is traffic healthy? which provider is hot? what did we spend? The v2 brief (source `compare-banner` copy) was "same coral + warm-paper palette, but higher density: KPIs with trends, time-series with latency overlay, provider health strip, sortable performance table, and model leaderboard — all filterable by provider & time range."

**Primary surfaces (from source evidence):**
- **Analytics dashboard** — the main surface: KPIs (requests, success, latency, cost), Traffic & Latency time-series (area+line overlay), Cost by provider (bar share), Provider health strip (circuit/healthy), sortable Provider Performance table (requests, tokens, latency p50/p95, success, circuit, cost), Top Models leaderboard, Recent Requests stream, and Provider Detail drawer.
- **Navigation chrome:** Sticky sidebar (Operate: Home/Providers/Presets/Models/Playground; Observe: Analytics/Logs/API Keys) + sticky topbar (breadcrumb, comparison pill "Improved v2", search, provider filter, 24H/7D/30D seg, Refresh, Export CSV).
- **Supporting surfaces referenced in source nav:** Providers (CRUD + circuit toggles), Models, Playground, Logs, API Keys — reflected in the nav structure and available as kit extensions.

**Core capabilities the system encodes:** The product provides filterable observability, includes snapshot cost estimation, supports latency-aware rendering, and enables circuit-aware operability:
- Filterable observability by provider + time range (24H/7D/30D tab + provider select + free-text search, all live).
- Snapshot cost estimation (measured vs estimated tokens, 4-char estimator disclaimer, per-provider share vs token toggle).
- Latency-aware rendering (p50/p95 with good/mid/bad thresholds, mini bar viz, sparkline on KPI).
- Circuit-aware operability (open/closed, 24s cooldown, enabled toggle, live dot pulse).

The workspace application is built for product evaluators and designed to help operators ship reliable routing. The platform offers a high-fidelity prototype that features the full analytics surface.

**Source workspace:** project `6a3e0d06-47c7-41fe-848c-3ce0ad840746` "Web Prototype" (examplePrompt Dashboard, high-fidelity, product evaluators) → this workspace `111d9616-ed1b-4313-ae9a-35df1d2ec2bc`. Linked local dir `/Users/fanioz/code/dotnet/minirouter`. Source evidence lives under `context/` and is preserved as runtime brand assets.

## Source Context

- `context/source-context.md` — generation contract, project ids, copied file list.
- `context/provenance.md` — token/font/asset extraction lineage for this run (what was verbatim, what was normalized, what was absent).
- `minirouter-analytics-v2.html` (64 KB) — **preserved source outside `context/`** per package contract. The only high-signal evidence file; do not delete.

## Package Contents

```
DESIGN.md                 ← normative spec: theme, color (hex+OKLCH), type, spacing, layout, components, motion, voice, anti-patterns
colors_and_type.css       ← :root tokens + html.dark remap + base resets — paste verbatim as first <style>
context/source-context.md ← provenance of the copied project
context/provenance.md     ← extraction notes (this run)
minirouter-analytics-v2.html ← preserved source (64 KB)
assets/
  wordmark.svg            ← 220×48 ink wordmark (◈ + MiniRouter + Native AOT • Edge Router)
  favicon.svg             ← 32×32 ink favicon (◈)
  provider-dots.svg       ← provider pin swatches (coral/ink/stone)
fonts/                    ← absent — source has no font binaries (Inter via system stack, see provenance)
build/                    ← absent — source is single HTML with inline SVG strokes; no build/tray artifacts
preview/                  ← focused review cards (each loads colors_and_type.css directly)
ui_kits/app/
  index.html              ← applied interface kit (full Analytics v2 on the live tokens)
  components.html         ← isolated button/input/badge/card/chart/table atlas
  README.md               ← kit reuse guide
SKILL.md                  ← agent-facing contract (frontmatter + When/How/Highlights)
README.md                 ← this file (package guide + preview manifest)
```

**Fonts:** No `woff2` to preserve — source references Inter + JetBrains Mono via the system stack only (`-apple-system`, `ui-monospace`). `assets/` therefore holds only SVGs derived from primitives.

**Build:** Single-HTML source with inline stroke icons (1.7–1.8) — no runtime build artifacts. `build/` is intentionally empty.

## Reuse Workflow

To reuse this package: start with `colors_and_type.css`, inspect any `preview/` card, copy structure from `ui_kits/app`, reuse `assets/` and `fonts/` where present, and review via `DESIGN.md`. Open, copy, compose, and inspect — the workflow works for both reviewers and future agents building new surfaces.

## Preview Manifest

Open any preview/*.html card directly — each loads `colors_and_type.css` and uses the actual component classes, so a token change reflects instantly.

| Card | Path | What it proves | Suggested first |
|---|---|---|---|
| Colors — Primary | `preview/colors-primary.html` | Warm paper vs surface vs ink, coral single accent, dark toggle, wordmark + provider pins | **1st** |
| Colors — Semantic | `preview/colors-semantic.html` | success/warn/danger solid+soft pairs, text hierarchy, comparison pill | |
| Typography | `preview/typography-specimens.html` | Inter-only scale (28/15/13.5/10.5), caps tracking, body 13.5/1.5, mono tabular-nums | **2nd** |
| Spacing | `preview/spacing-tokens.html` | 4-pt scale, card paddings, 1360/268/64 shell, 14px gutters, responsive reflow | |
| Radius & Shadows | `preview/radius-shadows.html` | 8/12/16/9999, shadow-card vs raised, warm dividers | |
| Buttons & Inputs | `preview/components-buttons.html` | ghost→primary→disabled, tabs, segmented, inputs, switches, focus ring | |
| Data Components | `preview/components-data.html` | KPI, cost bars, health, table extract, leaderboard, logs | |
| Brand Assets | `preview/brand-assets.html` | wordmark, favicon, provider dots, preservation notes | |
| Surfaces — Dashboard | `preview/surfaces-dashboard.html` | Full shell on live tokens (sidebar+topbar+cards+chart) | **3rd** |

**Review in 60 seconds:** `colors-primary.html` → `typography-specimens.html` → `surfaces-dashboard.html`. The rest drill into spacing/radius/buttons/data/brand.

## Quick Start

1. **Tokens first.** Paste `colors_and_type.css` verbatim as the first `<style>` (or `<link>` it). Dark is `html.dark`.
2. **Read the spec.** `DESIGN.md §2–§4` for hex/OKLCH, type, spacing/radius; `§5` for the 268px sidebar + 64px topbar + 1360px shell; `§6` for component markup.
3. **Copy from the kit.** `ui_kits/app/index.html` and `ui_kits/app/components.html` both reference `../../colors_and_type.css` — steal markup directly.
4. **Verify visually.** Change a token in `colors_and_type.css` and reload any `preview/*.html` card or the kit.

## Applied Kit

`ui_kits/app/index.html` is an **applied** MiniRouter Analytics surface — not a generic mock. It reuses the source layout and sample data (18,429 req, 98.4% success, $42.18, 3 providers) but rewrites all CSS to consume `colors_and_type.css` only. Use as a starter template for new router-adjacent dashboards. Atlas is at `ui_kits/app/components.html`.

## Review Workflow

1. Reviewer opens `preview/colors-primary.html`, toggles dark, checks coral on paper legibility.
2. Opens `preview/typography-specimens.html` — confirms Inter tight tracking + mono tabular numerals.
3. Opens `preview/surfaces-dashboard.html` or `ui_kits/app/index.html` — confirms full shell density matches source banner ("same coral + warm-paper palette, but higher density").
4. Agent picks a card row from `ui_kits/app/components.html` and pastes into the new page with no extra CSS.

## Visual Signature

`#FDFAF6` warm paper · `#111111` ink · `#E56A4A` coral (one accent) · `#6B7280/#9CA3AF` muted tiers · `12px` card radius / `9999px` pills · faint `shadow-card` + `1px` strokes · Inter everywhere with `JetBrains Mono` for tokens/numbers · `tabular-nums` on all numerics.

## Provenance

No web-font binaries present in source — Inter renders via system stack identically. Icons are inline SVG strokes (1.7–1.8 weight). Original artifact preserved as `minirouter-analytics-v2.html` (64 KB). See `context/provenance.md`.

## Tokens at a Glance

| Token | Value | Notes |
|---|---|---|
| `--bg` | `#FDFAF6` | Warm paper |
| `--surface` | `#ffffff` | Cards / chrome |
| `--fg` | `#111111` | Ink text |
| `--accent` | `#E56A4A` | Coral primary (dark `#EE8D6A`) |
| `--success` | `#0EA464` | Live / ok |
| `--warn` | `#D97706` | Mid latency |
| `--danger` | `#DC2626` | Bad / open |
| `--border` | `#E5E7EB` | Card strokes |
| `--radius-md` | `12px` | Cards |
| `--radius-pill` | `9999px` | Buttons / pills |
| `--font-body` | `Inter, -apple…` | Display = same |
| `--font-mono` | `JetBrains Mono…` | Tokens / numbers |

## License / Reuse

Tokens and component classes are intended for direct reuse within Open Design projects derived from this workspace. Keep `colors_and_type.css` as the source of truth — do not duplicate hex values elsewhere.
