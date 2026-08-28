# Story 1.3: Optimize File-Based Persistence Concurrency

## Description
`ProviderService` uses a coarse-grained `lock` and synchronous file I/O for `providers.json`, blocking threads during CRUD operations. We need to maintain the file-based persistence but upgrade it to `ReaderWriterLockSlim` for concurrent reads, and use asynchronous I/O (`File.WriteAllTextAsync`) within an exclusive write lock.

## Priority
3 (Important for stability)

## Tasks
- [x] Replace the standard `lock` in `ProviderService` with `ReaderWriterLockSlim`.
- [x] Refactor read methods to use read locks.
- [x] Refactor write methods to use write locks and `File.WriteAllTextAsync`.
- [x] Ensure that I/O operations are correctly awaited without causing deadlocks.

## Acceptance Criteria
- Multiple simultaneous reads are permitted without blocking.
- Writes correctly block other reads and writes during execution.
- File updates persist asynchronously.

## Required Tests (QA)
- Run concurrent `load-test.sh` simulating frequent reads mixed with occasional writes.
- Verify stability and absence of deadlocks under load.

## Validated Estimates
6 hours

## Definition of Done
- Code implemented and reviewed.
- Load tests executed and passed.
- No thread contention issues detected under normal load.

## Status
Ready for Review

## Dev Agent Record

### Agent Model Used
Gemini Experimental

### Debug Log References
No major bugs. Implemented `ReaderWriterLockSlim` for memory read/write concurrency and `SemaphoreSlim` for serializing asynchronous file writes, successfully avoiding deadlocks.

### Completion Notes List
- Replaced coarse `lock` with `ReaderWriterLockSlim` for concurrent memory reads.
- Replaced synchronous `File.WriteAllText` with `File.WriteAllTextAsync`.
- Added `SemaphoreSlim` to ensure async file I/O operations are serialized without blocking the `ReaderWriterLockSlim` thread affinity.
- Load tested successfully, allowing multiple concurrent requests.

## File List
- `Services/ProviderService.cs`

## Change Log
- Replaced `lock` with `ReaderWriterLockSlim` and `SemaphoreSlim` in `ProviderService.cs`.
- Refactored `ListProvidersAsync`, `GetProviderByIdAsync`, `GetNextProviderAsync` to use read locks.
- Refactored `CreateProviderAsync`, `UpdateProviderAsync`, `DeleteProviderAsync`, and `LoadProvidersAsync` to use write locks for memory changes and async semaphore for file changes.
