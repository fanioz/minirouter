// Nav state persistence (story dashboard-navigation.1.1).
//
// Stores `{ collapsed, route }` under `minirouter:nav:v1` in localStorage.
// Forward-compat: bump the version segment if the shape changes.
// Malformed values or read failures fall back to defaults.

import { DEFAULT_ROUTE, KNOWN_ROUTES } from './router.js';

export const NAV_KEY = 'minirouter:nav:v1';

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
