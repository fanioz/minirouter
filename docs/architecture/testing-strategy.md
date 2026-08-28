# Testing Strategy

## Integration with Existing Tests
**Existing Test Framework:** xUnit for backend.
**Test Organization:** `Tests/` directory.
**Coverage Requirements:** Existing backend coverage must be maintained.

## New Testing Requirements

### Unit Tests for New Components
- **Framework:** Vitest / React Testing Library.
- **Location:** `frontend/src/__tests__/`.
- **Coverage Target:** Core component rendering and form validation.
- **Integration with Existing:** Completely isolated from backend xUnit tests.

### Integration Tests
- **Scope:** API interaction.
- **Existing System Verification:** E2E tests (e.g., Playwright) can run against a locally running MiniRouter backend.
