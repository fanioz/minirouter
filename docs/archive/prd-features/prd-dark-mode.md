# PRD: Apply Dark Mode to MiniRouter Dashboard

## 1. Goals and Background Context

### Goals
- Implement a fully functional dark mode for the MiniRouter frontend dashboard.
- Provide a user-accessible toggle to switch between Light, Dark, and System (OS-preference) modes.
- Persist the user's theme choice across sessions.
- Ensure all existing UI components, pages, and custom styles render correctly in both themes.

### Background Context
The MiniRouter frontend is a Svelte 5 SPA built with Vite, Tailwind CSS v4, and shadcn-svelte components (bits-ui). The project already includes two key dark-mode enablers that are **installed but not wired up**:

1. **`mode-watcher` (v1.1.0)** — A Svelte library for managing light/dark mode state, already imported in the Sonner toast component (`sonner.svelte`) but never initialized at the app level.
2. **Tailwind CSS v4 `dark:` variant** — Already used extensively in the shadcn-svelte UI primitives (button, badge, input, switch, checkbox), but the `<html>` element never receives a `class="dark"` attribute, so these styles are inert.

The current `app.css` defines a `:root` block with light-only custom properties (`--bg-color`, `--panel-bg`, etc.) and several hardcoded light-mode hex colors in hover states. There is **no `.dark` or `[data-theme="dark"]` counterpart**. Some Svelte components (e.g., `ApiKey.svelte`) use inline `dark:` Tailwind utilities, confirming the intent to support dark mode eventually.

The front-end spec (`docs/front-end-spec.md`) explicitly states the brand identity should be *"dark mode friendly, high contrast, clean lines"*, validating this feature as a planned enhancement.

### Change Log
| Date       | Version | Description               | Author       |
|------------|---------|---------------------------|--------------|
| 2026-08-12 | 0.1     | Initial PRD Draft         | Antigravity  |

---

## 2. Requirements

### Functional

| ID   | Requirement |
|------|-------------|
| FR1  | The app MUST initialize `mode-watcher` at the root level (`App.svelte` or `main.js`) so that the `<html>` element receives the correct `class` attribute (`dark` or `light`) based on the active mode. |
| FR2  | The sidebar MUST include a theme toggle control allowing the user to cycle between **Light**, **Dark**, and **System** modes. |
| FR3  | The user's chosen mode MUST be persisted in `localStorage` and restored on subsequent page loads. |
| FR4  | All CSS custom properties in `app.css` MUST have dark-mode overrides under a `.dark` selector (or equivalent Tailwind v4 mechanism) covering backgrounds, text colors, borders, and accent colors. |
| FR5  | All hardcoded light-mode hex colors in `app.css` (e.g., `#f3f4f6` in hover states) MUST be replaced with CSS custom properties or Tailwind semantic tokens so they adapt to the active theme. |
| FR6  | All page components (`Home`, `Providers`, `Models`, `ApiKey`, `Logs`, `Analytics`, `Playground`) MUST render correctly in both light and dark modes without visual regressions. |
| FR7  | The Sonner toast system (already wired to `mode-watcher`) MUST continue to function and automatically adapt its theme. |
| FR8  | The theme toggle MUST be keyboard-accessible (Tab, Enter/Space) and include an ARIA label describing the current mode. |

### Non-Functional

| ID    | Requirement |
|-------|-------------|
| NFR1  | The dark mode implementation MUST NOT add any new npm dependencies beyond what is already installed (`mode-watcher`, `tailwindcss`, `@tailwindcss/vite`). |
| NFR2  | The theme switch MUST apply instantly (< 50ms perceived) with no full-page flash (FOUC). |
| NFR3  | Dark mode colors MUST meet WCAG 2.1 AA contrast ratios (≥ 4.5:1 for normal text, ≥ 3:1 for large text). |
| NFR4  | The implementation MUST remain compatible with the existing Vite build pipeline and Native AOT backend serving of `wwwroot` static assets. |
| NFR5  | No visual regressions in light mode — the current look and feel MUST be preserved exactly. |
| NFR6  | Bundle size increase from this feature MUST be ≤ 2 KB gzipped. |

---

## 3. Technical Assumptions

**Repository Structure:** Monorepo  
**Service Architecture:** Monolith (ASP.NET Core + SPA in `frontend/`)  
**Frontend Stack:** Svelte 5, Vite 8, Tailwind CSS v4, shadcn-svelte (bits-ui), mode-watcher  
**Testing Requirements:** Frontend unit tests via Vitest (`cd frontend && npm test`); manual visual inspection in both modes.

### Additional Technical Notes
- **`mode-watcher` API**: Provides `<ModeWatcher>` component (mount once), `mode` store (reactive current mode), `toggleMode()`, and `setMode('light' | 'dark' | 'system')` functions.
- **Tailwind v4 dark mode**: Uses `@variant dark (&:where(.dark, .dark *))` — the `.dark` class on `<html>` activates all `dark:` prefixed utilities.
- **shadcn-svelte design tokens**: Components reference Tailwind CSS theme tokens (`bg-background`, `text-foreground`, `bg-card`, `bg-muted`, `text-muted-foreground`, `border-input`, `bg-primary`, `text-primary-foreground`, `bg-destructive`, `ring-ring`, etc.). These tokens are defined by the shadcn-svelte theme layer and need dark-mode values defined in `app.css`.
- The existing `app.css` `:root` custom properties (`--bg-color`, `--panel-bg`, etc.) are used only by the legacy class-based styles (`.sidebar`, `.nav-item`, `.modal-content`, etc.) — these need dark overrides too.

---

## 4. Epic List

- **Epic 5: Dark Mode Support** — Implement full dark mode theming with user toggle and preference persistence.

---

## 5. Epic 5: Dark Mode Support

**Expanded Goal:** Enable the MiniRouter dashboard to operate in light and dark color schemes, with seamless switching and OS preference detection, leveraging the existing `mode-watcher` library and Tailwind CSS v4 dark variant.

---

### Story 5.1: Initialize `mode-watcher` and Wire Theme Infrastructure

**As a** developer,  
**I want** the theme management system properly initialized at the app root,  
**so that** the `<html>` element dynamically receives `class="dark"` or `class="light"` and all downstream components react accordingly.

**Acceptance Criteria:**
1. `<ModeWatcher>` component from `mode-watcher` is mounted in `App.svelte` (or a layout wrapper).
2. On first visit, the mode defaults to the user's OS preference (`prefers-color-scheme`).
3. The `<html>` element receives `class="dark"` when dark mode is active.
4. The Sonner toast integration in `sonner.svelte` continues to work (it already reads `mode.current`).
5. No flash of unstyled/wrong-theme content (FOUC) on page load.

**Technical Notes:**
- `mode-watcher` handles `localStorage` persistence and `<html>` class injection automatically.
- Consider adding an inline script in `index.html` `<head>` to set the initial class before Svelte hydrates, preventing FOUC.

**Files:**
- `frontend/src/App.svelte`
- `frontend/index.html`

---

### Story 5.2: Define Dark Mode Color Tokens in CSS

**As a** developer,  
**I want** dark-mode color values defined for all CSS custom properties and Tailwind semantic tokens,  
**so that** the UI has a cohesive dark palette.

**Acceptance Criteria:**
1. `app.css` includes a `.dark` (or `html.dark`) selector block with overrides for all `:root` custom properties:
   - `--bg-color` → dark background (e.g., `#0a0a0b`)
   - `--panel-bg` → dark surface (e.g., `#18181b`)
   - `--text-primary` → light text (e.g., `#f4f4f5`)
   - `--text-secondary` → muted light text (e.g., `#a1a1aa`)
   - `--border-color` → subtle dark border (e.g., `#27272a`)
   - `--primary`, `--primary-hover` → adjusted for dark backgrounds
   - `--danger`, `--danger-hover`, `--success` → adjusted for contrast
2. All shadcn-svelte Tailwind semantic color tokens (`--color-background`, `--color-foreground`, `--color-card`, `--color-muted`, `--color-primary`, `--color-destructive`, etc.) have dark-mode definitions, either via Tailwind v4 `@theme` or CSS custom properties in `.dark`.
3. Hardcoded hex colors in `app.css` (`#f3f4f6` in `.btn-ghost:hover`, `.btn-toggle:hover`, `.nav-item:hover`, `.input-field:disabled`, `#eff6ff` in `.nav-item.active`) are replaced with CSS variables that flip in dark mode.
4. All colors pass WCAG AA contrast checks against their respective backgrounds.
5. Light mode appearance is unchanged.

**Proposed Dark Color Palette:**
| Token              | Light             | Dark              |
|--------------------|-------------------|-------------------|
| `--bg-color`       | `#f9fafb`         | `#09090b`         |
| `--panel-bg`       | `#ffffff`         | `#0a0a0b`         |
| `--text-primary`   | `#111827`         | `#fafafa`         |
| `--text-secondary` | `#4b5563`         | `#a1a1aa`         |
| `--border-color`   | `#e5e7eb`         | `#27272a`         |
| `--primary`        | `#2563eb`         | `#3b82f6`         |
| `--primary-hover`  | `#1d4ed8`         | `#60a5fa`         |
| `--danger`         | `#dc2626`         | `#ef4444`         |
| `--success`        | `#16a34a`         | `#22c55e`         |
| Nav active bg      | `#eff6ff`         | `#1e3a5f`         |
| Hover bg           | `#f3f4f6`         | `#27272a`         |

**Files:**
- `frontend/src/app.css`

---

### Story 5.3: Add Theme Toggle to Sidebar

**As a** user,  
**I want** a visible toggle in the sidebar to switch between Light, Dark, and System modes,  
**so that** I can choose my preferred viewing experience.

**Acceptance Criteria:**
1. A theme toggle button is placed at the bottom of the sidebar navigation area.
2. The toggle cycles through: System → Light → Dark → System (or uses a dropdown/segmented control).
3. The toggle icon updates to reflect the current mode (Sun for light, Moon for dark, Monitor for system).
4. When the sidebar is collapsed, only the icon is shown (consistent with other nav items).
5. The toggle is keyboard-accessible and includes `aria-label` describing the action.
6. Clicking the toggle applies the theme change immediately (no page reload).

**Technical Notes:**
- Use Lucide icons: `Sun`, `Moon`, `Monitor` (already using `@lucide/svelte`).
- Use `mode-watcher`'s `setMode()` or `toggleMode()` API.

**Files:**
- `frontend/src/App.svelte`

---

### Story 5.4: Audit and Fix Per-Component Dark Mode Rendering

**As a** user,  
**I want** every page and component in the dashboard to look polished in dark mode,  
**so that** there are no visual glitches, unreadable text, or jarring color mismatches.

**Acceptance Criteria:**
1. **Home.svelte**: Provider cards, badges, switch controls render correctly in dark mode.
2. **Providers.svelte**: Table rows, dialog/modal, form inputs, destructive buttons adapt properly.
3. **Models.svelte**: Table and action buttons are legible in dark mode.
4. **ApiKey.svelte**: Success banner (green bg), key display code blocks, status badges all have dark variants (some `dark:` classes already exist — verify they work).
5. **Logs.svelte**: Filter bar, `<select>` dropdowns, table rows (including error-highlighted rows), badges are all dark-mode compatible.
6. **Analytics.svelte**: Cards, stat numbers, cost data are legible on dark surfaces.
7. **Playground.svelte**: Chat bubbles (user/assistant), textarea, model selector, empty state all contrast correctly.
8. No component uses hardcoded light-only colors (e.g., `bg-white`, `text-black`, or raw hex) outside of the CSS variable system.
9. Manual visual inspection completed for all 7 pages in both modes.

**Files:**
- `frontend/src/Home.svelte`
- `frontend/src/Providers.svelte`
- `frontend/src/Models.svelte`
- `frontend/src/ApiKey.svelte`
- `frontend/src/Logs.svelte`
- `frontend/src/Analytics.svelte`
- `frontend/src/Playground.svelte`

---

### Story 5.5: Add Frontend Tests for Theme Toggle

**As a** developer,  
**I want** automated tests verifying the theme toggle behavior,  
**so that** future changes don't break theme switching.

**Acceptance Criteria:**
1. A test verifies the theme toggle button exists in the sidebar.
2. A test verifies clicking the toggle changes the mode (via `mode-watcher` API observation or DOM class assertion).
3. All existing frontend tests continue to pass (`cd frontend && npm test`).

**Files:**
- `frontend/src/App.test.js` (new or extended)

---

## 6. Checklist Results Report

### Executive Summary
| Metric                       | Value                         |
|------------------------------|-------------------------------|
| Overall PRD Completeness     | 95%                           |
| MVP Scope Appropriateness    | Just Right                    |
| Readiness for Architecture   | **Ready**                     |
| Most Critical Gap            | Exact shadcn-svelte token list needs verification at implementation time |

### Category Statuses

| Category                          | Status | Critical Issues |
|-----------------------------------|--------|-----------------|
| 1. Problem Definition & Context   | PASS   | None |
| 2. MVP Scope Definition           | PASS   | None |
| 3. User Experience Requirements   | PASS   | None |
| 4. Functional Requirements        | PASS   | None |
| 5. Non-Functional Requirements    | PASS   | None |
| 6. Epic & Story Structure         | PASS   | None |
| 7. Technical Guidance             | PASS   | None |
| 8. Cross-Functional Requirements  | PASS   | None |
| 9. Clarity & Communication        | PASS   | None |

### Top Issues by Priority
- **BLOCKERS:** None
- **HIGH:** Verify exact set of shadcn-svelte color tokens that need `.dark` overrides in Tailwind v4 format.
- **MEDIUM:** FOUC prevention script in `index.html` may need testing across browsers.
- **LOW:** Playground chat bubble contrast in dark mode may need fine-tuning.

### MVP Scope Assessment
The scope is tightly bounded: wire up an already-installed library, define color tokens, add one UI toggle, and audit existing components. No backend changes required. No new dependencies needed.

### Final Decision
**READY FOR ARCHITECT:** The PRD is structured and ready for architectural design.

---

## 7. Next Steps

### Architect Prompt
Activate `@architect` and review `docs/prd-dark-mode.md` to design the implementation approach. Key decisions:
1. Whether to define dark tokens via Tailwind v4 `@theme` in `app.css` or via `.dark` CSS custom property overrides.
2. Whether the FOUC-prevention script should be a standalone `<script>` in `index.html` or handled by `mode-watcher`'s built-in mechanism.
3. Ordering of stories — Story 5.1 and 5.2 can be parallelized; Story 5.4 depends on both.
