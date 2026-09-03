# Story 8.7 Fix Request: F-1…F-4 (QA Project Review 2026-08-29)

## Description

Fix request raised from the full-project QA review (Quinn, 2026-08-29, signal CONCERNS —
`docs/qa/qa-report-project-review-2026-08-29.md`). Story 8.7 is reopened (Done → In
Progress) because its core acceptance criterion — cost computed at log-write time and
persisted per row — is not implemented in the production path (F-1). This request also
covers three adjacent findings from the same review: broken docker-compose port mapping
(F-2), unauthenticated management plane + remote shutdown when bound non-loopback (F-3),
and a HIGH-severity NuGet dependency vulnerability (F-4). All four are in the report's
"Must fix (sebelum siklus rilis berikutnya)" list.

## Priority

1 (Must fix before next release cycle)

## Tasks

### F-1 — Story 8.7 cost persistence (MAJOR, story reopened)

- [ ] Wire `Services/PricingTable.cs:CalculateCost()` into the `Services/ProxyService.cs` success path; set `Cost`/`CostEstimated` at every `new RequestLog` site (~lines 470, 515, 633). Currently 0 production callers.
- [ ] Add `cost` and `cost_estimated` columns to the `request_log` INSERT in `Services/LogService.cs` (INSERT currently omits both; `$cost`/`$cost_estimated` parameters at ~lines 120-121 are dead bindings — wire them in or remove).
- [ ] Preserve story semantics: lookup precedence exact > pattern > provider default; unmatched model → `null` cost, never `0`.
- [ ] Propagate `cost_estimated` from Story 8.4's estimated-token flag.
- [ ] Correct the story file's Affected Files claim ("ProxyService.cs — compute cost at log time") once implementation matches reality.

### F-2 — docker-compose port mismatch (MAJOR)

- [ ] Fix `docker-compose.yml` mapping `5050:5000` → `5050:8080` (container listens on 8080 per Dockerfile `ENV ASPNETCORE_URLS=http://+:8080` + `EXPOSE 8080`).
- [ ] Verify the documented docker workflow is reachable at `host:5050`.

### F-3 — Management plane + shutdown without auth (HIGH, deployment-dependent)

- [ ] Add `ApiKeyEndpointFilter` (or admin key) to `/api/*` and `POST /_shutdown` when bound to non-loopback interfaces. `/api/providers/test` is an arbitrary-URL fetch (SSRF vector); `/api/keys` can mint API keys.
- [ ] Decide bind policy: keep Docker bind all-interfaces with auth, or default Docker bind to loopback (README markets VPS deployment — pick one and document it).
- [ ] Keep localhost dev flow working without added friction.

### F-4 — NuGet dependency vulnerability (HIGH, NU1903)

- [ ] Bump `Microsoft.Data.Sqlite` / pin `SQLitePCLRaw.lib.e_sqlite3` ≥ patched version (GHSA-2m69-gcr7-jv3q, currently 2.1.11 via Microsoft.Data.Sqlite 10.0.10).
- [ ] `dotnet build` reports zero NU1903 warnings afterwards.

## Acceptance Criteria

- [ ] A real proxied request produces a `request_log` row with non-null `cost` when the model matches the pricing table; fully unknown model → `null`, never `0`
- [ ] `/api/analytics/tokens` and `/api/analytics/keys` return non-zero `totalCost` after real traffic; null costs excluded from (not zeroed into) aggregates
- [ ] `cost_estimated` is true iff the row's tokens came from Story 8.4's estimator
- [ ] Pre-existing `request_log` rows survive migration with null cost and remain queryable
- [ ] `docker compose up` → service reachable at `localhost:5050` (`/health` 200)
- [ ] From a non-loopback interface, `/api/*` and `/_shutdown` require auth (or are unreachable per the chosen bind policy); localhost dev flow unaffected
- [ ] `dotnet build` shows 0 NU1903 warnings; dotnet + vitest suites fully green

## Required Tests (QA)

- Persistence regression test: proxy success path writes a non-null `cost` row (the exact gap QA caught in F-1)
- Lookup precedence: exact beats pattern beats provider default; unknown → null excluded from aggregates
- Migration: pre-existing rows keep null cost and stay queryable
- `decimal` precision across large accumulation (existing story test must still pass)
- Compose smoke test: container reachable at `localhost:5050`
- Auth test: `/api/*` + `/_shutdown` rejected without key on non-loopback bind
- Build gate: no NU1903; 104/104 baseline remains green

## Validated Estimates

6–8 hours (F-1 ≈ 4h, F-2 ≈ 0.5h, F-3 ≈ 2–3h, F-4 ≈ 0.5h)

## Definition of Done

- All F-1…F-4 tasks checked with evidence
- Story 8.7 acceptance criteria re-verified end-to-end in the production path
- No regressions: clean build, dotnet 84/84 + vitest 20/20, browser smoke pass
- QA re-review scheduled (`*review-build`, story-fix scope) after patch

## Status

Done (2026-09-03) — Resolved in Wave 1 via PR #14

## Resolution

All findings F-1 through F-4 have been fixed and merged:

- **F-1** (Story 8.7 cost persistence): Fixed — cost calculation wired into ProxyService success path, columns added to INSERT statement
- **F-2** (docker-compose port mismatch): Fixed — port mapping corrected to 5050:8080
- **F-3** (Management plane auth): Fixed — ApiKeyEndpointFilter added to management endpoints
- **F-4** (NuGet dependency vulnerability): Fixed — Microsoft.Data.Sqlite bumped to resolve NU1903

**Merged**: https://github.com/fanioz/minirouter/pull/14

## References

- QA report: `docs/qa/qa-report-project-review-2026-08-29.md` (findings F-1…F-4, Recommendations 1–4)
- Reopened story: `docs/stories/8.7-cost-calculation.story.md`
- Format reference: `docs/stories/story-1.1-fix-body-buffering.md`
