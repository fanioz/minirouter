# Story 1.4: Decouple Proxy Logic

## Description
The proxy routing logic is currently a massive inline anonymous function in `Program.cs`. This should be extracted into a dedicated middleware or a separate service (`ProxyService.cs`) to enable isolated unit testing and improve maintainability.

## Priority
4 (Important for future maintenance)

## Tasks
- [x] Create a new class/service `ProxyService.cs` or custom middleware.
- [x] Move the inline proxy routing logic from `Program.cs` into the new class.
- [x] Ensure dependencies are properly injected into the new service.
- [x] Register the new service/middleware in `Program.cs`.

## Acceptance Criteria
- `Program.cs` is clean and declarative.
- Proxy logic resides in its own isolated class.
- Existing routing functionality works exactly as before.

## Required Tests (QA)
- Ensure all endpoints function identically via existing Postman/curl validation scripts.
- Write a basic unit test to verify isolated logic if applicable.

## Validated Estimates
6 hours

## Definition of Done
- Code implemented and reviewed.
- Existing validation scripts pass successfully.
- Code style and architecture guidelines followed.

## Status
Ready for Review

## Dev Agent Record

### File List
- `Program.cs` (Modified)
- `Services/IProxyService.cs` (New)
- `Services/ProxyService.cs` (New)
- `Services/ProviderService.cs` (Modified - Thread safety fix)

### Change Log
- Extracted inline proxy routing from `Program.cs` into `ProxyService`.
- Registered `IProxyService` as a Singleton in the DI container.
- Updated `Program.cs` endpoints to use the extracted `ProxyService`.
- Fixed an existing thread-safety bug in `ProviderService.GetNextProviderAsync` to ensure tests consistently pass.
