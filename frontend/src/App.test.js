import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/svelte';
import App from './App.svelte';

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
