# Epic 5: Dark Mode Support

**Expanded Goal:** Enable the MiniRouter dashboard to operate in light and dark color schemes, with seamless switching and OS preference detection, leveraging the existing `mode-watcher` library and Tailwind CSS v4 dark variant.

## Story 5.1: Initialize `mode-watcher` and Wire Theme Infrastructure

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

## Story 5.2: Define Dark Mode Color Tokens in CSS

**As a** developer,  
**I want** dark-mode color values defined for all CSS custom properties and Tailwind semantic tokens,  
**so that** the UI has a cohesive dark palette.

**Acceptance Criteria:**
1. `app.css` includes a `.dark` (or `html.dark`) selector block with overrides for all `:root` custom properties.
2. All shadcn-svelte Tailwind semantic color tokens have dark-mode definitions.
3. Hardcoded hex colors in `app.css` are replaced with CSS variables that flip in dark mode.
4. All colors pass WCAG AA contrast checks against their respective backgrounds.
5. Light mode appearance is unchanged.

**Files:**
- `frontend/src/app.css`

## Story 5.3: Add Theme Toggle to Sidebar

**As a** user,  
**I want** a visible toggle in the sidebar to switch between Light, Dark, and System modes,  
**so that** I can choose my preferred viewing experience.

**Acceptance Criteria:**
1. A theme toggle button is placed at the bottom of the sidebar navigation area.
2. The toggle cycles through: System → Light → Dark → System (or uses a dropdown/segmented control).
3. The toggle icon updates to reflect the current mode (Sun for light, Moon for dark, Monitor for system).
4. When the sidebar is collapsed, only the icon is shown.
5. The toggle is keyboard-accessible and includes `aria-label`.
6. Clicking the toggle applies the theme change immediately.

**Files:**
- `frontend/src/App.svelte`

## Story 5.4: Audit and Fix Per-Component Dark Mode Rendering

**As a** user,  
**I want** every page and component in the dashboard to look polished in dark mode,  
**so that** there are no visual glitches, unreadable text, or jarring color mismatches.

**Acceptance Criteria:**
1. **Home.svelte**: Provider cards, badges, switch controls render correctly in dark mode.
2. **Providers.svelte**: Table rows, dialog/modal, form inputs, destructive buttons adapt properly.
3. **Models.svelte**: Table and action buttons are legible in dark mode.
4. **ApiKey.svelte**: Success banner (green bg), key display code blocks, status badges all have dark variants.
5. **Logs.svelte**: Filter bar, `<select>` dropdowns, table rows, badges are dark-mode compatible.
6. **Analytics.svelte**: Cards, stat numbers, cost data are legible.
7. **Playground.svelte**: Chat bubbles, textarea, model selector, empty state all contrast correctly.
8. No component uses hardcoded light-only colors outside of CSS variable system.
9. Manual visual inspection completed for all 7 pages.

**Files:**
- `frontend/src/Home.svelte`, `Providers.svelte`, `Models.svelte`, `ApiKey.svelte`, `Logs.svelte`, `Analytics.svelte`, `Playground.svelte`

## Story 5.5: Add Frontend Tests for Theme Toggle

**As a** developer,  
**I want** automated tests verifying the theme toggle behavior,  
**so that** future changes don't break theme switching.

**Acceptance Criteria:**
1. A test verifies the theme toggle button exists in the sidebar.
2. A test verifies clicking the toggle changes the mode.
3. All existing frontend tests continue to pass (`cd frontend && npm test`).

**Files:**
- `frontend/src/App.test.js`
