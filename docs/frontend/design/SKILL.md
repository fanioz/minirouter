---
name: minirouter-neutral-modern
description: Warm paper + coral analytics system for MiniRouter Native AOT edge LLM router — single coral accent, Inter-only type, pill controls, ink navigation, and high-density data components (KPI, chart, cost bars, health, table, logs). Use for router/observability dashboards.
user-invocable: true
---

# MiniRouter — Neutral Modern — Agent Skill

> **Use this skill when generating UIs that should look like MiniRouter (warm paper + coral, high-density analytics).** It is the agent-facing contract for this design-system package. Read it before writing any HTML/CSS.

## What is inside

- `colors_and_type.css` — single source of truth: `:root` tokens (bg/surface/fg/muted/meta/border/accent/success/warn/danger), `html.dark` remap, radius (`8/12/16/9999`), shadows, Inter + JetBrains Mono stacks, spacing `--space-*`, motion and focus-ring. Paste verbatim.
- `DESIGN.md` — normative spec across 9 sections: theme, color (hex+OKLCH), type scale, spacing, layout shell (268+64+1360, 1.62/.92 grids), all components, motion, voice, anti-patterns.
- `preview/*.html` (9 cards) — isolated visual proof on the live tokens; each loads `../colors_and_type.css`.
- `assets/` — wordmark SVG, favicon SVG, provider dot swatches derived verbatim from source primitives (no font binaries in source).
- `ui_kits/app/` — applied kit: `index.html` (full Analytics v2 on live tokens) + `components.html` (isolated atlas).
- `context/source-context.md` + `context/provenance.md` + preserved `minirouter-analytics-v2.html` (64 KB) — source evidence outside `context/`.

## Source context

- **Source project:** `6a3e0d06-47c7-41fe-848c-3ce0ad840746` "Web Prototype" (examplePrompt: Dashboard, high-fidelity, product evaluators). Linked local dir `/Users/fanioz/code/dotnet/minirouter` (Native AOT ~34 MB, `PORT=8080`, `UseStaticFiles()` Svelte SPA hint in footer).
- **This workspace:** `111d9616-ed1b-4313-ae9a-35df1d2ec2bc` (design system `user:web-prototype-design-system-2`, title Neutral Modern).
- **Evidence:** Single file `minirouter-analytics-v2.html` (64 KB) — Admin Analytics v2 prototype described by its own banner as "same coral + warm-paper palette, but higher density: KPIs with trends, time-series with latency overlay, provider health strip, sortable performance table, and model leaderboard — all filterable by provider & time range." No external logos, avatars, or font binaries were present; icons are inline SVG strokes (1.7–1.8) and the brand mark is `◈` in an ink square.
- **Extraction:** All hex/radius/shadow/motion values verbatim from `:root`; OKLCH annotations added without value change. Dark remap preserved intact. See `context/provenance.md`.

## When to use

- Building prototypes, mockups, interfaces, or production artifacts that should carry the MiniRouter instrument character — provider analytics dashboards, latency/cost observability panels, routing playgrounds, log viewers, or key management UIs.
- Re-theming or extending the dashboard (new providers, new models, comparison modes) while keeping warm paper + one coral signal.
- Generating high-density data surfaces where border-over-shadow, pill controls, and tabular numerals matter. Useful for both rapid prototypes and production design handoffs.

Do **not** use for marketing pages or illustration-heavy surfaces — this system is an ops instrument, not a brand campaign.

## How to use

1. **Paste `colors_and_type.css` verbatim as the first `<style>`** (or `<link>` it). It already contains `html.dark`. Theme toggle is `document.documentElement.classList.toggle('dark', isDark)` + label update — see `ui_kits/app/index.html`.
2. **Read `DESIGN.md` §2–§4** before writing CSS: 1 accent only (`#E56A4A` → `#EE8D6A` dark), `12px` card radius, `9999px` pills (except `.input` 10px), Inter (display=body) + JetBrains Mono, tabular numerals, `14px` inter-card gap, `1360px` content max, sticky `268px` sidebar + `64px` topbar.
3. **Copy markup from `ui_kits/app/`** — never invent new button/card classes. Canonical classes: `.btn`, `.btn-primary`, `.tab`, `.seg`, `.card`, `.kpi`, `.badge`, `.pill`, `.input`, `.select`, `.switch`, `.progress`, `.copy`, `.drawer`, `.toast`.
4. **Verify via previews.** Each `preview/*.html` loads the live CSS — change a token in `colors_and_type.css` and the cards reflect it instantly.

## Design-system highlights

- **Colors:** Single coral accent (`#E56A4A`), warm paper `#FDFAF6`, ink `#111111`, muted tiers, success/warn/danger pairs.
- **Typography:** Inter-only display+body with tight tracking, JetBrains Mono for tokens/numbers, tabular-nums on all numerics.
- **Spacing:** 4-pt scale, 14px inter-card gaps, 268px sidebar + 64px topbar + 1360px content shell, responsive stacking (1120/860/640).
- **Radius:** 8/12/16/9999 (pill controls dominate, cards 12px, inputs 10px exception).
- **Shadows:** Faint card shadow + raised shadow for drawer/toast, removed in dark mode.
- **Icons:** Inline SVG strokes 1.7–1.8, no icon font, layout-driven interaction.
- **Layout:** Asymmetric grids (1.62/.92, 1.32/.88) creating chart dominance, sticky chrome with backdrop blur.
- **Interaction:** Focus ring `0 0 0 3px var(--accent-ring)`, hover border shift, pulse on live dot with reduced-motion support.

## Hard rules (must enforce)

- One coral per screen, pill controls, ink actives, border+shadow cards, focus ring, tabular numerals, qualified costs — per above. Violating any breaks the instrument character validated in v2.

## Layout recipe

```
.shell (flex row)
  .sidebar (268px sticky 100vh — brand 64h + nav groups + foot pill+switch)
  .main (flex:1 min-width:0 column)
    .topbar (sticky 64h, rgba(253,250,246,.88) + blur/saturate, breadcrumb + comparison pill + controls)
    .content (max 1360px, 24/20/32 pad)
      .page-title (h1 28/700 -0.03 + 62ch p + actions badges)
      .compare-banner (optional warm info strip)
      .kpis (grid 4 → 2 @1120)
      .grid2 (1.62fr .92fr → 1fr)
      .panel / provider table (sticky thead #FCFCF9)
      .grid3 (1.32fr .88fr → 1fr)
```

Breakpoints are structural, not padding-only: at `1120px` grids stack, at `860px` sidebar becomes horizontal strip and topbar wraps, at `640px` search hides. Never allow horizontal scroll; clip long identifiers with `ellipsis`, don't force `nowrap`.

## Component inventory (copy from `ui_kits/app/components.html`)

- **Buttons:** ghost pill → primary coral → tabs → segmented → icon-btn.
- **KPI:** label caps + value 26px + `up/down` pill (`#A7F3D0/#FECACA` border) + mini viz (sparkline stroke accent 2px, bars, or progress).
- **Cost rows:** provider name (110px ellipsis) + `bar-track 8h #F3F4F6` + fill in provider color + cost tabular.
- **Health cards:** 2-col grid, `10px` dot + name + circuit badge.
- **Provider table:** sticky caps header, dot+id, mono baseUrl, requests with progress, tokens mono, latency good/mid/bad, success badge, circuit badge, cost mono, toggle+copy actions.
- **Model leaderboard:** rank (ink vs alt) + mono id + meta + tokens + copy.
- **Log list:** grid `84px 1fr auto`, mono time, bold provider + mono model, `ok/fail` pill, truncated msg, latency mono.
- **Chart:** `720×200` SVG, `gridline #F0EDE9`, `areaGrad .24→0`, `line 2.2 coral`, `line2 dashed .5`, dots every 3rd point.
- **Drawer + Toast:** `Drawer` (440/92vw, `shadow-raised`, `blur(2px)` bg) + `Toast` (ink pill, `2200ms` auto-hide).

## Voice & copy

- Precise, operator-grade, caveated on cost. No emoji, no marketing adjectives.
- Canonical terms: `Provider`, `Base URL`, `Model`, `Latency p50 / p95`, `Tokens in / out`, `Success`, `Circuit`, `Est. cost`, `Explicit routing: providerId/model • no retries`.
- Labels are `10.5/750 0.07–0.09 uppercase` tracking; identifiers stay lowercase (`openai-primary`, `gpt-4o-mini`).

## Anti-patterns to avoid

Purple gradients, emoji icons, left-bar actives, gray hover text, serif display swaps, hand-drawn illustrations, multiple primaries per viewport, icon-everywhere headings, beige washes, borderless data cards, `nowrap` title forcing, outline-only charts, fabricated metrics, designer-only control panels.

## File map

- `DESIGN.md` — normative spec. Changes here are visual contract changes.
- `colors_and_type.css` — single source of truth. Edit tokens here; previews and kits will pick them up.
- `preview/*.html` — isolated visual proof. Each loads the real CSS; use them to verify a token change.
- `ui_kits/app/index.html` — full applied surface to clone.
- `assets/` — wordmark, favicon, provider dots (extracted verbatim).

## Verification before handoff

- [ ] `"$OD_NODE_BIN" "$OD_BIN" tools connectors design-system-package-audit --path . --fail-on-warnings` — 0 errors, 0 warnings.
- [ ] Every primary CTA has one dark/light hover state that does not reduce contrast.
- [ ] All badges and mono numerals render without overflow at `640px`.
- [ ] Chart has both `areaGrad` fill and `line` stroke (not outlines alone).
