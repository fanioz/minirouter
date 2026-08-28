# PRD: API Key Copy Button

## 1. Goals and Background Context
Users need a convenient way to copy API keys to their clipboard. While the creation screen displays the full plaintext key with a copy button exactly once, users might also want to quickly copy the Key ID (or Prefix) from the main API Keys list table to reference in configuration files or share with team members.

## 2. Requirements

### Functional
* **FR1:** The API Key list table MUST include a quick "Copy" action (icon) for each API Key entry.
* **FR2:** Clicking the copy icon MUST copy the API Key ID (e.g. `ak_abcd1234...`) or Prefix to the user's clipboard.
* **FR3:** The UI MUST provide visual feedback (e.g., a checkmark icon or toast notification) when the copy action is successful.

### Non-Functional
* **NFR1:** The copy functionality must use the native browser Clipboard API (`navigator.clipboard`).

## 3. Epic & Story Definition

**Epic 5: Security & API Key Management**

### Story 5.2: Add Copy Action to API Key List
**As an** Administrator,
**I want to** click a copy button next to an API Key in the list,
**so that** I can quickly copy its identifier without highlighting the text manually.

**Acceptance Criteria:**
* **1:** A "Copy" icon button is visible on each row of the API Keys table.
* **2:** Clicking it copies the Key ID to the clipboard.
* **3:** A brief "Copied!" toast or icon change confirms the action.

## 4. Next Steps
Activate `@dev` to execute Story 5.2 by adding the copy button to the `ApiKey.svelte` table layout.
