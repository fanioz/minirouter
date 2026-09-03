<script>
  import { onMount } from 'svelte';
  import { toast } from 'svelte-sonner';
  import { Input } from '$lib/components/ui/input';
  import { authenticatedFetch } from '$lib/auth.js';
  import { RefreshCw, FilterX } from '@lucide/svelte';

  let logs = $state([]);
  let providers = $state([]);
  let loading = $state(true);
  let error = $state(null);

  // Filters
  let filterProviderId = $state('');
  let filterStatus = $state('all');
  let filterModel = $state('');
  let limit = $state(100);

  async function fetchProviders() {
    try {
      const res = await authenticatedFetch('/api/providers');
      if (res.ok) {
        providers = await res.json();
      }
    } catch (e) {
      console.error("Could not fetch providers for filter dropdown", e);
    }
  }

  async function fetchLogs() {
    loading = true;
    error = null;
    try {
      const params = new URLSearchParams();
      params.append('limit', limit.toString());
      if (filterProviderId) params.append('providerId', filterProviderId);
      if (filterModel) params.append('model', filterModel);
      if (filterStatus === 'success') params.append('success', 'true');
      if (filterStatus === 'failure') params.append('success', 'false');

      const response = await authenticatedFetch(`/api/logs?${params.toString()}`);
      if (!response.ok) {
        throw new Error(`HTTP error: ${response.status}`);
      }
      logs = await response.json();
    } catch (e) {
      error = e.message;
      toast.error('Failed to load logs: ' + e.message);
    } finally {
      loading = false;
    }
  }

  onMount(() => {
    fetchProviders();
    fetchLogs();
  });

  function formatDate(isoString) {
    return new Date(isoString).toLocaleString();
  }

  function clearFilters() {
    filterProviderId = '';
    filterModel = '';
    filterStatus = 'all';
    fetchLogs();
  }

  function latencyClass(ms) {
    if (ms === null || ms === undefined) return '';
    if (ms < 350) return 'good';
    if (ms < 500) return 'mid';
    return 'bad';
  }
</script>

<div class="flex flex-col gap-3.5">
  <div class="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
    <div>
      <h1 class="page-h1">Request logs</h1>
      <p class="mt-[7px] max-w-[62ch] text-[13.5px]" style="color: var(--muted);">Recent routing history and token usage. One row per request with latency and status.</p>
    </div>
    <button
      class="inline-flex h-9 shrink-0 items-center gap-2 rounded-full px-3.5 text-[13px] font-semibold transition-colors disabled:opacity-50"
      style="border: 1px solid var(--border); background: var(--surface); color: var(--fg);"
      onclick={fetchLogs}
      disabled={loading}
    >
      <RefreshCw class="h-4 w-4 {loading ? 'animate-spin' : ''}" /> Refresh
    </button>
  </div>

  <!-- Filter bar — pill selects per §6.4 -->
  <div class="card-surface flex flex-col gap-3 p-3.5 sm:flex-row sm:items-end">
    <div class="w-full sm:w-44">
      <label for="provider-filter" class="caps-label mb-1.5 block">Provider</label>
      <select
        id="provider-filter"
        class="h-9 w-full rounded-full border border-border bg-surface px-3.5 text-[13px] font-semibold outline-none transition-colors focus-visible:ring-3"
        style="color: var(--fg);"
        bind:value={filterProviderId}
        onchange={fetchLogs}
      >
        <option value="">All Providers</option>
        {#each providers as p}
          <option value={p.id}>{p.id}</option>
        {/each}
      </select>
    </div>

    <div class="w-full sm:w-40">
      <label for="status-filter" class="caps-label mb-1.5 block">Status</label>
      <select
        id="status-filter"
        class="h-9 w-full rounded-full border border-border bg-surface px-3.5 text-[13px] font-semibold outline-none transition-colors focus-visible:ring-3"
        style="color: var(--fg);"
        bind:value={filterStatus}
        onchange={fetchLogs}
      >
        <option value="all">All</option>
        <option value="success">Success</option>
        <option value="failure">Failure</option>
      </select>
    </div>

    <div class="w-full sm:w-44">
      <label for="model-filter" class="caps-label mb-1.5 block">Model</label>
      <Input
        id="model-filter"
        placeholder="e.g. gpt-4o"
        class="rounded-full"
        bind:value={filterModel}
        onchange={fetchLogs}
        onkeyup={(e) => e.key === 'Enter' && fetchLogs()}
      />
    </div>

    {#if filterProviderId !== '' || filterStatus !== 'all' || filterModel !== ''}
      <button
        class="mb-0.5 inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-full transition-colors sm:ml-auto"
        style="border: 1px solid var(--border); background: var(--surface); color: var(--muted);"
        onclick={clearFilters}
        title="Clear Filters"
        aria-label="Clear filters"
      >
        <FilterX class="h-4 w-4" />
      </button>
    {/if}
  </div>

  {#if loading && logs.length === 0}
    <div class="card-surface flex items-center justify-center p-12">
      <p class="text-[13.5px]" style="color: var(--muted);">Loading logs...</p>
    </div>
  {:else if error && logs.length === 0}
    <div class="rounded-xl p-4" style="background: var(--danger-soft); border: 1px solid color-mix(in srgb, var(--danger) 30%, transparent); color: var(--danger);">
      <p class="font-semibold">Error loading logs</p>
      <p class="mt-0.5 text-[13px]">{error}</p>
    </div>
  {:else if logs.length === 0}
    <div class="card-surface flex flex-col items-center justify-center border-dashed p-12 text-center">
      <p class="text-[13.5px]" style="color: var(--muted);">No requests found matching your criteria.</p>
    </div>
  {:else}
    <div class="card-surface">
      <div class="overflow-x-auto">
        <table class="w-full text-left">
          <thead>
            <tr>
              <th class="caps-label px-4 py-2.5" style="background: var(--surface-head); border-bottom: 1px solid var(--border);">Time</th>
              <th class="caps-label px-3 py-2.5" style="background: var(--surface-head); border-bottom: 1px solid var(--border);">Provider</th>
              <th class="caps-label px-3 py-2.5" style="background: var(--surface-head); border-bottom: 1px solid var(--border);">Model</th>
              <th class="caps-label px-3 py-2.5" style="background: var(--surface-head); border-bottom: 1px solid var(--border);">Status</th>
              <th class="caps-label px-3 py-2.5 text-right" style="background: var(--surface-head); border-bottom: 1px solid var(--border);">Latency</th>
              <th class="caps-label px-4 py-2.5 text-right" style="background: var(--surface-head); border-bottom: 1px solid var(--border);">Tokens in / out</th>
            </tr>
          </thead>
          <tbody>
            {#each logs as log}
              <tr class="transition-colors hover:bg-accent" style="border-bottom: 1px solid var(--row-border);">
                <td class="mono whitespace-nowrap px-4 py-3 text-[11px] align-top" style="color: var(--meta);">{formatDate(log.timestamp)}</td>
                <td class="px-3 py-3 align-top text-[13px] font-bold" style="letter-spacing: -.01em;">{log.providerId}</td>
                <td class="mono px-3 py-3 align-top text-xs" style="color: var(--muted);">{log.model || '—'}</td>
                <td class="px-3 py-3 align-top">
                  {#if log.success}
                    <span class="status-pill status-ok">Success</span>
                  {:else}
                    <span class="status-pill status-fail">Failed</span>
                    {#if log.errorMessage}
                      <div class="mono mt-2 max-w-md truncate rounded-lg whitespace-pre-wrap p-2 text-xs" style="background: var(--danger-soft); color: var(--danger);">{log.errorMessage}</div>
                    {/if}
                  {/if}
                </td>
                <td class="px-3 py-3 text-right align-top">
                  <span class="lat {latencyClass(log.latencyMs)}">{log.latencyMs} ms</span>
                </td>
                <td class="mono px-4 py-3 text-right align-top text-xs" style="color: var(--muted);">
                  {#if log.tokensIn !== null || log.tokensOut !== null}
                    {log.tokensIn || 0} / {log.tokensOut || 0}
                  {:else}
                    —
                  {/if}
                </td>
              </tr>
            {/each}
          </tbody>
        </table>
      </div>
      {#if logs.length === limit}
        <div class="flex justify-center p-3.5" style="border-top: 1px solid var(--row-border);">
          <button
            class="inline-flex h-9 items-center gap-2 rounded-full px-4 text-[13px] font-semibold transition-colors"
            style="border: 1px solid var(--border); background: var(--surface); color: var(--fg);"
            onclick={() => { limit += 100; fetchLogs(); }}
          >
            Load more
          </button>
        </div>
      {/if}
    </div>
  {/if}
</div>
