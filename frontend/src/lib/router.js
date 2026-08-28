// Hash router for MiniRouter dashboard.
//
// Story dashboard-navigation.1.1: ~30 LOC, no new dependency.
// Maps `#/<segment>` -> route id. Unknown segments fall back to 'home'.
// Exposes a Svelte readable store so components can subscribe.

import { readable } from 'svelte/store';

export const DEFAULT_ROUTE = 'home';

export const KNOWN_ROUTES = [
  'home',
  'providers',
  'presets',
  'models',
  'playground',
  'analytics',
  'logs',
  'apikey'
];

/**
 * Parse `window.location.hash` (e.g. "#/providers") into a route id.
 * Returns DEFAULT_ROUTE for empty, malformed, or unknown segments.
 *
 * @param {string} [hash]
 * @returns {string}
 */
export function parseHash(hash) {
  if (!hash || typeof hash !== 'string') return DEFAULT_ROUTE;
  const m = hash.match(/^#\/([a-z0-9-]+)\/?$/i);
  if (!m) return DEFAULT_ROUTE;
  const seg = m[1].toLowerCase();
  return KNOWN_ROUTES.includes(seg) ? seg : DEFAULT_ROUTE;
}

/** @returns {string} current route id derived from window.location.hash. */
export function current() {
  if (typeof window === 'undefined') return DEFAULT_ROUTE;
  return parseHash(window.location.hash);
}

/** @param {string} segment navigate to `#/<segment>` (no-op if invalid). */
export function navigate(segment) {
  if (typeof window === 'undefined') return;
  const seg = String(segment || '').toLowerCase();
  if (!KNOWN_ROUTES.includes(seg)) return;
  const next = `#/${seg}`;
  if (window.location.hash !== next) {
    window.location.hash = next;
  }
}

/**
 * Readable store of the current route id. Updates on `hashchange`.
 * Initial value is derived from `window.location.hash` (or DEFAULT_ROUTE).
 */
export const route = readable(current(), (set) => {
  if (typeof window === 'undefined') return () => {};
  const handler = () => set(current());
  window.addEventListener('hashchange', handler);
  // Sync once on subscribe in case hash changed between store init and here.
  set(current());
  return () => window.removeEventListener('hashchange', handler);
});
