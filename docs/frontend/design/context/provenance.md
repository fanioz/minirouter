# Provenance — MiniRouter Neutral Modern

## Source
- Project id: `6a3e0d06-47c7-41fe-848c-3ce0ad840746` "Web Prototype"
- New workspace: `111d9616-ed1b-4313-ae9a-35df1d2ec2bc` (design system id `user:web-prototype-design-system-2`)
- Copied evidence: `minirouter-analytics-v2.html` (64 KB, Admin Analytics v2, single HTML prototype with inline `<style>` and `<script>`)
- Linked dir: `/Users/fanioz/code/dotnet/minirouter` (Native AOT, PORT 8080, UseStaticFiles Svelte SPA — not copied, referenced in footer copy only)
- Prompt kind: `prototype` / `examplePromptTitle: Dashboard` for product evaluators, high-fidelity

## What was extracted verbatim
- **All `:root` tokens** — bg, surfaces, fg, muted/meta, borders, coral accent + hover/active/soft/ring, success/warn/danger (+soft), radius, shadows, font stacks, container max, ease/durations. Copied without hue shift into `colors_and_type.css` and added OKLCH comments.
- **`html.dark` remap** — complete inversion (bg #0f0f0f, surface #1a1a1a, accent #EE8D6A, muted #9aa0a8, shadows removed). Preserved as the only dark-mode implementation.
- **Type scale** — Inter for display+body, JetBrains Mono for mono; sizes/weights/trackings taken from `.page-title h1`, `.panel-header h3`, `.kpi-label`, `thead th`, `.mono-cell`, `.badge`.
- **Layout constants** — sidebar 268px, topbar 64px, content max 1360px, grid fractions 4 / 1.62:0.92 / 1.32:0.88, gaps 14px, breakpoints 1120/860/640.
- **Component classes & behaviors** — `.btn/.btn-primary/.tab/.seg/.icon-btn`, `.card/.kpi/.cost-row/.health-card`, `.nav-item` active ink, `.breadcrumb/.comparison`, `.search/.select/.input/.switch`, table / model / log / chart / drawer / toast visuals and motion tokens.
- **Copy tone & terminology** — provider identifiers, latency/token/cost language, disclaimers.
- **Icons** — inline stroke SVGs (1.7–1.8) for home/gear/layers/diamond/chat/bar/document/key/search/refresh/copy/close. No icon font.

## What was normalized (not invented)
- OKLCH annotations added alongside hex for downstream use (no value change).
- Font fallback order normalized to `-apple-system, BlinkMacSystemFont, "Segoe UI"` and `ui-monospace, JetBrains Mono...` (source used `-apple-system,system-ui`).
- Spacing literals re-tokenized as `--space-1`..`--space-10` (4-based scale already implied by paddings).
- `colors_and_type.css` consolidates `shadow-card/raised`, `ease`, `motion`, and `focus-ring` in one file so previews and kits can load it directly.

## What was not present (and therefore not bundled)
- **No external logo/wordmark file, favicon, or avatar** — source renders the brand as `◈` in a `32px` ink square plus text. Assets therefore consist of a generated wordmark SVG and provider dot swatches derived from the three provider colors.
- **No font binaries** — Inter and JetBrains Mono are referenced via system stack only. `fonts/` is absent intentionally (no `woff2` to preserve).
- **No runtime build icons** — `build/` is not required for a single-HTML source with inline SVGs.
- **No additional pages** — source is one dashboard surface; the kit re-expresses it across `index.html` + `components.html`.

## Preserved artifact
- `minirouter-analytics-v2.html` retained outside `context/` at project root (64 KB) as high-signal source evidence per generation contract. Preview cards and `ui_kits/app/` reference it implicitly by reusing its layout and sample data, not by importing its inline style — the token file is the live source of truth.

## Verification
- `"$OD_NODE_BIN" "$OD_BIN" tools connectors design-system-package-audit --path . --fail-on-warnings` is the completion gate. All token hex values round-trip against `:root` in the preserved HTML.
