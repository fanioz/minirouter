// API key store for authenticated API calls.
//
// Stores the current API key used for X-Api-Key header in requests.
// Default is empty string (no authentication required for local development).

import { writable } from 'svelte/store';

function createApiKeyStore() {
  const defaultValue = '';
  const storage = typeof window !== 'undefined' ? localStorage : null;

  const saved = storage?.getItem('minirouter:apikey');
  const initial = saved !== null ? saved : defaultValue;

  const { subscribe, set, update } = writable(initial);

  return {
    subscribe,
    set: (value) => {
      if (storage) {
        try {
          storage.setItem('minirouter:apikey', value);
        } catch {
          // Ignore storage errors
        }
      }
      set(value);
    },
    clear: () => {
      if (storage) {
        try {
          storage.removeItem('minirouter:apikey');
        } catch {
          // Ignore storage errors
        }
      }
      set(defaultValue);
    }
  };
}

export const apiKeyStore = createApiKeyStore();