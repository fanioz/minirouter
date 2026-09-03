// Nav state persistence (story dashboard-navigation.1.1).
//
// Stores `{ collapsed, route }` under `minirouter:nav:v1` in localStorage.
// Forward-compat: bump the version segment if the shape changes.
// Malformed values or read failures fall back to defaults.
//
// API key persistence (Story 8.7 F-3):
// Stores the operator's API key under `minirouter:apikey:v1` for authenticated
// remote dashboard sessions.

import { DEFAULT_ROUTE, KNOWN_ROUTES } from './router.js';

export const NAV_KEY = 'minirouter:nav:v1';
export const API_KEY_STORAGE_KEY = 'minirouter:apikey:v1';

export const DEFAULTS = Object.freeze({
  collapsed: false,
  route: DEFAULT_ROUTE
});

function safeStorage() {
  try {
    if (typeof window === 'undefined') return null;
    return window.localStorage;
  } catch {
    return null;
  }
}

function sanitize(state) {
  if (!state || typeof state !== 'object') return { ...DEFAULTS };
  const collapsed = Boolean(state.collapsed);
  const routeRaw = typeof state.route === 'string' ? state.route.toLowerCase() : DEFAULT_ROUTE;
  const route = KNOWN_ROUTES.includes(routeRaw) ? routeRaw : DEFAULT_ROUTE;
  return { collapsed, route };
}

/**
 * Load saved nav state. Returns DEFAULTS on miss, parse error, or unknown
 * shape. Never throws.
 *
 * @returns {{ collapsed: boolean, route: string }}
 */
export function loadNav() {
  const ls = safeStorage();
  if (!ls) return { ...DEFAULTS };
  let raw;
  try {
    raw = ls.getItem(NAV_KEY);
  } catch {
    return { ...DEFAULTS };
  }
  if (!raw) return { ...DEFAULTS };
  try {
    return sanitize(JSON.parse(raw));
  } catch {
    return { ...DEFAULTS };
  }
}

/**
 * Persist nav state. Silent on failure (quota, disabled storage, SSR).
 *
 * @param {{ collapsed?: boolean, route?: string }} state
 */
export function saveNav(state) {
  const ls = safeStorage();
  if (!ls) return;
  try {
    ls.setItem(NAV_KEY, JSON.stringify(sanitize(state)));
  } catch {
    // ignore — best-effort
  }
}

/**
 * Load the saved API key. Returns null on miss or error. Never throws.
 *
 * @returns {string | null}
 */
export function loadApiKey() {
  const ls = safeStorage();
  if (!ls) return null;
  try {
    const key = ls.getItem(API_KEY_STORAGE_KEY);
    return key && typeof key === 'string' ? key : null;
  } catch {
    return null;
  }
}

/**
 * Save the API key. Silent on failure (quota, disabled storage, SSR).
 *
 * @param {string} key - The API key to store
 */
export function saveApiKey(key) {
  const ls = safeStorage();
  if (!ls) return;
  try {
    if (key && typeof key === 'string') {
      ls.setItem(API_KEY_STORAGE_KEY, key);
    }
  } catch {
    // ignore — best-effort
  }
}

/**
 * Clear the saved API key. Silent on failure.
 */
export function clearApiKey() {
  const ls = safeStorage();
  if (!ls) return;
  try {
    ls.removeItem(API_KEY_STORAGE_KEY);
  } catch {
    // ignore — best-effort
  }
}
