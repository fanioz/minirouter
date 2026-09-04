# Epic 5 Retrospective: Dark Mode Support

## Overview
This retrospective covers the completion of Epic 5, which implemented a fully functional dark mode for the MiniRouter frontend dashboard, complete with a user-accessible toggle and preference persistence.

## Validation Against PRD Goals
- **Goal:** Implement a fully functional dark mode.
  - **Status:** **Met**. The dashboard now fully supports dark mode, covering all UI components, pages, and custom styles without visual regressions.
- **Goal:** Provide a user-accessible toggle (Light/Dark/System).
  - **Status:** **Met**. A theme toggle has been added to the sidebar allowing users to cycle between modes seamlessly.
- **Goal:** Persist user's theme choice.
  - **Status:** **Met**. Utilized `mode-watcher` to persist the preference in `localStorage`.
- **Goal:** No new dependencies beyond existing ones.
  - **Status:** **Met**. Only `mode-watcher` and Tailwind CSS v4 `dark:` variants were used.

## Story Completion Summary
- **Story 5.1: Initialize `mode-watcher`**: Properly initialized at the app root, handling `<html class="dark">` injection and preventing FOUC.
- **Story 5.2: Define Dark Mode Color Tokens**: Implemented a two-layer CSS strategy in `app.css` using custom properties and Tailwind `@theme` tokens, mapping them perfectly without touching individual `.svelte` components.
- **Story 5.3: Add Theme Toggle to Sidebar**: Integrated the Lucide icons (Sun/Moon/Monitor) for a clean UI toggle.
- **Story 5.4: Audit Per-Component Rendering**: Component audit was successful; the CSS-variable mapping covered all components effortlessly.
- **Story 5.5: Add Frontend Tests**: Vitest assertions were added to `App.test.js` to ensure the toggle maintains its intended behavior without regressions.

## Key Findings and Learnings
- **CSS Architecture Efficiency**: Leveraging CSS custom properties (`--bg-color`, etc.) inside a `.dark` block and mapping them to Tailwind `@theme inline` variables allowed for a sweeping application of dark mode without modifying individual UI components.
- **FOUC Prevention**: Applying a blocking script in `index.html` proved essential for providing a high-quality user experience without jarring flashes on initial load.
- **Testing**: Using a `matchMedia` polyfill in the test setup was necessary to accommodate components like `svelte-sonner` that depend on window sizing and media queries during jsdom tests.

## Improvements & Next Steps
- Consider expanding the theme system to support custom primary colors (e.g., violet, green) alongside the light/dark modes.
- Monitor user feedback regarding the legibility of specific data-dense tables (like the Logs view) under various monitor contrast settings.
