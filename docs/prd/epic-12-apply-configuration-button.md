# Epic 12: Apply Configuration Button (One-Click CLI Tool Setup)

## Epic Goal

Add a secure server-side "Apply Configuration" button to the Integrations tab that writes the correct MiniRouter provider configuration directly into Claude Code's and Codex CLI's user-level config files, eliminating manual copy-paste and reducing setup friction to a single click.

**Created by:** @pm (Morgan)  
**Date:** 2026-08-31  
**Priority:** 2 — Medium (UX polish; does not block core functionality)

---

## Background

After Epic 10 (Claude Code support) and Epic 11 (Codex CLI Responses API support), both CLI tools are functionally compatible with MiniRouter. The current Integrations tab shows working configuration snippets for both tools:

- **Claude Code:** `~/.claude/config.json` JSON snippet with `minirouter` custom provider
- **Codex CLI:** `~/.codex/config.toml` TOML snippet with `[providers.minirouter]` table

Users must manually copy these snippets, open their config files (often requiring them to navigate to hidden home directories), paste the content, merge carefully with existing keys, save, and restart the tool. This flow has several pain points:

1. **Error-prone:** Users may accidentally overwrite existing providers or malform JSON/TOML syntax.
2. **Friction:** Requires terminal navigation or Finder "Go to Folder" for hidden paths.
3. **Platform-specific:** Windows users must translate `~/.claude/` to `%USERPROFILE%\.claude\`.
4. **Inaccessible to non-technical users:** Not all MiniRouter operators are comfortable hand-editing config files.

The "Apply Configuration" button solves this by allowing the MiniRouter server to write the necessary configuration changes directly into the tool's config file — with user consent, validation, and idempotent merging to preserve existing settings.

---

## User Flow

1. **User opens Integrations tab** — sees the current per-tool config snippet cards (no change to existing UI).
2. **Each tool card shows a new "Apply Configuration" button** below the copy-to-clipboard snippet.
3. **User clicks "Apply Configuration"** (e.g., for Claude Code).
4. **Frontend sends `POST /api/config/apply-claude-code`** (authenticated) to the MiniRouter server.
5. **Backend validates:**
   - Auth check: request must include valid API key (protected by `ApiKeyEndpointFilter`).
   - Path check: target config file path is within `$HOME` (Unix) or `%USERPROFILE%` (Windows).
   - Permission check: server process user can write to the target file.
6. **Backend merges configuration idempotently:**
   - Reads existing config (if file exists).
   - Inserts/updates only the `minirouter` provider entry (Claude Code) or `[providers.minirouter]` table (Codex CLI).
   - Preserves all other keys, providers, and settings.
   - Does **not** inject API keys, secrets, or auth tokens (user still sets these separately in their tool's config).
7. **Backend responds:**
   - `200 OK` → frontend shows success toast: "✓ Configuration applied to ~/.claude/config.json. Restart Claude Code to activate."
   - `403 Forbidden` → frontend shows error toast: "⚠ Cannot write to config file: [reason]."
   - `500 Internal Server Error` → generic failure toast.
8. **User restarts their CLI tool** (Claude Code or Codex CLI) — the tool now uses MiniRouter as the provider.

---

## Security Model

### Threat Model

- **Attacker scenario 1:** Unauthenticated caller on public internet tries to write arbitrary data to server owner's home directory.
  - **Mitigation:** Apply config endpoint is protected by `ApiKeyEndpointFilter` — requires valid `X-Api-Key` header. Only users with MiniRouter access can trigger config writes.

- **Attacker scenario 2:** Authenticated user tries to write to paths outside their home directory (e.g., `/etc/`, `/var/`, system paths).
  - **Mitigation:** Backend validates that the resolved target path is within `$HOME` (or `%USERPROFILE%`). Absolute path validation with canonicalization to prevent `../` traversal.

- **Attacker scenario 3:** Malicious actor with MiniRouter API key tries to inject XSS payloads or shell commands into config files.
  - **Mitigation 1 (project-local configs rejected):** Both Claude Code and Codex CLI ignore project-local provider configs by design (documented security boundary). The apply endpoint writes only to user-level configs (`~/.claude/`, `~/.codex/`).
  - **Mitigation 2 (structured data only):** Backend writes via structured serialization (JSON/TOML libraries) — no raw string concatenation. User cannot inject executable code through this path.

- **Attacker scenario 4:** Attacker uses MiniRouter to overwrite user's API keys or delete existing provider configurations.
  - **Mitigation:** Write is idempotent and merge-only. Backend preserves all existing keys; only inserts/updates the `minirouter` provider entry. Never deletes keys or overwrites unrelated settings.

### Key Constraints

- **Server-side only:** The API runs as the MiniRouter server process user. It can only touch files that process user owns (typically the user running `dotnet run` or the systemd service user).
- **No privilege escalation:** No `sudo`, no elevated writes. If the server process doesn't have write access, the operation fails gracefully with `403`.
- **No secrets injection:** The apply endpoint does **not** set `api_key` (Claude Code) or `api_key` (Codex CLI). Users must still manually configure their tool's authentication separately.
- **No remote config:** Only writes to `$HOME/.claude/` and `$HOME/.codex/` on the server host. Does not support remote SSH writes or network mounts.

---

## Scope

### In Scope (Story 12.1)

- `POST /api/config/apply-claude-code` — writes `~/.claude/config.json` with `minirouter` provider entry
- `POST /api/config/apply-codex` — writes `~/.codex/config.toml` with `[providers.minirouter]` table
- Auth: both endpoints protected by `ApiKeyEndpointFilter`
- Path validation: canonical path must be within `$HOME` (Unix) or `%USERPROFILE%` (Windows)
- Idempotent merge logic:
  - Reads existing config (JSON for Claude, TOML for Codex)
  - Inserts or updates only the `minirouter` provider
  - Preserves all other keys and providers
  - Creates missing parent directories if needed
- Error handling:
  - `403` if path is outside `$HOME` or not writable
  - `500` if file read/write fails
  - Response body includes human-readable reason
- Frontend:
  - New `<button>` in `ClaudeCodeConfig.svelte` and `CodexConfig.svelte` (or similar components)
  - Button labeled "Apply Configuration"
  - On success: show toast "✓ Configuration applied to {path}. Restart {tool} to activate."
  - On error: show toast with reason from response body
- Tests:
  - `ConfigApplyServiceTests.cs` — unit tests for merge logic (mocked file system)
  - `ConfigApplyEndpointTests.cs` — integration tests (temp directories)

### Out of Scope (future stories or not planned)

- **Auto-restart tools:** User must manually restart Claude Code or Codex CLI after config is applied.
- **Secrets management:** No `api_key` injection; users set these separately in their tool's config.
- **Project-local configs:** Only user-level configs are written. Project-local configs are ignored by both tools per their security model.
- **Remote host config:** Only applies to the same host where MiniRouter is running. No SSH or remote write support.
- **Windows registry:** Both tools use file-based configs on Windows; no registry writes needed.
- **Config validation:** Backend does not validate the tool's config schema (e.g., whether `max_tokens` is valid). It only writes the provider entry.
- **Undo/rollback:** User can manually revert changes by editing the config file or restoring from backup. No automatic rollback.

---

## User Story

As a MiniRouter operator,  
I want to click "Apply Configuration" in the Integrations tab,  
So that the MiniRouter provider is automatically added to my CLI tool's config without manual file editing.

---

## Acceptance Criteria

1. Clicking "Apply Configuration" for Claude Code writes to `~/.claude/config.json` (or `%USERPROFILE%\.claude\config.json` on Windows).
2. Clicking "Apply Configuration" for Codex CLI writes to `~/.codex/config.toml` (or `%USERPROFILE%\.codex\config.toml` on Windows).
3. Existing providers and keys in the config file are preserved (idempotent merge).
4. If the config file doesn't exist, it is created with the `minirouter` provider as the only entry.
5. If the config file is malformed (invalid JSON/TOML), the endpoint returns `500` with a clear error message.
6. If the server process doesn't have write permission, the endpoint returns `403` with a clear error message.
7. If the target path is outside `$HOME` (Unix) or `%USERPROFILE%` (Windows), the endpoint returns `403`.
8. Both endpoints are protected by `ApiKeyEndpointFilter` (unauthenticated requests return `401`).
9. Success toast shows the target file path and instructs the user to restart their tool.
10. Error toast shows the reason for failure (e.g., "Permission denied: ~/.claude/config.json").
11. All new and existing tests pass; `dotnet build` clean; `npm run build` clean.

---

## Technical Approach

### Backend

New service: `Services/ConfigApplyService.cs`

```csharp
public class ConfigApplyService
{
    public async Task<ConfigApplyResult> ApplyClaudeCodeConfigAsync();
    public async Task<ConfigApplyResult> ApplyCodexConfigAsync();
    private string ResolveConfigPath(string toolName); // returns canonical path in $HOME
    private bool ValidatePathInHomeDirectory(string path);
    private Task<bool> ValidateWritePermissionAsync(string path);
    private Task MergeJsonConfigAsync(string path, JsonObject providerEntry);
    private Task MergeTomlConfigAsync(string path, TomlTable providerTable);
}

public class ConfigApplyResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ConfigPath { get; init; }
}
```

New endpoints in `Program.cs`:

```csharp
app.MapPost("/api/config/apply-claude-code", async (ConfigApplyService svc) =>
{
    var result = await svc.ApplyClaudeCodeConfigAsync();
    return result.Success 
        ? Results.Ok(new { path = result.ConfigPath })
        : Results.Problem(statusCode: 403, detail: result.ErrorMessage);
})
.AddEndpointFilter<ApiKeyEndpointFilter>();

app.MapPost("/api/config/apply-codex", async (ConfigApplyService svc) =>
{
    var result = await svc.ApplyCodexConfigAsync();
    return result.Success 
        ? Results.Ok(new { path = result.ConfigPath })
        : Results.Problem(statusCode: 403, detail: result.ErrorMessage);
})
.AddEndpointFilter<ApiKeyEndpointFilter>();
```

Dependencies:
- `System.Text.Json` for JSON manipulation (Claude Code config)
- `Tomlyn` NuGet package for TOML parsing/writing (Codex CLI config)

### Frontend

New components (or additions to existing integration components):

- `ClaudeCodeConfig.svelte` — add button below snippet
- `CodexConfig.svelte` — add button below snippet

Button handler:

```typescript
async function applyConfig(tool: 'claude-code' | 'codex') {
    const endpoint = tool === 'claude-code' 
        ? '/api/config/apply-claude-code' 
        : '/api/config/apply-codex';
    
    const response = await fetch(endpoint, {
        method: 'POST',
        headers: { 'X-Api-Key': apiKey } // from store
    });
    
    if (response.ok) {
        const { path } = await response.json();
        showToast(`✓ Configuration applied to ${path}. Restart ${tool} to activate.`, 'success');
    } else {
        const { detail } = await response.json();
        showToast(`⚠ ${detail}`, 'error');
    }
}
```

---

## Story Breakdown

This epic is a single story — the feature is self-contained and testable as a unit.

| Story | Title | Complexity | Dependencies |
|-------|-------|------------|--------------|
| **12.1** | Apply Configuration Button for CLI Tools | Medium | Epic 10, Epic 11 (CLI tool support must exist first) |

---

## Definition of Done (Epic)

- [ ] "Apply Configuration" button live in Integrations tab for Claude Code and Codex CLI
- [ ] Both endpoints protected by API key auth
- [ ] Idempotent merge logic preserves existing config keys
- [ ] Path validation prevents writes outside `$HOME`
- [ ] Success/error toasts provide clear feedback
- [ ] All tests pass (unit + integration)
- [ ] `dotnet build` and `npm run build` clean
- [ ] Manual smoke test: button click → config file updated → tool restart → MiniRouter routes traffic

---

## Open Questions

1. **Should the button be enabled only if the server detects the tool's config file exists?**  
   → No. The endpoint creates the file if missing. Button is always enabled (errors surface via toast).

2. **Should we show a confirmation dialog before writing to disk?**  
   → Yes (optional UX polish). Story 12.1 can include a simple browser `confirm()` modal: "This will write to ~/.claude/config.json. Continue?"

3. **What if the user is running MiniRouter in a Docker container?**  
   → Out of scope. The apply endpoint writes to the container's `$HOME`, not the host. Users mounting config volumes can still use manual copy-paste.

4. **Should we validate that the tool is actually installed before showing the button?**  
   → No. The Integrations tab shows instructions regardless of whether the tool is installed. The apply button follows the same pattern.

---

*Epic created by @pm (Morgan) · 2026-08-31*
