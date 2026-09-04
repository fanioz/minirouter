# Terminal UX Specification: Restart Server Command

## 1. Design Philosophy

This specification adheres to our core principle of **"Delight in Details" (Micro-interactions)** while remaining technically pragmatic for a CLI environment. The goal is to provide the user with clear, immediate feedback at every step of the process lifecycle so they never wonder if the system is hung.

## 2. Color System (Design Tokens)

We use standard ANSI colors mapped to semantic states to ensure accessibility and readability across both light and dark terminal themes.

| Token | Semantic Meaning | ANSI Color | Hex/RGB (Reference) |
|-------|------------------|------------|---------------------|
| `ui-info` | Informational steps, searches | `Cyan` | `#00FFFF` |
| `ui-wait` | Long-running processes | `Blue` | `#0000FF` |
| `ui-success`| Completion, healthy state | `Green` | `#00FF00` |
| `ui-warning`| Force kills, fallbacks | `Yellow` | `#FFFF00` |
| `ui-error` | Failures, exceptions | `Red` | `#FF0000` |

## 3. Spinner Behavior

For states that block execution (waiting for shutdown, waiting for startup), we will use an animated spinner to indicate the system is actively working.

- **Spinner Type:** Dots (`⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏`) - Requires UTF-8 terminal support. 
- **Fallback:** ASCII (`| / - \`) - If standard console encoding is not UTF-8.
- **Tick Rate:** 100ms.

## 4. User Journey & Output States

### Scenario A: Happy Path (Graceful Restart)

1. **Discovery State**
   - **Visual:** `[Cyan] 🔍 Locating active MiniRouter server...`
2. **Draining State**
   - **Visual:** `[Blue] ⠋ Requesting graceful shutdown...` (Animated)
3. **Starting State**
   - **Visual:** `[Blue] ⠋ Starting new server instance...` (Animated)
4. **Success State**
   - **Visual:** `[Green] ✔️ Server restarted successfully (New PID: 12345)`

### Scenario B: Hard Kill Path (Force Flag or Timeout)

1. **Discovery State**
   - **Visual:** `[Cyan] 🔍 Locating active MiniRouter server...`
2. **Draining State (Timeout)**
   - **Visual:** `[Blue] ⠋ Requesting graceful shutdown...` (Animated -> waits 5s)
   - *If timeout reached or `--force` used:*
   - **Visual:** `[Yellow] ⚠️ Graceful shutdown failed. Forcing termination...`
3. **Starting State**
   - **Visual:** `[Blue] ⠋ Starting new server instance...` (Animated)
4. **Success State**
   - **Visual:** `[Green] ✔️ Server forcefully restarted (New PID: 12345)`

### Scenario C: No Server Running

1. **Discovery State**
   - **Visual:** `[Cyan] 🔍 Locating active MiniRouter server...`
2. **Warning State**
   - **Visual:** `[Yellow] ℹ️ No active server found. Starting a new instance...`
3. **Starting State**
   - **Visual:** `[Blue] ⠋ Starting new server instance...` (Animated)
4. **Success State**
   - **Visual:** `[Green] ✔️ Server started (PID: 12345)`

### Scenario D: Fatal Error

1. **Error State**
   - **Visual:** `[Red] ❌ Failed to restart server: [Exception Message]`

## 5. Accessibility & Best Practices

- **Color Reliance:** Do not rely on color alone. Use explicit icons (`✔️`, `❌`, `⚠️`, `ℹ️`) prefixing the messages.
- **Idempotency:** Re-running the command rapidly should not leave orphan spinners on the screen. Always capture `Ctrl+C` (SIGINT) to clear the spinner and reset terminal cursor visibility before exiting.
