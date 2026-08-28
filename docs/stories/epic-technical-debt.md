# Epic: Technical Debt Resolution - MiniRouter

## Objective
Resolve critical and important architectural bottlenecks identified in MiniRouter to maintain low memory consumption, high performance, correctness, and stability under heavy load, without increasing infrastructure costs.

## Scope
This epic covers the resolution of 4 identified technical debts:
1. Excessive proxy memory allocation due to "In-Memory Body Buffering".
2. Correctness flaw due to a "Global Round-Robin Counter".
3. Scalability issue due to "File-Based Persistence Concurrency".
4. Maintenance difficulty due to "Endpoint Logic Coupling".

*There are no database or UX debts in this scope.*

## Success Criteria
- Idle memory usage < 50 MB (Linux x64/macOS ARM64).
- Peak memory usage < 150 MB under heavy load.
- Zero "Native AOT Trimming Warnings" added (JSON serialization must remain source-generated).
- Internal latency overhead < 5ms added by proxy per request.
- Correct sequential routing per model without skipping available providers.
- Configuration reading and writing stable and lock-free under concurrency.
- Proxy logic separated, clean, and covered by tests.

## Timeline
**Phase 1: Quick Wins & Criticals (1 week)**
- Fix high memory consumption.
- Adjustments in routing logic.

**Phase 2: Foundation and Optimization (1 week)**
- File persistence restructuring.
- Decoupling and refactoring of main proxy logic.

## Budget
Approved $3,600 (Total estimate: 24 hours).

## Stories
- [ ] [Story 1.1: Fix In-Memory Body Buffering](story-1.1-fix-body-buffering.md) (Priority 1)
- [x] [Story 1.2: Fix Global Round-Robin Counter](story-1.2-fix-round-robin-counter.md) (Priority 2)
- [x] [Story 1.3: Optimize File-Based Persistence Concurrency](story-1.3-optimize-file-persistence.md) (Priority 3) — closed by Epic 8, Story 8.2 (2026-08-11)
- [x] [Story 1.4: Decouple Proxy Logic](story-1.4-decouple-proxy-logic.md) (Priority 4)

## Baseline Correction
The prior checklist marked Stories 1.1 and 1.3 complete, but the corresponding changes are not
present in the current codebase. Their status is corrected above; this epic remains historical
planning context rather than evidence that those implementations landed.
