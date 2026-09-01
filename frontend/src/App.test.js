import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, fireEvent, cleanup } from '@testing-library/svelte';
import App from './App.svelte';
import { NAV_KEY, DEFAULTS, loadNav, saveNav } from '$lib/persist.js';
import { DEFAULT_ROUTE, parseHash, KNOWN_ROUTES } from '$lib/router.js';

// Mock mode-watcher without Svelte runes
vi.mock('mode-watcher', () => {
  return {
    ModeWatcher: vi.fn(),
    mode: { current: 'system' },
    userPrefersMode: { current: 'system' },
    setMode: vi.fn(),
  };
});

describe('App - Theme Toggle', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    window.localStorage.clear();
    window.location.hash = '';
  });

  it('renders the theme toggle button in sidebar', () => {
    render(App);
    const toggleBtns = screen.getAllByRole('button', { name: /toggle theme/i });
    expect(toggleBtns.length).toBeGreaterThanOrEqual(1);
  });

  it('expands the sidebar after collapsing (toggle always available)', async () => {
    render(App);
    const toggleBtns = screen.getAllByRole('button', { name: /toggle sidebar/i });
    expect(toggleBtns.length).toBeGreaterThanOrEqual(1);
    const toggleBtn = toggleBtns[0];

    const sidebar = toggleBtn.closest('aside');
    expect(sidebar).not.toBeNull();

    // collapse
    await fireEvent.click(toggleBtn);
    expect(sidebar.style.width).toBe('64px');

    // expand again — regression: toggle must still exist when collapsed
    const toggleWhenCollapsed = screen.getAllByRole('button', { name: /toggle sidebar/i });
    expect(toggleWhenCollapsed.length).toBeGreaterThanOrEqual(1);
    await fireEvent.click(toggleWhenCollapsed[0]);
    expect(sidebar.style.width).toBe('var(--sidebar-w)'); // resolves to 268px
  });

  it('renders the sidebar with navigation tabs', () => {
    render(App);
    expect(screen.getAllByText('MiniRouter').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('Home').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('Providers').length).toBeGreaterThanOrEqual(1);
  });
});

// Story dashboard-navigation.1.1 — AC #10: routing + persistence tests.
describe('App - Routing & Persistence', () => {
  beforeEach(() => {
    window.localStorage.clear();
    window.location.hash = '';
  });

  afterEach(() => {
    cleanup();
    window.localStorage.clear();
    window.location.hash = '';
  });

  it('defaults the active route to #/home when no prior preference exists', async () => {
    expect(window.localStorage.getItem(NAV_KEY)).toBeNull();
    render(App);
    // Home button is the active nav item by default.
    const homeBtn = screen.getAllByRole('button', { name: /^home/i })[0];
    expect(homeBtn).toHaveAttribute('aria-current', 'page');
    // The empty hash should resolve to DEFAULT_ROUTE.
    expect(parseHash(window.location.hash)).toBe(DEFAULT_ROUTE);
    // Providers is not active by default.
    const providersBtn = screen.getAllByRole('button', { name: /providers/i })[0];
    expect(providersBtn).not.toHaveAttribute('aria-current', 'page');
  });

  it('clicking Providers updates the URL hash and the active nav state', async () => {
    render(App);
    const providersBtn = screen.getAllByRole('button', { name: /providers/i })[0];
    await fireEvent.click(providersBtn);
    // hash was set synchronously by navigate()
    expect(window.location.hash).toBe('#/providers');
    // Let the hashchange -> route store -> activeTab pipeline flush.
    await new Promise((r) => setTimeout(r, 0));
    // active state moved (re-query — Svelte may have re-rendered the same node)
    const providersBtnAfter = screen.getAllByRole('button', { name: /providers/i })[0];
    expect(providersBtnAfter).toHaveAttribute('aria-current', 'page');
    // home is no longer active
    const homeBtn = screen.getAllByRole('button', { name: /^home/i })[0];
    expect(homeBtn).not.toHaveAttribute('aria-current', 'page');
  });

  it('responds to browser hashchange (back/forward) by switching tabs', async () => {
    render(App);
    // Simulate user navigating to logs
    window.location.hash = '#/logs';
    window.dispatchEvent(new HashChangeEvent('hashchange'));
    await new Promise((r) => setTimeout(r, 0));
    const logsBtn = screen.getAllByRole('button', { name: /^logs/i })[0];
    expect(logsBtn).toHaveAttribute('aria-current', 'page');

    // Simulate browser back to providers
    window.location.hash = '#/providers';
    window.dispatchEvent(new HashChangeEvent('hashchange'));
    await new Promise((r) => setTimeout(r, 0));
    const providersBtn = screen.getAllByRole('button', { name: /providers/i })[0];
    expect(providersBtn).toHaveAttribute('aria-current', 'page');
  });

  it('deep-link with #/providers on mount activates Providers directly', async () => {
    window.location.hash = '#/providers';
    render(App);
    // No flash of home: Providers is the active tab right after mount.
    const providersBtn = screen.getAllByRole('button', { name: /providers/i })[0];
    expect(providersBtn).toHaveAttribute('aria-current', 'page');
    // And the Providers component heading is visible.
    expect(screen.getByRole('heading', { level: 1, name: 'Providers' })).toBeInTheDocument();
  });

  it('persists collapsed + active route to localStorage and restores on remount', async () => {
    // First mount: collapse sidebar, navigate to apikey.
    const first = render(App);
    const sidebar = screen.getByLabelText('Toggle Sidebar').closest('aside');
    expect(sidebar).not.toBeNull();
    await fireEvent.click(screen.getByLabelText('Toggle Sidebar'));
    expect(sidebar.style.width).toBe('64px');
    const apikeyBtn = screen.getAllByRole('button', { name: /api keys/i })[0];
    await fireEvent.click(apikeyBtn);
    expect(window.location.hash).toBe('#/apikey');

    // Wait for hashchange -> route store -> activeTab -> $effect -> saveNav.
    await new Promise((r) => setTimeout(r, 0));
    const saved = loadNav();
    expect(saved.collapsed).toBe(true);
    expect(saved.route).toBe('apikey');

    // Sanity: the same key the persist layer uses is populated.
    const raw = window.localStorage.getItem(NAV_KEY);
    expect(raw).not.toBeNull();
    const parsed = JSON.parse(raw);
    expect(parsed.collapsed).toBe(true);
    expect(parsed.route).toBe('apikey');

    first.unmount();

    // Second mount (simulating page reload): collapse + route both restored.
    window.location.hash = '';
    const second = render(App);
    await new Promise((r) => setTimeout(r, 0));
    const restored = screen.getByLabelText('Toggle Sidebar').closest('aside');
    expect(restored.style.width).toBe('64px');
    const apikeyAfterReload = screen.getAllByRole('button', { name: /api keys/i })[0];
    expect(apikeyAfterReload).toHaveAttribute('aria-current', 'page');
    second.unmount();
  });

  it('falls back to defaults when localStorage contains malformed data', () => {
    window.localStorage.setItem(NAV_KEY, 'not-json');
    expect(loadNav()).toEqual(DEFAULTS);
  });

  it('rejects unknown routes in parseHash and falls back to DEFAULT_ROUTE', () => {
    expect(parseHash('#/home')).toBe('home');
    expect(parseHash('#/providers')).toBe('providers');
    expect(parseHash('#/apikey')).toBe('apikey');
    expect(parseHash('#/logs')).toBe('logs');
    expect(parseHash('#/nope')).toBe(DEFAULT_ROUTE);
    expect(parseHash('')).toBe(DEFAULT_ROUTE);
    expect(parseHash('garbage')).toBe(DEFAULT_ROUTE);
  });

  it('KNOWN_ROUTES covers the 8 nav tabs', () => {
    for (const id of [
      'home', 'providers', 'presets', 'models', 'playground',
      'analytics', 'logs', 'apikey'
    ]) {
      expect(KNOWN_ROUTES).toContain(id);
    }
  });
});
