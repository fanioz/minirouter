# MininRouter Brownfield Enhancement PRD

## Intro Project Analysis and Context

### Existing Project Overview
**Analysis Source**: Document-project output available at `docs/brownfield-architecture.md`
**Current Project State**: MininRouter acts as an intelligent proxy and router for LLM API providers (OpenAI, Anthropic). It is built with a .NET 10 Native AOT backend and a Svelte 5 + Tailwind CSS frontend.

### Available Documentation Analysis
Using existing project analysis from document-project output (`docs/brownfield-architecture.md`).

### Enhancement Scope Definition
**Enhancement Type**: UI/UX Overhaul
**Enhancement Description**: Overhaul the existing frontend UI to implement a modern, futuristic dark mode aesthetic.
**Impact Assessment**: Moderate Impact (some existing code changes, restricted entirely to the frontend styling).

### Goals and Background Context
**Goals**:
* Implement a global dark mode toggle.
* Persist user theme preference.
* Apply a futuristic design language to all UI components.

**Background Context**:
The user desires a more modern, dark, and futuristic interface. The current system uses Svelte 5 and Tailwind CSS. The goal is to reskin the application without altering the underlying backend routing logic or SQLite database structures.

## Requirements

### Functional
* **FR1**: Implement a global theme toggle (Dark/Light/System) accessible from the main application layout.
* **FR2**: Persist the user's theme preference across sessions using local storage (leveraging the existing `mode-watcher` library).
* **FR3**: Apply a "futuristic" design language (e.g., neon accents, high-contrast dark backgrounds, glassmorphism effects) to all existing UI components.
* **FR4**: Ensure data-dense views (like the Providers list and Logs) remain highly legible in the new futuristic dark mode.

### Non Functional
* **NFR1**: The dark mode implementation must exclusively utilize the existing Tailwind CSS configuration to prevent CSS bloat.
* **NFR2**: The new design must not negatively impact the current frontend build times or the Native AOT integration.
* **NFR3**: Theme switching must be instant and flicker-free on initial load.

### Compatibility Requirements
* **CR1**: Existing Frontend Component Compatibility (all existing `bits-ui` and `lucide-svelte` components must be appropriately themed).
* **CR2**: Backend API Independence: The UI overhaul must require zero changes to the existing C# Minimal APIs or SQLite database schema.
* **CR3**: Routing Stability: The existing Svelte 5 SPA routing mechanics must remain entirely unaffected.

## User Interface Enhancement Goals

### Integration with Existing UI
The new futuristic dark mode will act as a foundational layer applied globally to the application. It will leverage the existing Tailwind CSS tokens and `mode-watcher` library, extending them with futuristic design tokens (e.g., deep space backgrounds, glowing accents, sharp borders). All existing component layouts will remain identical to avoid UX confusion, with only the visual styling changing.

### Modified/New Screens and Views
* Global Layout/Shell: Addition of a theme toggle switch.
* Dashboard/Providers List: Visual overhaul.
* Settings/API Keys View: Visual overhaul.
* Logs Analytics View: Styling updates for data density legibility.

### UI Consistency Requirements
* The futuristic theme must not break the accessibility (contrast ratios, focus rings) provided by `bits-ui`.
* Font sizes and spacing must remain completely consistent with the existing application to prevent layout shifts.

## Technical Constraints and Integration Requirements

### Existing Technology Stack
**Languages**: C# (.NET 10), JavaScript/TypeScript (Svelte 5)
**Frameworks**: ASP.NET Core Minimal APIs, Svelte 5, Tailwind CSS
**Database**: SQLite
**Infrastructure**: Native AOT Compilation (Backend), Vite (Frontend build)
**External Dependencies**: `bits-ui`, `lucide-svelte`, `mode-watcher`

### Integration Approach
**Database Integration Strategy**: N/A - Pure frontend enhancement. No SQLite changes.
**API Integration Strategy**: N/A - Frontend will continue to consume existing Minimal APIs identically.
**Frontend Integration Strategy**: Expand the `tailwind.config` / CSS variables to define the new "futuristic dark mode" color palette. Update Svelte components to use these semantic Tailwind classes.
**Testing Integration Strategy**: Existing Vitest and Svelte Testing Library suites must pass without modification.

### Code Organization and Standards
**File Structure Approach**: Maintain the existing `frontend/src/` structure.
**Naming Conventions**: Continue using standard Tailwind utility classes; introduce custom semantic variables in CSS if needed.
**Coding Standards**: Conform to existing Svelte 5 runes and logic patterns.
**Documentation Standards**: Update UI patterns documentation if a style guide exists.

### Deployment and Operations
**Build Process Integration**: The frontend will continue to be built via `npm run build` hooked into the C# `MininRouter.csproj` `BeforeBuild` target.
**Deployment Strategy**: Remains a single binary deployment.
**Monitoring and Logging**: Unaffected.
**Configuration Management**: Unaffected.

### Risk Assessment and Mitigation
**Technical Risks**: The `MininRouter.csproj` synchronously blocks on the frontend build. Large CSS changes could slightly increase build time.
**Integration Risks**: Modifying global CSS might inadvertently break hardcoded styles in specific components.
**Deployment Risks**: Minimal.
**Mitigation Strategies**: Rely heavily on Tailwind's design token system rather than custom CSS overrides to ensure safe, isolated styling.

## Epic and Story Structure

**Epic Structure Decision**: Single Epic - "Futuristic Dark Mode UI Overhaul".
Rationale: The changes are entirely scoped to the frontend repository (`frontend/`) and revolve around CSS token updates, component styling, and state toggling for the theme. Splitting this into multiple epics would create unnecessary overhead.

## Epic 1: Futuristic Dark Mode UI Overhaul

**Epic Goal**: Overhaul the MininRouter frontend to a modern, futuristic dark mode while preserving all existing functionality and layout structures.

**Integration Requirements**: All changes must reside in the `frontend/` directory. Zero changes to backend Minimal APIs or SQLite schema.

### Story 1.1 Theme Configuration and Base Styling
As a user,
I want to toggle a futuristic dark mode,
so that I can use the application comfortably in dark environments.
#### Acceptance Criteria
1: Tailwind configuration is updated with futuristic color tokens.
2: A theme toggle switch is added to the global layout.
3: User preference is saved via local storage (`mode-watcher`).
#### Integration Verification
IV1: Verify the backend still serves the built Svelte app without errors.
IV2: Verify no layout shifts occur when toggling between themes.

### Story 1.2 Core Components Re-theming
As a user,
I want all interactive elements to match the futuristic theme,
so that the application feels cohesive and modern.
#### Acceptance Criteria
1: `bits-ui` and `lucide-svelte` components are updated with new color tokens.
2: Futuristic accents (glows, borders) are applied to buttons, modals, and inputs.
#### Integration Verification
IV1: Verify all form inputs successfully trigger their backend API calls.
IV2: Verify modal accessibility and focus traps remain intact.

### Story 1.3 Data Views and Layout Polish
As a user,
I want data-dense views like the Provider List and Logs to be highly legible in dark mode,
so that I can analyze data efficiently.
#### Acceptance Criteria
1: The Providers and Analytics list views are visually overhauled.
2: Tables and complex data structures maintain high contrast and legibility.
#### Integration Verification
IV1: Verify complex data loading (e.g. `AggregatedModels` caching) renders correctly without UI breakage.
IV2: Verify pagination and filtering controls still function correctly.
