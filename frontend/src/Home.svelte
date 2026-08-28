<script>
  import { onMount } from 'svelte';
  import { toast } from 'svelte-sonner';
  import { Switch } from '$lib/components/ui/switch';
  import { Badge } from '$lib/components/ui/badge';
  import { RefreshCw } from '@lucide/svelte';

  let providers = $state([]);
  let loading = $state(true);
  let error = $state(null);

  async function fetchProviders() {
    loading = true;
    error = null;
    try {
      const res = await fetch('/api/providers');
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      providers = await res.json();
    } catch (e) {
      error = e.message;
      toast.error('Failed to load providers: ' + e.message);
    } finally {
      loading = false;
    }
  }

  async function toggleStatus(provider) {
    try {
      const payload = {
        name: provider.name,
        baseUrl: provider.baseUrl,
        apiKey: '',
        enabled: !provider.enabled,
        models: provider.models
      };

      const res = await fetch(`/api/providers/${provider.id}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });

      if (!res.ok) {
        const errData = await res.json().catch(() => ({}));
        throw new Error(errData.message || `HTTP ${res.status}`);
      }

      toast.success(`Provider '${provider.id}' ${!provider.enabled ? 'enabled' : 'disabled'}`);
      await fetchProviders();
    } catch (e) {
      toast.error(`Error toggling status: ${e.message}`);
    }
  }

  onMount(() => {
    fetchProviders();
  });

  const enabledCount = $derived(providers.filter(p => p.enabled).length);

</script>

<div class="space-y-3.5">
  <!-- Page title per §5.1 -->
  <div class="flex items-start justify-between gap-4">
    <div>
      <h1 class="page-h1">Home</h1>
      <p class="mt-[7px] max-w-[62ch] text-[13.5px]" style="color: var(--muted);">Provider status dashboard — enable, disable, and monitor routing targets.</p>
    </div>
    <div class="flex shrink-0 items-center gap-2">
      <span class="status-pill status-ok"><span class="live-dot" style="width:6px;height:6px;"></span> Live</span>
    </div>
  </div>

  {#if loading}
    <div class="card-surface flex items-center justify-center py-10">
      <p class="text-[13.5px]" style="color: var(--muted);">Loading providers...</p>
    </div>
  {:else if error}
    <div class="rounded-xl p-4" style="background: var(--danger-soft); border: 1px solid color-mix(in srgb, var(--danger) 30%, transparent); color: var(--danger);">
      <p>Failed to load data: {error}</p>
      <button class="mt-2 text-[13px] font-semibold underline" onclick={fetchProviders}>Retry</button>
    </div>
  {:else if providers.length === 0}
    <div class="card-surface flex flex-col items-center justify-center border-dashed py-12">
      <p class="text-[13.5px]" style="color: var(--muted);">No providers configured.</p>
    </div>
  {:else}
    <!-- KPI strip per §6.2 -->
    <div class="grid grid-cols-2 gap-3.5 lg:grid-cols-3">
      <div class="card-surface flex flex-col" style="padding: 16px 16px 14px;">
        <div class="caps-label">Providers</div>
        <strong class="mt-2.5 text-[26px] font-bold leading-none tnum" style="letter-spacing: -.03em;">{providers.length}</strong>
        <div class="mt-2 text-xs" style="color: var(--muted);">configured targets</div>
      </div>
      <div class="card-surface flex flex-col" style="padding: 16px 16px 14px;">
        <div class="caps-label">Enabled</div>
        <strong class="mt-2.5 text-[26px] font-bold leading-none tnum" style="letter-spacing: -.03em; color: var(--success);">{enabledCount}</strong>
        <div class="mt-2 text-xs" style="color: var(--muted);">in rotation</div>
      </div>
      <div class="card-surface col-span-2 flex flex-col lg:col-span-1" style="padding: 16px 16px 14px;">
        <div class="caps-label">Disabled</div>
        <strong class="mt-2.5 text-[26px] font-bold leading-none tnum" style="letter-spacing: -.03em;">{providers.length - enabledCount}</strong>
        <div class="mt-2 text-xs" style="color: var(--muted);">excluded from routing</div>
      </div>
    </div>

    <!-- Provider health grid per §6.2 -->
    <div class="card-surface">
      <div class="flex items-center justify-between px-4 py-3" style="border-bottom: 1px solid var(--border);">
        <h3 class="panel-title">Provider health</h3>
        <button
          class="inline-flex h-9 items-center gap-2 rounded-full px-3.5 text-[13px] font-semibold transition-colors disabled:opacity-50"
          style="border: 1px solid var(--border); background: var(--surface); color: var(--fg);"
          onclick={fetchProviders}
          disabled={loading}
        >
          <RefreshCw class="h-4 w-4 {loading ? 'animate-spin' : ''}" />
          Refresh
        </button>
      </div>
      <div class="grid grid-cols-1 gap-2.5 p-3.5 md:grid-cols-2">
        {#each providers as provider, i}
          <div class="card-surface flex items-center gap-2.5 transition-colors hover:bg-accent" style="padding: 10px 11px; box-shadow: none;">
            <span class="h-2.5 w-2.5 shrink-0 rounded-full {provider.enabled ? 'live-dot' : ''}" style={provider.enabled ? '' : 'background: var(--meta);'}></span>
            <div class="min-w-0 flex-1">
              <div class="flex items-center gap-2">
                <b class="truncate text-[13px]" style="letter-spacing: -.01em;">{provider.id}</b>
                {#if provider.enabled}
                  <span class="status-pill status-ok">Live</span>
                {:else}
                  <span class="status-pill status-muted">Disabled</span>
                {/if}
              </div>
              <div class="mono mt-0.5 truncate text-xs" style="color: var(--fg-2);" title={provider.baseUrl}>{provider.baseUrl}</div>
            </div>
            <Switch checked={provider.enabled} onCheckedChange={() => toggleStatus(provider)} aria-label="Enable {provider.id}" />
          </div>
        {/each}
      </div>
    </div>
  {/if}
</div>
