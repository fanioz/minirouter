# Technical Debt Consolidation (DRAFT)

## 1. Overview
This document consolidates the technical debt identified across the MiniRouter project during the discovery phases. It serves as a draft to be reviewed by experts before being integrated into a formal PRD or refactoring plan.

## 2. Backend & System Architecture Debt
*Source: `docs/architecture/system-architecture.md`*

1. **Global Round-Robin Counter**
   - **Description**: The `_currentIndex` for selecting the next provider is a single integer shared across all models. Requests for a specific model advance the global counter, which can cause requests for other models to skip providers or lead to uneven load distribution.
   - **Impact**: Unpredictable load balancing when multiple models are requested concurrently.

2. **File-Based Persistence Concurrency**
   - **Description**: The `ProviderService` uses a single coarse-grained `lock` for all CRUD operations and writes the entire provider list to `providers.json` synchronously via `File.WriteAllText`.
   - **Impact**: Poor horizontal scalability (cannot safely run multiple instances pointing to the same file) and potential performance bottlenecks under heavy administrative load.

3. **In-Memory Body Buffering**
   - **Description**: The proxy endpoint (`/v1/chat/completions`) uses `StreamReader.ReadToEndAsync` to buffer the entire request body into memory to inspect the requested `model`.
   - **Impact**: For requests with massive context windows or large base64 images, this causes high memory consumption and negates some of the Native AOT footprint benefits.

4. **Endpoint Logic Coupling**
   - **Description**: The proxy routing and request rewriting logic is implemented as a single, large anonymous function inline in `Program.cs`.
   - **Impact**: Makes the proxy logic hard to unit test and maintain.

## 3. Database Debt
*Note: Phase 2 was skipped.*
- The system operates without a traditional database (uses a flat JSON file). There are no database schemas or queries to audit.

## 4. Frontend & UX Debt
*Source: `docs/frontend/frontend-spec.md`*
- The project is a backend-only .NET API. There are no frontend components, assets, or web server configurations. **Zero UX technical debt.**

---

## 5. Expert Review Questions [NEEDS REVIEW]

Please review the consolidated technical debt above and answer the following questions:

**For the Backend/Architecture Expert:**
1. Is fixing the **Global Round-Robin Counter** a high priority for the upcoming release, or is the current behavior acceptable for the expected use cases?
2. Regarding **In-Memory Body Buffering**, should we invest in streaming JSON parsing (e.g., `Utf8JsonReader` on a memory stream chunk) to extract the model name without buffering the whole body?
3. Since the app is built for Native AOT with a focus on minimal memory footprint, does the **File-Based Persistence** need to be replaced with a real database (like SQLite), or is the coarse-grained lock acceptable given CRUD operations are infrequent?

**For the Database Expert:**
1. Please confirm that we do not need to introduce a database (e.g., SQLite or PostgreSQL) at this stage and that the current JSON file approach is acceptable.

**For the Frontend Expert:**
1. Please confirm there are no hidden UI requirements or planned frontend features that we missed during discovery.
