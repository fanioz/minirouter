<script>
  import { ModeWatcher, setMode, userPrefersMode } from 'mode-watcher';
  import Providers from './Providers.svelte';
  import Presets from './Presets.svelte';
  import Models from './Models.svelte';
  import Home from './Home.svelte';
  import ApiKey from './ApiKey.svelte';
  import Logs from './Logs.svelte';
  import Analytics from './Analytics.svelte';
  import Playground from './Playground.svelte';
  import { Toaster } from '$lib/components/ui/sonner';
  import {
    PanelLeft, LayoutDashboard, Settings, Box, Key, ScrollText,
    BarChart3, MessageSquare, Sun, Moon, Monitor, LayoutGrid
  } from '@lucide/svelte';

  let activeTab = $state('home');
  let isSidebarCollapsed = $state(false);

  // Design system §5.3: two nav groups — Operate & Observe
  const operateTabs = [
    { id: 'home', label: 'Home', icon: LayoutDashboard },
    { id: 'providers', label: 'Providers', icon: Settings },
    { id: 'presets', label: 'Presets', icon: LayoutGrid },
    { id: 'models', label: 'Models', icon: Box },
    { id: 'playground', label: 'Playground', icon: MessageSquare }
  ];
  const observeTabs = [
    { id: 'analytics', label: 'Analytics', icon: BarChart3 },
    { id: 'logs', label: 'Logs', icon: ScrollText },
    { id: 'apikey', label: 'API Keys', icon: Key }
  ];
  const allTabs = [...operateTabs, ...observeTabs];
  let activeLabel = $derived(allTabs.find(t => t.id === activeTab)?.label ?? 'Home');

  const themeModes = ['system', 'light', 'dark'];
  const themeIcons = { system: Monitor, light: Sun, dark: Moon };
  const themeLabels = { system: 'System theme', light: 'Light theme', dark: 'Dark theme' };

  let currentThemeMode = $derived(userPrefersMode.current ?? 'system');
  let ThemeIcon = $derived(themeIcons[currentThemeMode]);
  let themeLabel = $derived(themeLabels[currentThemeMode]);

  function toggleSidebar() {
    isSidebarCollapsed = !isSidebarCollapsed;
  }

  function cycleTheme() {
    const idx = themeModes.indexOf(currentThemeMode);
    const next = themeModes[(idx + 1) % themeModes.length];
    setMode(next);
  }
</script>

<ModeWatcher />

<div class="flex min-h-screen w-full bg-background text-foreground">
  <!-- Sidebar — 268px sticky, surface, 1px stroke -->
  <aside
    class="sticky top-0 hidden h-screen shrink-0 flex-col bg-card transition-all duration-300 ease-in-out min-[860px]:flex"
    style="width: {isSidebarCollapsed ? '64px' : 'var(--sidebar-w)'}; border-right: 1px solid var(--border);"
  >
    <!-- Brand head — 64px -->
    <div class="flex h-16 shrink-0 items-center gap-2.5" style="border-bottom: 1px solid var(--border); padding: 0 {isSidebarCollapsed ? '12px' : '16px'};">
      {#if !isSidebarCollapsed}
        <div class="grid h-8 w-8 shrink-0 place-items-center text-[13px] font-extrabold" style="border-radius: 9px; background: var(--fg); color: var(--bg); letter-spacing: -.04em;">◈</div>
        <div class="min-w-0 flex-1">
          <div class="truncate text-[15px] font-bold leading-none" style="letter-spacing: -.02em;">MiniRouter</div>
          <div class="mt-1 truncate text-[10.5px] font-semibold uppercase leading-none tracking-[.06em]" style="color: var(--meta);">Native AOT • Edge Router</div>
        </div>
      {/if}
      <button
        class="shrink-0 transition-colors hover:bg-accent hover:text-foreground {isSidebarCollapsed ? 'mx-auto grid h-9 w-9 place-items-center rounded-[9px]' : 'ml-auto grid h-9 w-9 place-items-center rounded-[9px]'}"
        style="color: var(--muted);"
        onclick={toggleSidebar}
        title="Toggle Sidebar"
        aria-label="Toggle Sidebar"
      >
        <PanelLeft class="h-[18px] w-[18px]" />
      </button>
    </div>

    <!-- Nav — ink-active per §6.3, hover warm per §7 -->
    <nav class="flex-1 overflow-y-auto px-2.5 pb-2 pt-3">
      {#if isSidebarCollapsed}
        <div class="mb-3 flex justify-center pt-1">
          <div class="grid h-8 w-8 place-items-center text-[13px] font-extrabold" style="border-radius: 9px; background: var(--fg); color: var(--bg);">◈</div>
        </div>
      {:else}
        <div class="caps-label px-2.5 pb-2 pt-1" style="letter-spacing: .09em;">Operate</div>
      {/if}
      {#each operateTabs as tab}
        {@const Icon = tab.icon}
        <button
          class="mb-0.5 flex w-full items-center gap-2.5 rounded-[10px] border border-transparent py-[9px] text-left text-[13.5px] font-medium transition-colors active:translate-y-px
                 {activeTab === tab.id
                   ? 'border-foreground bg-foreground text-background'
                   : 'text-muted-foreground hover:bg-nav-hover hover:text-foreground'}
                 {isSidebarCollapsed ? 'justify-center px-0' : 'px-2.5 text-left'}"
          onclick={() => activeTab = tab.id}
          title={isSidebarCollapsed ? tab.label : ''}
          aria-current={activeTab === tab.id ? 'page' : undefined}
        >
          <Icon class="h-[18px] w-[18px] shrink-0" />
          {#if !isSidebarCollapsed}
            <span class="truncate">{tab.label}</span>
          {/if}
        </button>
      {/each}

      {#if !isSidebarCollapsed}
        <div class="caps-label px-2.5 pb-2 pt-4" style="letter-spacing: .09em;">Observe</div>
      {:else}
        <div class="mx-auto my-2 h-px w-6" style="background: var(--border);"></div>
      {/if}
      {#each observeTabs as tab}
        {@const Icon = tab.icon}
        <button
          class="mb-0.5 flex w-full items-center gap-2.5 rounded-[10px] border border-transparent py-[9px] text-left text-[13.5px] font-medium transition-colors active:translate-y-px
                 {activeTab === tab.id
                   ? 'border-foreground bg-foreground text-background'
                   : 'text-muted-foreground hover:bg-nav-hover hover:text-foreground'}
                 {isSidebarCollapsed ? 'justify-center px-0' : 'px-2.5 text-left'}"
          onclick={() => activeTab = tab.id}
          title={isSidebarCollapsed ? tab.label : ''}
          aria-current={activeTab === tab.id ? 'page' : undefined}
        >
          <Icon class="h-[18px] w-[18px] shrink-0" />
          {#if !isSidebarCollapsed}
            <span class="truncate">{tab.label}</span>
          {/if}
        </button>
      {/each}
    </nav>

    <!-- Sidebar foot — live pill + theme toggle -->
    <div class="shrink-0 p-2.5" style="border-top: 1px solid var(--border);">
      {#if !isSidebarCollapsed}
        <div class="mb-2.5 flex items-center gap-2 px-2.5 py-2 text-xs" style="background: var(--surface-warm); border: 1px solid var(--border-soft); border-radius: 12px;">
          <span class="live-dot"></span>
          <span class="mono text-[12px]" style="color: var(--muted);">:8080</span>
        </div>
      {/if}
      <button
        class="flex w-full items-center gap-2.5 rounded-[10px] py-[9px] font-medium transition-colors hover:bg-nav-hover hover:text-foreground
               {isSidebarCollapsed ? 'justify-center px-0 text-[13.5px]' : 'px-2.5 text-left text-[13.5px]'}"
        style="color: var(--muted);"
        onclick={cycleTheme}
        title={isSidebarCollapsed ? themeLabel : ''}
        aria-label="Toggle theme: {themeLabel}"
      >
        <ThemeIcon class="h-[18px] w-[18px] shrink-0" />
        {#if !isSidebarCollapsed}
          <span class="truncate">{themeLabel}</span>
        {/if}
      </button>
    </div>
  </aside>

  <!-- Main -->
  <div class="flex min-w-0 flex-1 flex-col">
    <!-- Sticky topbar — 64px, warm blur backdrop per §5.3 -->
    <header
      class="sticky top-0 z-20 flex h-16 shrink-0 items-center gap-3.5 px-6"
      style="background: color-mix(in srgb, var(--bg) 88%, transparent); backdrop-filter: saturate(1.5) blur(10px); border-bottom: 1px solid var(--border);"
    >
      <div class="flex min-w-0 items-center gap-2.5 text-[13px]">
        <span class="whitespace-nowrap" style="color: var(--muted);">MiniRouter</span>
        <span style="color: var(--meta);">/</span>
        <strong class="whitespace-nowrap font-semibold">{activeLabel}</strong>
      </div>
      <div class="h-5 w-px shrink-0" style="background: var(--border);"></div>
      <div class="flex items-center gap-2 text-xs">
        <span class="live-dot"></span>
        <span class="font-medium" style="color: var(--muted);">Live</span>
      </div>
      <button
        class="ml-auto grid h-9 w-9 place-items-center rounded-[9px] transition-colors hover:bg-accent hover:text-foreground min-[860px]:hidden"
        style="color: var(--muted);"
        onclick={cycleTheme}
        title={themeLabel}
        aria-label="Toggle theme: {themeLabel}"
      >
        <ThemeIcon class="h-[18px] w-[18px]" />
      </button>
    </header>

    <!-- Mobile nav — horizontal pill strip per §5.4 -->
    <nav class="flex gap-1.5 overflow-x-auto px-4 py-2 min-[860px]:hidden" style="border-bottom: 1px solid var(--border); background: var(--surface);">
      {#each allTabs as tab}
        <button
          class="h-7 shrink-0 rounded-full px-3 text-xs font-semibold transition-all"
          style="border: 1px solid {activeTab === tab.id ? 'var(--fg)' : 'var(--border)'}; background: {activeTab === tab.id ? 'var(--fg)' : 'var(--surface)'}; color: {activeTab === tab.id ? 'var(--bg)' : 'var(--muted)'};"
          onclick={() => activeTab = tab.id}
          aria-current={activeTab === tab.id ? 'page' : undefined}
        >
          {tab.label}
        </button>
      {/each}
    </nav>

    <!-- Content — max 1360px centered, per §5.1 -->
    <main class="min-w-0 flex-1">
      <div class="mx-auto w-full max-w-[1360px] px-6 pb-8 pt-5">
        {#if activeTab === 'home'}
          <Home />
        {:else if activeTab === 'providers'}
          <Providers />
        {:else if activeTab === 'presets'}
          <Presets />
        {:else if activeTab === 'models'}
          <Models />
        {:else if activeTab === 'apikey'}
          <ApiKey />
        {:else if activeTab === 'logs'}
          <Logs />
        {:else if activeTab === 'analytics'}
          <Analytics />
        {:else if activeTab === 'playground'}
          <Playground />
        {/if}
      </div>
    </main>
  </div>
</div>

<Toaster richColors closeButton position="bottom-right" />
