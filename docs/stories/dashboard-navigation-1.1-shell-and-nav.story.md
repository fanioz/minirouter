---
clickup:
  task_id: ""
  epic_task_id: ""
  list: "Backlog"
  url: ""
  last_sync: ""
executor: "@dev"
quality_gate: "@po"
quality_gate_tools: ["coderabbit", "npm-build", "vitest"]
---

# Story dashboard-navigation.1.1: Dashboard UI Shell, Collapsible Left Nav, and Routing Skeleton

## Status

In Review

## Context (epic)

This is the first story of the `dashboard-navigation` epic (`docs/stories/epics/dashboard-navigation/execution-plan.yaml`). The epic stands up a proper dashboard shell with a left-side, collapsible navigation. This story covers **Wave 1** only: the shell, the nav, and a routing skeleton for the four primary tabs (Home, Providers, API Key, Logs). Subsequent waves (2 and 3) move existing views into the new tabs and add placeholder content — they are deliberately out of scope here.

## Story

**As a** MiniRouter operator opening the dashboard,
**I want** a persistent left-side navigation with Home, Providers, API Key, and Logs tabs, a collapsible sidebar, and real route semantics (back/forward, deep links, refresh-safe),
**so that** the dashboard has a stable, navigable shell that the next two waves can populate without further layout work.

## Acceptance Criteria

1. **Given** a first-time visitor on `/`,
   **When** the SPA loads,
   **Then** the dashboard renders the full MiniRouter shell: 268px sidebar (collapsed 64px), 64px topbar, 1360px-max content area — matching the dimensions in `docs/frontend/design/colors_and_type.css` (`--sidebar-w`, `--topbar-h`, `--container-max`).

2. **Given** the shell is rendered,
   **When** the user clicks a nav entry (Home / Providers / API Key / Logs),
   **Then** the URL hash updates to `#/home`, `#/providers`, `#/apikey`, or `#/logs` respectively, and only the corresponding view component is mounted in the content slot.

3. **Given** the user has navigated to a tab,
   **When** they press the browser **Back** button,
   **Then** the dashboard returns to the previous tab and the URL hash reflects it; **Forward** behaves symmetrically.

4. **Given** the user pastes a URL with `#/providers` into a new tab,
   **When** the SPA loads,
   **Then** the Providers view renders directly without a flash of the Home view, and the Providers nav item shows the active state.

5. **Given** the shell is rendered on a viewport `< 860px` wide,
   **When** the page loads,
   **Then** the sidebar is hidden (per the existing `min-[860px]:flex` breakpoint in `App.svelte`); the four routes remain reachable via a future mobile menu (out of scope here, but the breakpoint must not regress).

6. **Given** the user clicks the collapse toggle (`PanelLeft` icon) in the sidebar head,
   **When** the click registers,
   **Then** the sidebar animates from `268px` to `64px` (or back) using the design system's `--motion-base` timing, and the brand wordmark/labels collapse out while icons remain visible.

7. **Given** the user has collapsed or expanded the sidebar and selected a tab,
   **When** they reload the page (with the same `#/...` route),
   **Then** both the collapsed state and the active route are restored from `localStorage` under the key `minirouter:nav` (versioned subkey for forward-compat).

8. **Given** no prior preference exists in `localStorage`,
   **When** the page loads,
   **Then** the sidebar defaults to **expanded** and the route resolves to `#/home` (or the hash if one is already present).

9. **Given** the user toggles the theme (existing `mode-watcher` cycle button in the topbar),
   **When** the theme changes,
   **Then** the shell's colors update via the existing CSS variable remap (`html.dark` block in `colors_and_type.css`); no shell-specific dark-mode code is added.

10. **Given** the shell is fully wired,
    **When** `npm run build` and `npm test` are run,
    **Then** the production build succeeds, and a new `App.test.js` assertion set covers: (a) default route is `#/home`, (b) clicking Providers updates the hash and active class, (c) `localStorage` round-trip restores collapsed + active state across remount.

## Out of Scope (this story)

- **Moving the existing Providers table into the Providers tab.** That's Wave 2. In this story, the Providers route renders a placeholder ("Providers — moved in Wave 2") so the routing skeleton has something to land on.
- **Adding Home, API Key, or Logs placeholder content.** Those waves (Wave 3) render `<h1>{Label} — placeholder</h1>` only.
- **Mobile nav menu / drawer.** The breakpoint hides the sidebar on small screens; a hamburger menu that re-exposes nav is a separate story.
- **Sub-routes or nested routing.** No `#/providers/:id` yet. Wave 2 may introduce them.
- **Re-skinning the existing components.** The `Providers.svelte`, `ApiKey.svelte`, `Logs.svelte`, `Home.svelte` files stay untouched in this story.
- **Backend changes.** No new endpoints, no CORS changes, no API contract changes.

## In Scope (this story)

- Introducing a thin client-side **hash router** (~30 LOC, no new dependency) that maps `#/<segment>` → component.
- Restructuring `App.svelte` so the sidebar + topbar live in a `<Shell>` layout and the active route is rendered into a `<slot>`/named slot. The current `activeTab` `$state` is replaced by a derived store from the router.
- Persisting `{ collapsed, route }` to `localStorage` under `minirouter:nav` (versioned: `minirouter:nav:v1`).
- Keeping the existing `mode-watcher` integration intact; the theme toggle button moves into the topbar where it already is, but the topbar now has a stable `64px` height (per the design system).
- New `App.test.js` cases for routing + persistence (extending, not replacing, the existing test).

## Dependencies

- **Design system:** `docs/frontend/design/colors_and_type.css` tokens (`--sidebar-w`, `--topbar-h`, `--motion-base`, `--accent-ring`) and `docs/frontend/design/README.md` reuse rules. The shell must copy markup from `docs/frontend/design/ui_kits/app/components.html` rather than inventing new class names.
- **Existing shell code:** `frontend/src/App.svelte` (already has the collapse toggle and the two nav groups). This story **formalizes and routes** what's there, it does not throw it away.
- **mode-watcher:** already wired for theme persistence; do not duplicate the LocalStorage pattern.
- **bits-ui + Lucide icons:** already in `package.json`; reuse `PanelLeft`, `LayoutDashboard`, `Settings`, `Key`, `ScrollText` (no new icon dependencies).
- **No backend dependency** for Wave 1 — the four route components render placeholders only.

## Complexity Estimate

**T-shirt: M** (Medium). Wave 1 is mostly glue: ~50–80 LOC in `App.svelte`, a new `lib/router.ts` (or `.js`) of ~30 LOC, a `lib/persist.ts` of ~15 LOC, and 3–5 new test cases. No backend work, no DTO changes, no AOT considerations. Could slip to L if the existing `App.svelte` proves harder to restructure without regressing the current tabs.

## Dev Notes (assumptions logged)

- **[AUTO-DECISION] Routing strategy → custom hash router (~30 LOC, no new dependency).** The wave says "routing skeleton" — that's real route semantics, not a `{#if}` switch. Hash routing avoids server config and matches the SPA's static-file deployment. We will **not** pull in `svelte-spa-router`; the requirement is small enough that hand-rolled is cheaper than a dependency.
- **[AUTO-DECISION] Persistence → LocalStorage (`minirouter:nav:v1`).** Matches the existing `mode-watcher` pattern, survives reload, and is what "Operator opens the dashboard, the sidebar is where I left it" means in practice.
- **[AUTO-DECISION] Default state → expanded sidebar, `#/home` route.** No prior preference is the common case; expanded is the discoverable default.
- **Two nav groups are already present in `App.svelte` (Operate / Observe).** Wave 1's four tabs (Home, Providers, API Key, Logs) cross both groups. The story will **keep** the two-group header in the sidebar (per the design system §5.3 referenced in the existing `App.svelte:20`) — the four tabs simply appear under their existing group. This is a structural decision `@po` should sanity-check.
- **The existing `App.svelte` already has a working collapse toggle and 8 tab buttons.** This story is a formalization, not a from-scratch build. Wave 1 must not regress the visual design that already matches `colors_and_type.css`.
- **Topbar height is currently driven by the page content (no fixed topbar yet).** Wave 1 introduces a 64px sticky topbar with a breadcrumb (`Home` / `Providers` / etc.) and the existing theme toggle. This is a deliberate visual change vs. today's prototype — `@po` should confirm it's desired before `@dev` lands it.

## Tasks / Subtasks

- [x] Task 1 (AC: 1, 5): Establish the Shell layout
  - [x] Wrap the current `App.svelte` body in a `<Shell>` component (or inline) with `display: flex`, 64px topbar, sidebar + content slot.
  - [x] Apply `--sidebar-w` (268 / 64 collapsed) and `--topbar-h` (64) tokens from `colors_and_type.css` — do **not** hardcode pixels in component code.
  - [x] Preserve the `min-[860px]:flex` mobile breakpoint behavior.
- [x] Task 2 (AC: 2, 3, 4, 8): Implement the hash router
  - [x] Create `frontend/src/lib/router.js` exporting `route: Readable<string>`, `navigate(segment)`, `current()`, and `parseHash(hash)`.
  - [x] On mount, read `window.location.hash` (default `#/home`); subscribe to `hashchange` to update the store.
  - [x] Map segments: all 8 known route ids (home, providers, presets, models, playground, analytics, logs, apikey); reject anything else, fall back to `#/home`.
  - [x] Update `activeTab` references in `App.svelte` to derive from `route` store instead of `$state`.
- [x] Task 3 (AC: 2): Placeholder views
  - [x] Inline `<h1>` placeholder for Providers in `App.svelte` content slot ("Providers — Wave 2 placeholder").
  - [x] In the content slot, render `{:else if activeTab === 'providers'} <h1>Providers — Wave 2 placeholder</h1> {/if}` (Home/ApiKey/Logs/Presets/Models/Analytics/Playground keep their existing real components per Out of Scope + @po decision #1).
- [x] Task 4 (AC: 6, 7): Persist nav state
  - [x] Create `frontend/src/lib/persist.js` with `loadNav(): { collapsed, route }` and `saveNav(state)`.
  - [x] Use key `minirouter:nav:v1`; ignore malformed values; on read failure, fall back to defaults.
  - [x] On collapse toggle, persist; on `route` change, persist (via `$effect`).
- [x] Task 5 (AC: 9): Theme integration
  - [x] Theme toggle retained in both sidebar foot and topbar (mobile-only); no new dark-mode code; relies on `html.dark` class flip from `mode-watcher`.
- [x] Task 6 (AC: 10): Tests
  - [x] Extend `frontend/src/App.test.js` with: default route, click navigates and updates hash, browser back/forward via mocked `hashchange`, deep-link activates directly, localStorage round-trip, malformed JSON fallback, parseHash edge cases, KNOWN_ROUTES coverage.
  - [x] Run `npm test` and `npm run build`; both pass.

## Resolved Questions (`@po` decisions, 2026-08-28)

1. **Two-group nav retained?** — **RATIFY with revision.** Keep the Operate/Observe group structure (matches `App.svelte:20` and `DESIGN.md`). The story's silent de-scoping of Presets, Models, Analytics, and Playground is a regression risk — Wave 1 must route **all 8 existing tabs**, not just the 4 named in the execution plan. The 4 named tabs are the priority routes for new hash URLs; the other 4 keep their current `activeTab` semantics as a transitional backstop and are routed in the same hash scheme. Flattening to one list is rejected.
2. **Topbar introduction.** — **RATIFY (already exists).** The 64px sticky topbar is **already** in `App.svelte:163-186` with a breadcrumb and theme toggle. The "topbar introduction" framing in the story is inaccurate. Wave 1's actual work on the topbar is to derive the breadcrumb from the new route store instead of the `activeTab` `$state` — not to add a new topbar.
3. **Hash vs. history API.** — **RATIFY hash routing.** No new dependency, no `_redirect` config, matches the static-file deployment. History API would require a backend change that is out of scope for this story.
4. **Placeholder wording.** — **RATIFY generic "Wave N placeholder" text.** Wave descriptions in `execution-plan.yaml` are the source of truth; per-tab copy is not needed for placeholders.

## Definition of Done

- All 10 acceptance criteria pass.
- `npm run build` produces a working `wwwroot/` bundle.
- `npm test` is green; new test cases added for routing + persistence.
- No new package dependencies added.
- No backend code touched.
- `frontend/src/App.svelte` still uses `mode-watcher` and the existing Lucide icons; no hardcoded hex values introduced.
- Dev Notes assumptions are either ratified by `@po` or revised in a comment.
- Story status moved to `Ready` by `@po` (10-point checklist ≥ 7/10).

## Affected Files (initial — `@dev` will refine in Dev Agent Record)

- `frontend/src/App.svelte` (restructured)
- `frontend/src/lib/router.ts` (new)
- `frontend/src/lib/persist.ts` (new)
- `frontend/src/lib/views/HomePlaceholder.svelte` (new) — or inline
- `frontend/src/lib/views/ProvidersPlaceholder.svelte` (new) — or inline
- `frontend/src/lib/views/ApiKeyPlaceholder.svelte` (new) — or inline
- `frontend/src/lib/views/LogsPlaceholder.svelte` (new) — or inline
- `frontend/src/App.test.js` (extended)

## Dev Agent Record

### File List (actual)

- `frontend/src/lib/router.js` (new) — hash router, ~80 LOC, no dependency. Export surface: `route` (readable store), `navigate(segment)`, `current()`, `parseHash(hash)`, `KNOWN_ROUTES`, `DEFAULT_ROUTE`.
- `frontend/src/lib/persist.js` (new) — localStorage layer under `minirouter:nav:v1`. Export surface: `loadNav()`, `saveNav(state)`, `NAV_KEY`, `DEFAULTS`.
- `frontend/src/App.svelte` (refactored) — `activeTab` now derived from the `route` store; all 8 tabs route through hash; Providers placeholder inline `<h1>`; breadcrumb derived from store; collapse + active route persisted via `$effect`. Placeholders for Home/ApiKey/Logs are NOT added (per Out of Scope: "Adding Home, API Key, or Logs placeholder content"). Other 4 non-priority tabs (Presets, Models, Analytics, Playground) keep their real components per @po decision #1.
- `frontend/src/App.test.js` (extended) — 3 original tests retained, 8 new tests added covering: default route, click navigates + updates hash, hashchange (back/forward) syncs tabs, deep-link activates without flash, localStorage round-trip across remount, malformed JSON fallback, parseHash edge cases, KNOWN_ROUTES coverage.
- `frontend/src/setupTests.js` (extended) — added an in-memory `localStorage` shim because vitest@4's jsdom no longer provides one by default; required for the new persist tests to run.

### Deviations from story

- **`router.ts` -> `router.js`, `persist.ts` -> `persist.js`.** The story's affected-files list specifies `.ts`, but the project is plain ES modules with no `tsconfig` and no TypeScript in `package.json`. Wrote `.js` with JSDoc type annotations instead so the rest of the codebase stays consistent. The export surface is identical. @qa may want to either ratify this or move the project to TS in a separate story.
- **No new `lib/views/*.svelte` files.** Used inline `<h1>{Label} — Wave N placeholder</h1>` inside `App.svelte` per Task 3's "or inline `<h1>`" option. The only required placeholder was Providers; the other three priority tabs (Home, API Key, Logs) keep their existing real components per the Out of Scope section.

### Verification

- `cd frontend && npm test` -> **20 passed / 20 total** (3 pre-existing + 8 new routing/persistence + 9 from other test files).
- `cd frontend && npm run build` -> **succeeds**; emits `wwwroot/assets/index-C0_KzZZo.js` (293.61 kB / 82.19 kB gzip), `wwwroot/assets/index-IDYJPCnY.css` (59.83 kB / 12.03 kB gzip), `wwwroot/index.html` (1.17 kB).
- `cd Tests && dotnet test` -> **Passed: 84, Failed: 0, Skipped: 0** — no backend regressions.

### Test status snapshot

```
Test Files  4 passed (4)
     Tests  20 passed (20)
  Duration  25.92s
```

```
Passed!  - Failed:     0, Passed:    84, Skipped:     0, Total:    84, Duration: 19 s
- MiniRouter.Tests.dll (net10.0)
```

### Commit

- `4d24faa3691c154caad4e917bfbef2f96168eb79` — `feat: dashboard navigation shell + hash router [Story dashboard-navigation.1.1]`

## Change Log
| Date       | Version | Description     | Author      |
|------------|---------|-----------------|-------------|
| 2026-08-28 | 0.1     | Initial draft   | @sm (River) |
| 2026-08-28 | 0.2     | GO (score 9/10) — Status: Draft → Ready; resolved 4 open questions; flagged 2 factual corrections to Dev Notes (topbar already exists; wave must cover all 8 existing tabs, not 4) | @po (Pax) |
| 2026-08-28 | 0.3     | Status: Ready → In Review — Wave 1 implemented: hash router + persist layer + App.svelte refactor + 8 new tests; all 20 vitest + 84 dotnet tests pass; `npm run build` succeeds. Files: `frontend/src/lib/router.js` (new), `frontend/src/lib/persist.js` (new), `frontend/src/App.svelte` (refactored), `frontend/src/App.test.js` (extended), `frontend/src/setupTests.js` (localStorage shim). Commit `4d24faa`. | @dev (Dex) |

## Change Log
| Date       | Version | Description     | Author      |
|------------|---------|-----------------|-------------|
| 2026-08-28 | 0.1     | Initial draft   | @sm (River) |
| 2026-08-28 | 0.2     | GO (score 9/10) — Status: Draft → Ready; resolved 4 open questions; flagged 2 factual corrections to Dev Notes (topbar already exists; wave must cover all 8 existing tabs, not 4) | @po (Pax) |
