# MiniRouter Product Requirements Document (PRD)

## 1. Goals and Background Context

**Goals**
* Provide an administrative interface to generate, view, edit, and revoke API Keys for the MiniRouter proxy endpoint.
* Secure the proxy endpoints (`/v1/chat/completions`) so that only clients with valid API keys can consume downstream models.

**Background Context**
Currently, the MiniRouter exposes its `POST /v1/chat/completions` proxy endpoint without robust incoming API key enforcement (or relies on basic authentication). As we deploy the proxy for broader use, we need a way to manage API keys that clients use to access the proxy. We already have an `ApiKey.svelte` tab and an `IApiKeyService` in the backend, but we need to formally document and implement the full lifecycle of API key management, including authorization middleware.

**Change Log**
| Date | Version | Description | Author |
|---|---|---|---|
| 2026-08-10 | 0.1 | Initial Draft for API Key Management | Morgan (PM) |

## 2. Requirements

### Functional
* **FR1:** The system MUST allow administrators to generate a new API Key (generating a secure token, e.g., `sk-mini-...`).
* **FR2:** The system MUST allow administrators to assign a descriptive name (e.g., "Frontend App", "Developer X") to each key.
* **FR3:** The system MUST allow administrators to revoke (delete or disable) an API Key.
* **FR4:** The `POST /v1/chat/completions` endpoint MUST require a valid `Authorization: Bearer <API-KEY>` header.
* **FR5:** If an invalid or missing API key is provided, the API MUST reject the request with a `401 Unauthorized`.
* **FR6:** The UI MUST display a list of all active API Keys, their names, and their creation/last-used dates.

### Non Functional
* **NFR1:** The actual API key tokens should be stored securely (hashed) in the SQLite database, returning the raw token only once upon creation.
* **NFR2:** Key verification must be highly performant (e.g., cached in memory) to prevent latency spikes on the proxy endpoint.
* **NFR3:** The backend implementation must remain Native AOT compliant.

## 3. Technical Assumptions

**Repository Structure:** Monorepo (Svelte 5 + .NET Minimal APIs)
**Security:** We will use standard HTTP Bearer authentication for the proxy endpoint.
**Data Storage:** `minirouter.db` (SQLite)

## 4. Epic List

* **Epic 5: Security & API Key Management:** Secure the proxy endpoint with robust authentication and provide UI tools for administrators to manage access tokens.

## 5. Epic 5: Security & API Key Management

**Expanded Goal:** Ensure that the proxy cannot be abused by unauthorized users, while providing a seamless management experience for the administrator.

### Story 5.1: Implement API Key CRUD and Authentication Middleware
**As an** Administrator,
**I want to** create, list, and revoke API keys via the dashboard, and have those keys enforced on the proxy endpoint,
**so that** I can control and monitor who is using my MiniRouter instance.

**Acceptance Criteria:**
* **1:** The `IApiKeyService` supports creating, listing, and revoking keys.
* **2:** The `ApiKey.svelte` UI implements the forms to call these CRUD endpoints.
* **3:** A custom authentication middleware is added to the `.NET` application that intercepts requests to `/v1/chat/completions`.
* **4:** The middleware validates the `Bearer` token against the database/cache.
* **5:** Invalid or missing tokens return `401 Unauthorized`.

## 7. Checklist Results Report

### Executive Summary
* **Overall PRD Completeness:** 95%
* **MVP Scope Appropriateness:** Just Right
* **Readiness for Architecture Phase:** Ready

### Category Statuses

| Category | Status | Critical Issues |
| --- | --- | --- |
| 1. Problem Definition & Context | PASS | None |
| 2. MVP Scope Definition | PASS | None |
| 3. User Experience Requirements | PASS | None |
| 4. Functional Requirements | PASS | None |
| 5. Non-Functional Requirements | PASS | None |
| 6. Epic & Story Structure | PASS | None |
| 7. Technical Guidance | PASS | None |

### Top Issues by Priority
* **HIGH:** Ensuring API key lookup latency does not impact the streaming TTFB.

### Final Decision
**READY FOR ARCHITECT:** The PRD is structured and ready for architectural design.

## 8. Next Steps

### Architect Prompt
Activate `@architect` and review `docs/prd/api-key-management.md` to design the system architecture for API Key Management. Identify how the authorization middleware will be implemented in a Native AOT compliant way, and how the Svelte 5 frontend will integrate with the existing `IApiKeyService` CRUD endpoints.
