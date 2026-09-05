import '@testing-library/jest-dom';
import { vi } from 'vitest';

// Default global fetch stub: components that call fetch('/api/...') on mount
// must never hit a real network in tests (Node's fetch rejects relative URLs
// with ERR_INVALID_URL). Returns an empty JSON array, which is safe for both
// list consumers ({#each}) and object consumers (property access yields
// undefined). Tests that assign their own `global.fetch = vi.fn(...)` simply
// override this default.
vi.stubGlobal('fetch', vi.fn(() =>
  Promise.resolve(new Response('[]', {
    status: 200,
    headers: { 'content-type': 'application/json' }
  }))
));

// Polyfill matchMedia for jsdom (used by svelte-sonner Toaster)
Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: (query) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: () => {},
    removeListener: () => {},
    addEventListener: () => {},
    removeEventListener: () => {},
    dispatchEvent: () => {},
  }),
});

// jsdom does not provide localStorage by default in vitest@4 — install an
// in-memory shim so router/persist code paths (story dashboard-navigation.1.1)
// can read/write the nav state without throwing.
if (typeof window !== 'undefined' && !window.localStorage) {
  const store = new Map();
  const memoryStorage = {
    getItem(key) { return store.has(key) ? store.get(key) : null; },
    setItem(key, value) { store.set(String(key), String(value)); },
    removeItem(key) { store.delete(key); },
    clear() { store.clear(); },
    key(i) { return Array.from(store.keys())[i] ?? null; },
    get length() { return store.size; },
  };
  Object.defineProperty(window, 'localStorage', { value: memoryStorage, writable: true });
}
