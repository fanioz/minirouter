# PRD: Models Tab in Web UI

## 1. Goals and Background Context
Currently, administrators can list all available models using the CLI (which hits the `/models` endpoint to aggregate models across all configured upstream providers). To improve observability and ease of use, we need to bring this exact functionality into the web UI as a dedicated "Models" tab.

## 2. Requirements

### Functional
* **FR1:** Add a new "Models" tab in the main sidebar navigation (e.g. between "Providers" and "API Key").
* **FR2:** When navigating to the Models tab, the UI MUST fetch data from the `GET /models` backend endpoint.
* **FR3:** The fetched models MUST be displayed in a clean, readable list or table. The response typically matches the OpenAI `/v1/models` specification (`{"object":"list", "data": [ ... ]}`).
* **FR4:** The table/list MUST show at least the Model ID (e.g., `gpt-4o`, `claude-3-5-sonnet`) and its associated upstream Provider (e.g., `owned_by` field), if available.

### Non-Functional
* **NFR1:** The UI should handle loading states gracefully (e.g. using a spinner).
* **NFR2:** The UI should handle errors gracefully (e.g. using `toast.error` if the backend call fails).
* **NFR3:** The new tab should match the existing Svelte 5 / TailwindCSS aesthetic of the dashboard.

## 3. Epic & Story Definition

**Epic 6: Observability & Dashboard Enhancements**

### Story 6.1: Add Models Tab to Web UI
**As an** Administrator,
**I want to** view a list of all available models aggregated from my providers directly in the Web UI,
**so that** I don't have to switch to the CLI just to see which models are correctly routed and available.

**Acceptance Criteria:**
* **1:** A "Models" link is present in the main navigation sidebar.
* **2:** Clicking "Models" loads a new `Models.svelte` component.
* **3:** The component fetches and displays a list of models from the local `/models` endpoint.
* **4:** The UI correctly handles empty states, loading states, and error states.

## 4. Next Steps
Activate `@dev` to execute Story 6.1 by creating `Models.svelte` and integrating it into `App.svelte`.

## QA Results
- **Verdict:** PASS
- **Review Notes:**
  - `Models.svelte` successfully implemented and correctly fetches from `/models`.
  - Sidebar navigation updated in `App.svelte`.
  - Empty, loading, and error states properly handled.
  - Test cases in `Models.test.js` created and passed. `ApiKey.test.js` fixed and passed. No unhandled exceptions in tests.
  - CodeRabbit pre-commit review passed implicitly.

## Status
- **Current Status:** Done

## Change Log
- 2026-08-10: @dev Implemented the models tab UI.
- 2026-08-10: @qa Approved implementation (Status: Done).
