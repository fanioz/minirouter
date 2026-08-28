# Architecture: Epic 5 Dark Mode Support

## Technical Approach

### 1. The `mode-watcher` Integration
- **`mode-watcher`** handles the complex logic of system preference detection, `localStorage` persistence, listening for OS theme changes, and injecting the `dark` or `light` class into the `<html>` element.
- It provides a `<ModeWatcher>` component that must be mounted once at the root (`App.svelte`).
- To prevent FOUC, `mode-watcher` typically requires a blocking script in `index.html` to read `localStorage` and set the `<html>` class before the Svelte app hydrates.

### 2. Tailwind CSS v4 `dark:` Variant
- Tailwind v4 uses the CSS `@variant dark (&:where(.dark, .dark *))` by default.
- Once `<html class="dark">` is set by `mode-watcher`, any Tailwind class prefixed with `dark:` (e.g., `dark:bg-slate-900`) becomes active.

### 3. Layered CSS Architecture

We will implement a two-layer CSS strategy in `app.css` to bridge our custom styles and shadcn-svelte components.

**Layer 1: The Core Custom Properties (`:root` vs `.dark`)**
```css
:root {
  --bg-color: #f9fafb;
  --panel-bg: #ffffff;
  --text-primary: #111827;
  --text-secondary: #4b5563;
  --border-color: #e5e7eb;
  --primary: #2563eb;
  --primary-hover: #1d4ed8;
  --danger: #dc2626;
  --danger-hover: #b91c1c;
  --success: #16a34a;
  --hover-bg: #f3f4f6;
  --active-bg: #eff6ff;
}

.dark {
  --bg-color: #09090b;
  --panel-bg: #0a0a0b;
  --text-primary: #fafafa;
  --text-secondary: #a1a1aa;
  --border-color: #27272a;
  --primary: #3b82f6;
  --primary-hover: #60a5fa;
  --danger: #ef4444;
  --danger-hover: #f87171;
  --success: #22c55e;
  --hover-bg: #27272a;
  --active-bg: #1e3a5f;
}
```

**Layer 2: shadcn-svelte / Tailwind v4 tokens**
```css
@theme inline {
  --color-background: var(--bg-color);
  --color-foreground: var(--text-primary);
  --color-card: var(--panel-bg);
  --color-card-foreground: var(--text-primary);
  --color-popover: var(--panel-bg);
  --color-popover-foreground: var(--text-primary);
  --color-primary: var(--primary);
  --color-primary-foreground: #ffffff;
  --color-muted: var(--hover-bg);
  --color-muted-foreground: var(--text-secondary);
  --color-accent: var(--hover-bg);
  --color-accent-foreground: var(--text-primary);
  --color-destructive: var(--danger);
  --color-border: var(--border-color);
  --color-input: var(--border-color);
  --color-ring: var(--primary);
}
```

### 4. Per-Component Fixes
The following hardcoded light-only colors in `app.css` need to reference variables:
- `.btn-ghost:hover` → `var(--hover-bg)`
- `.btn-toggle:hover` → `var(--hover-bg)`
- `.nav-item:hover` → `var(--hover-bg)`
- `.nav-item.active` → `var(--active-bg)`
- `.input-field:disabled` → `var(--hover-bg)`

## Implementation Plan

1. **Story 5.1 (Initialize mode-watcher)**: `App.svelte` and `index.html` changes to inject `<ModeWatcher>` and FOUC prevention script.
2. **Story 5.2 (Define dark color tokens)**: `app.css` modifications for `.dark` and `@theme inline` mapping.
3. **Story 5.3 (Add theme toggle)**: Add toggle logic and UI using Lucide icons in `App.svelte` sidebar.
4. **Story 5.4 (Component audit)**: Ensure all Svelte pages render properly with the new classes. No code changes expected for most.
5. **Story 5.5 (Tests)**: Add Vitest assertions for theme toggle in `App.test.js`.
