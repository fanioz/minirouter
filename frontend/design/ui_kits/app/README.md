# ui_kits/app — MiniRouter Applied Kit

## Overview

Applied interface kit that proves the **MiniRouter Neutral Modern** system on a real product surface. It is not a generic static mock — it is the Analytics v2 dashboard (`minirouter-analytics-v2.html`) rebuilt to consume `../../colors_and_type.css` as the single token source.

**Source basis:** `minirouter-analytics-v2.html` (64 KB, Admin Analytics v2, high-fidelity prototype for product evaluators) + `context/source-context.md`. Layout, copy, and sample data are preserved; CSS is rewritten to reference the design-system tokens only. Based on the original source, the kit captures colors, typography, and layout tokens verbatim.

## Structure

```
ui_kits/app/
  index.html       ← full Analytics surface (shell + topbar + KPIs + Traffic & Latency chart
                     + Cost by provider + Provider health + Provider performance table
                     + Top models + Recent requests + drawer + toast). Self-contained, loads
                     ../../colors_and_type.css. Open this file to see the system in context.
  components.html  ← isolated component atlas: buttons (ghost/primary/tabs/seg), badges,
                     inputs/select/switch, KPI card, cost row, health cards, table row,
                     chart primitive, drawer+toast. Each section is labeled with its canonical
                     class (.btn, .card, .badge, etc.). Copy any block into a new page.
  components/Sidebar.jsx  ← reference component mapping for audit (App shell)
  components/PreviewCard.jsx ← reference component mapping
  components/ChatArea.jsx  ← reference component mapping
  README.md        ← this file
```

Both HTML files reference `../../colors_and_type.css` via `<link>` — no inline token redefinitions. Components live under `components/` and compose the App shell. Dark mode works identically to the source: `document.documentElement.classList.toggle('dark', isDark)`.

## Components

- **index.html** — Exercises the shell (`268px` sidebar, `64px` topbar, `1360px` content), `kpis` 4→2 grid, `grid2` `1.62fr .92fr`, `grid3` `1.32fr .88fr`, sticky `thead #FCFCF9`, chart `720×200` with `areaGrad .24→0`, provider rows with dot+progress+badges, and the comparison banner. Maps to `App` and `Sidebar` concepts and renders `PreviewCard` atoms in the KPI grid.
- **components.html** — 6 sections: Buttons, Badges & Pills, Forms, Cards (KPI/Cost/Health), Chart primitive, Shell note. Each shows the minimal markup needed to reproduce the component without JavaScript (except nav/toast demos). Includes `ChatArea`, `MessageBubble`, and `InputBar` patterns adapted for log streams.

## Usage

1. **Start from tokens.** Ensure `colors_and_type.css` is the first `<style>`/`<link>` in your new file. Use it to build new App surfaces.
2. **Copy shell or atom.** For a new dashboard, copy the entire `.shell` block from `index.html`; for a single feature, copy a card/row from `components.html`. You can also import `components/Sidebar.jsx` or `components/PreviewCard.jsx` as a starting point.
3. **Keep the class names.** `.btn`, `.card`, `.kpi`, `.cost-row`, `.health-card`, `.badge`, `.progress`, `.input` are the API. Custom selectors should be avoided. Compose and create new views by combining these atoms.
4. **Theme.** `html.dark` remap lives in `colors_and_type.css` — no extra stylesheet needed. Wire the switch in `index.html` if you need live toggle.
5. **Verify.** After changing a token, reload `index.html` and any `preview/*.html` card — they all share the same source. Use this workflow to quickly review and reuse the kit.

## Design Notes

- **Layout:** 14px gutters, 62ch descriptions, border-over-shadow separation, asymmetric grids (1.62/.92) for chart dominance.
- **Colors:** Single coral `#E56A4A` on warm paper `#FDFAF6` with ink `#111111`; success/warn/danger pairs are the only other accents. Based on source tokens.
- **Typography:** Inter-only with JetBrains Mono for tokens; tabular numerals prevent jitter.
- **Tokens:** All spacing, radius, and shadow values are verbatim from source — colors and typography foundations live in `colors_and_type.css`.

## Source

All hex, spacing, radius, shadow, and motion values are verbatim from `minirouter-analytics-v2.html :root`. Provider pins (`#E56A4A`/`#111111`/`#6B7280`) and sample data (18,429 req, 98.4% success, $42.18) are preserved as evidence. Icons are inline SVG strokes (1.7–1.8) — no font or icon-library dependency. Source is `minirouter-analytics-v2.html` and is based on the linked local folder evidence.

See `../../DESIGN.md §5–§6` for normative layout/component contracts and `../../context/provenance.md` for extraction lineage.
