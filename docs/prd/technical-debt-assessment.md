# Technical Debt Assessment (Final)

## 1. Executive Summary
This document outlines the final assessment of technical debt in the MiniRouter project. As a minimal ASP.NET Core Native AOT application, the primary goal is maintaining a low memory footprint and high performance. The identified debts have been prioritized, and clear architectural decisions have been made to resolve them.

## 2. Quantitative Non-Functional Requirements (NFRs)
To ensure the Native AOT footprint benefits are maintained, the following NFRs must be strictly enforced:
- **Idle Memory Usage**: < 50 MB (Linux x64/macOS ARM64)
- **Peak Memory Usage**: < 150 MB under heavy load
- **Internal Latency Overhead**: < 5ms added by the proxy per request
- **Zero Native AOT Trimming Warnings**: All JSON serialization must remain source-generated.

## 3. Consolidated Technical Debt & Architectural Decisions

### 3.1. In-Memory Body Buffering (Memory & Performance)
- **Description**: The proxy endpoint buffers the entire request body into memory (`StreamReader.ReadToEndAsync`) to inspect the requested `model`. Large payloads cause high memory allocation.
- **Architectural Decision**: **Must Fix.** Implement chunked parsing using `System.IO.Pipelines` (`PipeReader`) or a buffered `Utf8JsonReader` that reads only enough of the request body to extract the `"model"` property, then streams the rest untouched.
- **Priority**: 1 (Critical for maintaining memory NFRs)

### 3.2. Global Round-Robin Counter (Correctness)
- **Description**: The `_currentIndex` is shared globally. Requests for model A will advance the counter, potentially causing the next request for model B to skip an available provider.
- **Architectural Decision**: **Must Fix.** Convert the global counter to a per-model counter using a thread-safe collection (e.g., `ConcurrentDictionary<string, int>`).
- **Priority**: 2 (Critical for correct proxy routing)

### 3.3. File-Based Persistence Concurrency (Scalability)
- **Description**: `ProviderService` uses a coarse-grained `lock` and synchronous file I/O for `providers.json`, blocking threads during CRUD operations.
- **Architectural Decision**: **Maintain File-Based Persistence, but optimize.** A real database is overkill for this minimal app. Instead, upgrade to `ReaderWriterLockSlim` for concurrent reads, and use asynchronous I/O (`File.WriteAllTextAsync`) within an exclusive write lock.
- **Priority**: 3 (Important for stability)

### 3.4. Endpoint Logic Coupling (Maintainability)
- **Description**: Proxy routing logic is a massive inline anonymous function in `Program.cs`.
- **Architectural Decision**: **Refactor.** Extract the proxy logic into a dedicated middleware or a separate service (`ProxyService.cs`) to enable isolated unit testing.
- **Priority**: 4 (Important for future maintenance)

## 4. No Database or UX Debt
- **Database**: The JSON file approach is confirmed as the intended design. No relational database debt exists.
- **Frontend**: The system is a backend-only API. Zero UX technical debt.

## 5. Order of Resolution & Testing Strategy

**Phase 1: Memory & Correctness (Priorities 1 & 2)**
1. **Fix Body Buffering**: Implement streaming JSON inspection.
   - *Test Strategy*: Run `memory-measure.sh` with large payloads to confirm memory stays < 150MB.
2. **Fix Round-Robin Counter**: Implement per-model counters.
   - *Test Strategy*: Introduce unit tests isolating `ProviderService` to verify sequential selection per model.

**Phase 2: Stability & Refactoring (Priorities 3 & 4)**
3. **Optimize File Persistence**: Implement `ReaderWriterLockSlim` and async I/O.
   - *Test Strategy*: Run concurrent `load-test.sh` simulating frequent reads mixed with occasional writes.
4. **Decouple Proxy Logic**: Extract from `Program.cs`.
   - *Test Strategy*: Ensure all endpoints function identically via existing Postman/curl validation scripts.
