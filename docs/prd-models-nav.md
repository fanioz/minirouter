# Models Navigation PRD

## 1. Goals and Background Context

### Goals
* Add a "Models" link to the main navigation menu.
* Improve discoverability and user access to the Models view/management page.

### Background Context
Currently, users of the MiniRouter application need to manage or view their AI models, but there lacks a direct and prominent link in the primary navigation menu. By adding a "Models" item to the nav link, we will streamline the user experience, making model management much more accessible and consistent with standard navigation patterns.

### Change Log
| Date | Version | Description | Author |
|---|---|---|---|
| 2026-08-12 | 1.0 | Initial Draft | Morgan (PM) |

## 2. Requirements

### Functional
* **FR1:** The main navigation menu MUST include a new link titled "Models".
* **FR2:** Clicking the "Models" link MUST navigate the user to the models overview/management page.
* **FR3:** The "Models" link MUST visually indicate an active/selected state when the user is on the models page or any of its sub-pages.
* **FR4:** The "Models" link MUST be visible to all authenticated users.
* **FR5:** The Models view MUST fetch and display the list of available models.
* **FR6:** The UI table displaying the models MUST exactly mirror the columns, data structure, and formatting of the "CLI models list" output.

### Non Functional
* **NFR1 (UI/UX Consistency):** The new link MUST perfectly match the existing typography, spacing, hover effects, and active state styling of the current navigation menu.
* **NFR2 (Responsiveness):** The new link MUST integrate seamlessly with the responsive design, appearing correctly in desktop view and collapsing appropriately into the mobile menu.

## 3. User Interface Design Goals

* **Overall UX Vision:** A seamless, native-feeling integration. The new "Models" link should feel as though it has always been part of the application's main navigation.
* **Key Interaction Paradigms:** Standard click-to-navigate web behavior, immediate visual feedback on hover, and clear active state highlighting.
* **Core Screens and Views:** Main Application Layout (Header/Sidebar).
* **Accessibility:** WCAG AA (Keyboard navigable, screen reader accessible, proper contrast).
* **Branding:** Strictly adhere to the existing application branding and Tailwind CSS tokens.
* **Target Device and Platforms:** Web Responsive (Desktop, Tablet, Mobile).

## 4. Technical Assumptions

* **Repository Structure:** Monorepo (backend and frontend together).
* **Service Architecture:** API-Driven Monolith (Backend) + SPA (Frontend). The frontend is a Svelte 5 application. Navigation is managed via state within `App.svelte`.
* **Testing Requirements:** Frontend Unit Testing using `vitest` and `@testing-library/svelte`.
* **Additional Assumptions:**
  * Native AOT compliance applies to the backend APIs.
  * The frontend uses `bits-ui` and Tailwind CSS, meaning active/hover states will use Tailwind utility classes.

## 5. Epic List

* **Epic 1: Models Management UI:** Expose the Models management view directly in the primary navigation sidebar and ensure the data presented matches the exact fidelity of the CLI models list.

## 6. Epic Details

### Epic 1: Models Management UI
**Expanded Goal:** Expose the Models management view directly in the primary navigation sidebar of the Svelte frontend. This ensures users can rapidly switch contexts to manage their models without friction, utilizing the existing SPA tab state.

#### Story 1.1: Add "Models" link to Svelte Navigation Sidebar
*As a user, I want to click a "Models" link in the main navigation sidebar, so that I can easily access and manage the Models view.*

**Acceptance Criteria:**
1. When a user opens the application, they see a "Models" link clearly visible in the primary navigation sidebar alongside existing items like "Home" and "Providers".
2. The "Models" link displays a standard icon (e.g., a box) that matches the visual style and dimensions of the other navigation items.
3. When the user clicks the "Models" link, the main content area immediately switches to display the Models management view.
4. When the user is viewing the Models page, the "Models" navigation link is visually highlighted (active state) to indicate their current location in the application.

#### Story 1.2: Align Models UI List with CLI Format
*As a user, I want the web UI to display the models list with the exact same columns and information as the CLI, so that I have a consistent and complete view of my models across all interfaces.*

**Acceptance Criteria:**
1. The `Models.svelte` component is updated to include the following columns in the data table: `Provider`, `Model_ID`, and `Name`.
2. The UI table includes a `[Copy ID]` action column that copies the respective Model ID to the user's clipboard when clicked, showing a brief success indicator (e.g., a toast notification).
3. The UI table includes a `[Test]` action column. When clicked, the frontend sends a test request (via the backend or directly) to the provider using that specific model with a simple hardcoded prompt (e.g., "say hello in 7 words") and displays the resulting response or error to the user.
