import '@testing-library/jest-dom';

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
