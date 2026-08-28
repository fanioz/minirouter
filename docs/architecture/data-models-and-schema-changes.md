# Data Models and Schema Changes

## Schema Integration Strategy
**Database Changes Required:**
- **New Tables:** None
- **Modified Tables:** None
- **New Indexes:** None
- **Migration Strategy:** The UI will purely act as a consumer of the existing `providers.json` via the API.

**Backward Compatibility:**
- No changes to existing JSON schema.
- Native AOT serialization contexts in .NET will remain untouched.
