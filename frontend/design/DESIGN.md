# MiniRouter — Neutral Modern Design System

> **Category:** Project Design System — Operational Analytics / Edge Router
> **Surface:** Responsive web (desktop-first dashboard)
> **Source project:** `6a3e0d06-47c7-41fe-848c-3ce0ad840746` "Web Prototype" → `111d9616-ed1b-4313-ae9a-35df1d2ec2bc`
> **Source evidence:** `minirouter-analytics-v2.html` (64 KB, Admin Analytics v2, high-fidelity prototype for product evaluators)
> **Design system id:** `user:web-prototype-design-system-2` • Title: Neutral Modern
> **Linked local dir:** `/Users/fanioz/code/dotnet/minirouter` (Native AOT ~34 MB, `PORT=8080`, `UseStaticFiles()` Svelte SPA hint in source footer)

Source is a production-grade, dark/light-aware observability surface for an edge LLM router. This document codifies the visual language exactly as shipped in v2, alongside gaps filled with sensible defaults marked [default].

## Source Context & Evidence Map

This system was generated from copied project files in this workspace. All visual decisions are source-backed — not invented.

- **Source project:** `6a3e0d06-47c7-41fe-848c-3ce0ad840746` "Web Prototype" (kind: prototype, examplePrompt: Dashboard, high-fidelity for product evaluators). See `context/source-context.md`.
- **This workspace:** `111d9616-ed1b-4313-ae9a-35df1d2ec2bc` (design system `user:web-prototype-design-system-2`, title Neutral Modern, linked local dir `/Users/fanioz/code/dotnet/minirouter`).
- **Primary evidence:** `minirouter-analytics-v2.html` (64 KB) — preserved at project root outside `context/` per package contract. All tokens, type, spacing, layout, components, motion, and copy are extracted verbatim from its inline `<style>` and markup. See `context/provenance.md` for extraction lineage.
- **Design tokens:** `colors_and_type.css` — single file consolidating `:root` + `html.dark` remaps (bg, surfaces, fg, muted/meta, borders, accent, success/warn/danger, radius, shadows, fonts, motion).
- **Assets:** `assets/wordmark.svg`, `assets/favicon.svg`, `assets/provider-dots.svg` — derived from source primitives (◈ 32px ink square + text; no external binaries in source). `fonts/` and `build/` are intentionally empty — source has no font binaries or build artifacts (see provenance).
- **Previews:** `preview/colors-primary.html`, `preview/colors-semantic.html`, `preview/typography-specimens.html`, `preview/spacing-tokens.html`, `preview/radius-shadows.html`, `preview/components-buttons.html`, `preview/components-data.html`, `preview/brand-assets.html`, `preview/surfaces-dashboard.html` — each loads `colors_and_type.css` directly for visual proof.
- **Applied kit:** `ui_kits/app/index.html` + `ui_kits/app/components.html` — re-express the analytics shell on the live tokens (see `ui_kits/app/README.md`).

---

## 1. Visual Theme & Atmosphere

**Product context:** MiniRouter is a Native AOT edge router that proxies `chat.completions` across multiple LLM providers (openai-primary, anthropic, groq) with explicit `providerId/model` routing, no retries, and per-request circuit logic. The dashboard exists so platform operators can answer, in seconds: is traffic healthy? which provider is hot? what did we spend? The v2 brief was "same coral + warm-paper palette, but higher density: KPIs with trends, time-series with latency overlay, provider health strip, sortable performance table, and model leaderboard — all filterable by provider & time range." (source `compare-banner` copy).

**Mood:** Warm minimal, editorial-restrained. Not a marketing site, not a neon ops console. The palette is paper-warm (FDFAF6) rather than pure gray; the accent is a muted coral (E56A4A) — confident but not loud. Corners are soft (12–16 px), shadows are whisper-light in light mode and disappear in dark mode. The overall impression is a *calm instrument*: dense data, but generously aired with 14–24 px gutters and sticky chrome that never competes with content.

**Personality cues:**
- Quantitative first — tabular numbers, monospace tokens, p50/p95 always paired.
- Cautious about cost — "Pricing is a point-in-time snapshot. Costs are estimates for comparison only." sits directly above the table.
- Live but not noisy — a single 8 px `live-dot` with a pulse, reused everywhere; no flashing banners.
- Comparison-friendly — v2 banner explicitly frames the upgrade so evaluators can judge delta vs legacy layout.

**Keywords:** warm paper · coral signal · quiet precision · dense-but-breathing · operable.

---

## 2. Color

### 2.1 Palette (verbatim from `:root`)

| Role | Hex | OKLCH | Usage |
|---|---|---|---|
| **bg** | `#FDFAF6` | `oklch(98.2% 0.015 55)` | Page canvas — warm paper, not white |
| **surface** | `#ffffff` | `oklch(100% 0 0)` | Cards, sidebar, inputs, table head (`#FCFCF9` variant used sparingly) |
| **surface-warm** | `#FFF7F2` | `oklch(98.7% 0.012 45)` | Pills, soft badges, health hover, drawer accent |
| **fg** | `#111111` | `oklch(17% 0 0)` | Primary text, active nav, primary button text inverse |
| **fg-2** | `#2B2B2B` | `oklch(26% 0 0)` | Mono cells |
| **muted** | `#6B7280` | `oklch(58% 0.02 260)` | Secondary text, nav icons idle |
| **meta** | `#9CA3AF` | `oklch(70% 0.02 260)` | Labels (10.5 px uppercase), axis, placeholders |
| **border** | `#E5E7EB` | `oklch(92% 0.006 260)` | Card strokes, input borders |
| **border-soft** | `#F0EDE9` | `oklch(95% 0.008 70)` | Warm dividers (gridlines, soft card edges) |
| **accent** | `#E56A4A` | `oklch(64% 0.16 32)` | Coral — one accent only: CTA, sparkline, chart line, nav badge, bar fills |
| **accent-hover** | `#CC5535` | `oklch(59% 0.16 32)` | Primary hover |
| **accent-active** | `#B74C2F` | `oklch(54% 0.16 32)` | Primary active |
| **accent-soft** | `rgba(229,106,74,.10)` | — | Icon-btn hover, focus wash |
| **accent-ring** | `rgba(229,106,74,.22)` | — | Focus ring (3 px) |
| **success** | `#0EA464` | `oklch(62% 0.15 155)` | Live dot, success progress, badge-live |
| **success-soft** | `#ECFDF5` | `oklch(98% 0.02 155)` | ↑ badge bg |
| **warn** | `#D97706` | `oklch(64% 0.12 75)` | Mid latency, warn badge |
| **warn-soft** | `#FFFBEB` | `oklch(99% 0.012 85)` | Warn bg |
| **danger** | `#DC2626` | `oklch(55% 0.20 28)` | Bad latency, open circuit, `fail` |
| **danger-soft** | `#FEF2F2` | `oklch(98% 0.01 18)` | ↓ badge bg, fail bg |

**Provider color pins (semantic, not brand):** `openai-primary #E56A4A` (coral), `anthropic #111111` (ink), `groq #6B7280` (stone) — used for cost bars, dot before provider id, and health bullets.

**Dark remap (verbatim `html.dark`):** bg `#0f0f0f`, surface `#1a1a1a`, warm `#232323`, fg `#f0f0f0`, muted `#9aa0a8`, meta `#7a7f87`, border `#2e2e2e`, accent lightens to `#EE8D6A`, success `#22c97a`, shadows drop to `none` / `0 16px 40px rgba(0,0,0,.5)`. Inputs, cards, tabs invert to dark surfaces with `border:#2e2e2e`.

### 2.2 Usage rules
- One accent per screen. Coral appears at most twice per viewport (CTA + one data encoding). Never pair with a second saturated hue in the same card.
- Warm paper is the only background tint. No beige washes beyond `--surface-warm`; no gradients on containers (gradient is reserved for the SVG `areaGrad` under the chart line, `.24→0` opacity).
- Text-on-accent is always white. Text-on-warning/danger uses white on solid badges, dark on soft backgrounds is forbidden beyond the soft pair already in code.
- Borders are always `1px solid var(--border)` on cards/tables/inputs; soft dividers use `--border-soft`. No borderless cards in the data area — the page relies on stroke, not shadow, for separation (`shadow-card` is intentionally faint).

### 2.3 Derived scales
Generate tints with `oklch()` shifts of ±0.06–0.12 L. Hover moves `L + -0.06`; focus uses `accent-ring` via `box-shadow`. Never derive by hex interpolation.

---

## 3. Typography

**Family decision:** Both display and body are **Inter** (`--font-display`, `--font-body` = `"Inter",-apple-system,system-ui,sans-serif`). The source deliberately collapses display/body to one family for instrument legibility;Mono is `ui-monospace,"JetBrains Mono",SFMono-Regular,Menlo,monospace`. No Fraunces/serif anywhere — the “display” slot is Inter Tight-spacing, not a different face.

| Level | Size / Weight | Tracking | Line-height | Usage |
|---|---|---|---|---|
| **Page H1** | 28 px / 700 | -0.03em | 1.05 | `.page-title h1` "Router analytics" |
| **Panel H3** | 13.5 px / 700 | -0.01em | 1.3 | `.panel-header h3` — card titles |
| **Brand name** | 15 px / 700 | -0.02em | 1 | Sidebar `MiniRouter` |
| **Brand sub** | 10.5 px / 600 | 0.06em uppercase | 1 | `Native AOT • Edge Router` — meta under brand |
| **Label caps** | 10.5 px / 750–800 | 0.07–0.09em uppercase | 1 | `KPI_LABEL`, `THEAD TH`, `select label`, `muted` card labels |
| **Body** | 13–13.5 px / 400–500 | normal | 1.5 | Paragraphs, provider names, log messages |
| **Mono cell** | 12 px / 500–700 | tabular-nums | 1.4 | Tokens, baseUrl, `log-time`, `money`, model ids |
| **Badge / pill** | 11 px / 750 | 0.02em | 1 | `.badge`, `.cost-sub`, `.log-status` |
| **KPI value** | 26 px / 700 (tabular-nums) | -0.03em | 1 | `KPI strong` — dominant number |
| **Caption / foot** | 12 px / 400 | — | 1.5 | `kpi-foot`, `chart-meta`, `muted` disclaimers |

**Pairing rule:** Inter does all duty; contrast comes from size/weight/tracking, not family contrast. Mono only for code, tokens, numbers, and timestamps.

**Numerics:** KPI values, money, request counts, latency, and `Mono` columns use `font-variant-numeric: tabular-nums` to prevent jitter on live updates.

**Fallbacks:** `-apple-system, BlinkMacSystemFont, "Segoe UI"` for Inter; monospace falls through `ui-monospace` → `JetBrains Mono` → `SFMono-Regular` → `Menlo`.

---

## 4. Spacing

### 4.1 Scale (implemented values)
`4 / 8 / 12 / 16 / 20 / 24 / 32 / 40` — expressed in source as literal paddings, now tokenized as `--space-1`→`--space-10`. No 6 px or 10 px steps appear; density is regular.

- Card padding: `16/14` (KPI: `16 16 14`, cost `12 14 14`, health `10 11`, drawer `16`).
- Panel header: `13px 16px` with `1px` bottom stroke.
- Table cells: `thead 10/12`, `tbody 12/12`.
- Gaps: KPI grid `14px`, `grid2/grid3` `14px`, `nav-group` bottom `16px`, `chart-wrap` `14 16 4`.
- Sidebar: `268 px` wide, head `64 px`, nav `12/10/8` padding, foot `12/10`.

### 4.2 Radius
`--radius-sm 8px` (icon-btn, switches, health dots track), `--radius-md 12px` (cards, cost banner, health cards), `--radius-lg 16px` (large surfaces), `--radius-pill 9999px` (all CTAs, tabs, seg, search, badges, progress bars). Pill is the dominant shape; every control except cards is pill.

### 4.3 Density
High-density but padded. The page uses 14 px inter-card gutters on a `1360px` max container, with sticky chrome (`sidebar` + `topbar 64px`). Responsive: at `1120px` the KPI collapses 4→2 and `grid2/grid3` stack to single column; at `860px` sidebar becomes horizontal strip, topbar wraps; at `640px` search hides and title stacks. No horizontal scroll at any breakpoint (grid collapses, not squeezes).

### 4.4 Rhythm
Border-over-shadow: cards are defined by `1px border + faint shadow-card`, not elevation alone. The shadow is clipped (`0 1px 2px + 0 8px 20px @ .04`); raised surfaces (drawer) jump to `shadow-raised`. Dark mode removes `shadow-card` entirely and relies on border.

---

## 5. Layout & Composition

### 5.1 Shell
```
.shell (flex row) → .sidebar (268px, sticky 100vh, column) + .main (flex:1, min-width:0, column)
  .topbar (sticky 64px, 20/24 px padding, backdrop blur+saturate)
  .content (24/20/32 padding, 1360 max, centered)
    .page-title (flex space-between, h1 + p 62ch + title-actions)
    .compare-banner (warm 1px #FFD9C2, 12px radius)
    .kpis (grid 4 → 2)
    .grid2 (1.62fr .92fr → 1fr)
    .section-providers (card + table + footer bar #FCFCF9)
    .grid3 (1.32fr .88fr → 1fr)
    footer bar
  .drawer (fixed, 440px/92vw)
```

### 5.2 Grids & proportions
- **KPIs:** equal 4-col; each KPI is vertical stack (label→value→foot→mini viz).
- **Traffic + Cost:** asymmetric `1.62 / 0.92` — chart gets breathing room, cost strip stays compact.
- **Providers table:** full-bleed inside card; header sticky `top:0` with `#FCFCF9` and `z-index:1`.
- **Bottom pair:** `1.32 / 0.88` — models slightly wider than logs.
- Fractions are intentionally non-integer to create a subtle "chart-dominant" hierarchy without feeling templated.

### 5.3 Navigation & wayfinding
- **Sidebar:** two groups (`Operate` — Home/Providers/Presets/Models/Playground; `Observe` — Analytics/Logs/API Keys). Labels are muted caps, items have `nav-ico 18px` + count/badges on the trailing edge. Active state is full ink fill (`bg:var(--fg) color:white border:var(--fg)`), not a left bar. Counts invert (`rgba(255,255,255,.18)`).
- **Topbar:** breadcrumb `MiniRouter / Analytics` → `vsep` → coral `comparison` pill, then right-aligned controls: search pill (240px), provider select, segmented `24H/7D/30D`, Refresh ghost, Export primary. Backdrop is `rgba(253,250,246,.88) saturate(1.5) blur(10px)` — warm, not glass.
- **Content chrome:** Page title pairs a long-form description ("Costs are snapshot estimates…") with two badges (`Live tail`, `Updated just now • :8080`).

### 5.4 Responsive behavior
Two breakpoints drive meaningful reflow, not squeeze: KPI and grids stack; sidebar becomes horizontal pill-nav; topbar becomes wrapping toolbar. Table wrapper `overflow:auto` with `-webkit-overflow-scrolling:touch`. Nothing overflows the container; word wrapping uses `text-overflow:ellipsis` on `cost-provider` and `providerId` paths.

---

## 6. Components

### 6.1 Buttons
- **Ghost pill (`.btn`)** — `36h`, `1px border`, white, `13px/650`, `999px`, `gap 8`. Hover: `border #D1D5DB bg #FCFCF9`. Active: `translateY(1px)`. Disabled: `opacity .5`. Focus: `accent-ring`.
- **Primary (`.btn-primary`)** — accent fill white text. Hover `#CC5535`, active `#B74C2F`. Inverted hover swaps both fg/bg together. Only one primary per viewport (`Export CSV`, drawer `Save changes`).
- **Tabs (`.tab`)** — `28h`, `11/12px` sides, pill, ghost → ink active identical to nav active. Used in `Traffic & latency` metric switch.
- **Segmented (`.seg`)** — `36h` pill container with `3px` inset padding, inner buttons `28h` ghost → ink active. Used for time range.
- **Icon button (`.icon-btn`)** — `36px` square, `9px` radius, `muted` idle → `accent-soft` hover + `fg` icon. Drawer close uses this.

### 6.2 Cards (`.card`)
`background:var(--surface) border:1px solid var(--border) radius:md 12px shadow-card`. No hover lift except `health-card:hover { bg #FCFCF9 border #D1D5DB }`. Table rows hover `#FFF9F5`, focus-within `#FFF4EE`.

- **KPI card (`.kpi`)** — `16/14` padding, stacked: head (caps label + `kpi-icon 28px` warm square 8px radius), value (26px + `up/down` pill with `#A7F3D0/#FECACA` border), foot (12px muted + bold), viz (sparkline `stroke var(--accent) 2px` or `mini-bars 36h`, accent bars mark high latency, or progress `6h` rounded).
- **Cost row** — `cost-provider 110px/12px/650` clipped + ellipsis, `bar-track 8h #F3F4F6` with `bar-fill` in provider color, `cost-val 84px` right tabular.
- **Health grid** — `repeat(2,1fr) 10px` gap cards: `10/11` pad, `10px` dot (success/danger/gray), name `13px`, badge `10px`.

### 6.3 Navigation components
- **`.nav-item`** — `100%` flex, `9/10` pad, `10px` radius, `13.5/500`. Idle `muted`, hover `#F9F5F1 + fg`, active ink, badge `nav-badge accent 10/800` or `nav-count #F3F4F6 12/600`. Dark inverts hover to `#232323`.
- **Breadcrumb + comparison** — `bc 13px` muted → `strong fg`, `vsep 1/20`, `comparison 12/600 #7C4A32 on #FFF1E8 border #FFD9C2 pill`.
- **Brand** — `◈` in `32px` ink square `9px` radius, name `15/700 -0.02`, sub `10.5/600 0.06 uppercase meta`.

### 6.4 Forms
- **Search (`.search input`)** — `36h` pill, `12/34` left inset for icon, `13px`, placeholder `meta`. Focus `accent` border + ring.
- **Select pill (`.select`)** — `36h` pill, `label 11/700 0.07 uppercase muted` + native select `13/650 fg`.
- **Input (`.input`)** — `36h`, `10px` radius (not pill — the only non-pill input), `11` pad, `13px`. Disabled `#F9FAFB muted`. Drawer uses two instances (Base URL, Force model).
- **Switch (`.switch`)** — `36×22` track `#E5E7EB` → `accent` checked, `18px` thumb `0 1px 2px rgba(0,0,0,.16)`, `14px` translate. Drawer row uses `toggle 40×24` variant.

### 6.5 Data components
- **Provider table** — `thead th: 10.5/750 0.07 uppercase meta bkg #FCFCF9 sticky`, `tbody td: 12/12 bdr #F3F4F6`, hover/focus tints coral-warm. Cells: provider id `em 8px dot + 700 -0.01`, baseUrl mono `12/fg-2`, requests `b tabular + 6h progress`, tokens `mono-cell`, latency `lat good/mid/bad` (thresholds: `p95<350 good`, `<500 mid`, otherwise bad), success badge soft, circuit `badge-live/open`, cost `mono $` + `muted est`, toggles+copy action group.
- **Model leaderboard (`.model-row`)** — `11/0` btb `#F3F4F6`, `rank 26px` ink vs `alt #F3F4F6+border`, name `13/650 -0.01 mono`, meta `11 muted`, right `13 mono + meta`, copy `30px 9px` square.
- **Log list (`.log-item`)** — grid `84px 1fr auto`, `11/14` pad, `1px bdr #F3F4F6`, hover warm. Time `mono 11 meta`, provider `700 13` + `log-model mono 11.5 muted` + `log-status 10.5/800 pill ok/fail`, msg `12 muted truncated ellipsis`, latency `mono 12 650`.
- **Chart** — SVG `720×200` (`pad 36/16/12/22`), `gridline #F0EDE9 1px`, `axis mono 10 meta`, `area fill url(#areaGrad)`, `line accent 2.2`, `line2 fg dashed 1.5 .5 opacity`, dots `r3.5 white stroke accent 1.8` on every third point. Legend `dot 8px + b fg 650`. Data switches by range (24h/7d/30d) and metric (requests/tokens/latency) via `buildChart()`.
- **Compare banner** — `flex 12/14` gap, `1px #FFD9C2`, `surface-warm`, `12px` radius, diamond `28px` ink badge, title `12.5/ -0.01`, body `12 muted`.

### 6.6 Overlays
- **Drawer (`.drawer`)** — fixed inset, `display:none` → `.open:block`, bg `rgba(17,17,17,.38) blur(2px)`, panel `right 0 top 0 100%h min(440px,92vw) surface bl border shadow-raised` scroll `overscroll:contain`. Inner sections: provider summary, stat 2-col cards, labeled inputs, action row (Test ghost + Save primary), warning card `warn-soft/FDE68A` with `92350E/78350F` text.
- **Toast (`.toast`)** — fixed `18/18`, `fg bg white`, `11/14` pad, `999px`, `13/650`, `shadow-raised`, enter: opacity + translateY `motion-base/ease-standard`, auto-hide `2200ms`.

### 6.7 Badges & progress
- **`.badge`** — `11/750 0.02 pill 4/8 1px` white vs `badge-live success`, `badge-warn`, `badge-muted #F3F4F6`, `badge-open danger`, `badge-soft surface-warm #FFD9C2 text #7C4A32`. `log-status ok/fail` are `success-soft/danger-soft` with matching borders.
- **`.progress`** — `6h pill #F3F4F6 + i accent`.

---

## 7. Motion & Interaction

**Easing/duration:** `--ease-standard cubic-bezier(0.2,0,0,1)`, `--motion-fast 160ms`, `--motion-base 220ms`. Applied to `background, border-color, color, transform, box-shadow`. Hover/press use fast; chart bar width uses base.

**Hover:** background shifts one warm step (`+0.06 L` or `#FCFCF9`) plus `border #D1D5DB` — never muted text on hover. Primary hover is `-0.05 L` (`accent-hover`).

**Active:** `translateY(1px)` on `.btn`, `.copy`, `.nav-item`.

**Focus:** `box-shadow: 0 0 0 3px var(--accent-ring)` on every focusable (`a`, `button`, `input`, `select`). Dark swaps to `rgba(238,141,106,.28)`.

**Pulse:** `live-dot 8px` (`9aa` shadow loop `2.2s` 4px→7px via `@keyframes pulse`). Respects `prefers-reduced-motion` (`animation:none`).

**Shimmer:** `.skel` `linear-gradient 90deg transparent→rgba(255,255,255,.6)→transparent` `1.4s` infinite; disabled under `prefers-reduced-motion`.

**Chart:** `mini-bars i height` and `bar-fill width` transition `motion-base`.

**No scrollIntoView:** project enforces `localStorage` for drawer/persist (theme) only; no imperative scroll.

**Reduced motion:** pulses and shimmers suppressed. All other transitions degrade gracefully without alternate animation.

---

## 8. Voice & Brand

**Tone:** Precise, operator-grade, quietly confident. No exclamation, no emoji, no marketing superlatives. Copy is instruction-oriented and caveated where cost is involved.

**Terminology (canonical):** `Provider`, `Base URL`, `Model`, `Latency p50 / p95 (ms)`, `Tokens in / out`, `Success`, `Circuit (closed/open)`, `Est. cost`, `Force model (optional)`, `Explicit routing: providerId/model • no retries`, `Measured / Estimated`, `4 char ≈ 1 tok est.`, `stream_options`, `estimated=true`.

**Capitalization:** Sentence case for prose; **uppercase + wide tracking** only for meta labels (`10.5/700 0.07–0.09em`). Table headers are caps/small but still semantic `th`. Badges use capitalized words (`Live`, `Closed`, `Failed`) except monospace identifiers (`providerId`, `gpt-4o-mini` are lowercase).

**Provider names:** `openai-primary` (hyphenated lowercase), `anthropic` (lowercase), `groq` — never title-cased. Model ids are verbatim lowercase with hyphens/dots (`gpt-4o-mini`, `claude-3-haiku`, `llama-3.1-70b`).

**Disclaimer voice:** Costs are always qualified ("snapshot estimates, not invoiced amounts", "point-in-time snapshot. Costs are estimates for comparison only", "Measured $X / Estimated $Y").

---

## 9. Anti-patterns

- No purple gradient wash or multi-accent rainbows — coral is the only saturated signal; using it more than twice per viewport creates false urgency.
- No emoji as functional icons — all icons are `stroke 1.7–1.8` line icons (home, gear, layers, diamond, chat, bar, document, key).
- No left-bar active indicator on nav/table — active is full ink fill.
- No gray/light text on hover — hover must increase contrast, never reduce to `muted`.
- No `Inter, Roboto, Arial, Fraunces` as display face alone — display *is* Inter but tight-tracked; do not replace with a serif or display-specific face.
- No hand-drawn SVG people/scenes — this is an ops instrument, not an illustration surface.
- No multiple solid primaries per viewport — Export CSV is the page primary; drawer Save is the only other primary and it lives in an overlay, not the same viewport.
- No icon beside every heading — only KPI icons and panel headers earn an icon; table and list headings stay text-only.
- No warm beige/cream global background — the canvas is `#FDFAF6` warm paper, not beige; do not shift it warmer.
- No borderless cards — every data card carries `1px border`; removing it breaks the density system.
- No `white-space:nowrap` forcing titles off-screen — `cost-provider` and long URLs must clamp with `ellipsis`, not overflow.
- No hidden overflow concealing orphaned line breaks — fix container or tracking, don't clip.
- No charts as outlines only — area must carry `fill url(#areaGrad)`; bars must be solid fills in provider color or muted gray.
- No invented metrics or filler copy — if a token count is estimated, mark `estimated=true` / `• est`; never fabricate invoiced cost.
- No control panels that exist only for the designer — every input (search, provider select, range seg, sort, table filter) affects live data, not demo scaffolding.

---

## Provenance & Reuse Notes

- All hex, spacing, radius, shadow, and motion values are verbatim from `minirouter-analytics-v2.html :root`. Inter is the sole display/body family; monospace fallback was normalized to `JetBrains Mono` as implemented.
- Icons are inline SVGs (stroke weight 1.7–1.8); no external icon font is referenced — reuse by copying inline SVG, not by importing a library.
- Fonts are system + Inter; no web-font binary is bundled. Replace with hosted Inter only if licensing allows, otherwise the system stack renders identically on macOS/Windows.
- Color and typography foundations live in `colors_and_type.css`; component markup lives in `ui_kits/app/`.
- High-signal source is preserved outside `context/` as `minirouter-analytics-v2.html` (64 KB).
