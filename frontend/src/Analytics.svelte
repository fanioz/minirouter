<script>
  import { onMount } from 'svelte';
  import { toast } from 'svelte-sonner';
  import { RefreshCw } from '@lucide/svelte';

  let analytics = $state([]);
  let loading = $state(true);
  let error = $state(null);

  async function fetchAnalytics() {
    loading = true;
    error = null;
    try {
      const res = await fetch('/api/analytics/tokens');
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const data = await res.json();
      analytics = data.sort((a, b) => (b.totalTokensIn + b.totalTokensOut) - (a.totalTokensIn + a.totalTokensOut));
    } catch (e) {
      error = e.message;
      toast.error('Failed to load analytics: ' + e.message);
    } finally {
      loading = false;
    }
  }

  onMount(() => {
    fetchAnalytics();
  });

  function formatNumber(num) {
    if (num === null || num === undefined) return '0';
    return new Intl.NumberFormat('en-US').format(num);
  }

  function formatMoney(num) {
    const v = Number(num) || 0;
    return '$' + v.toFixed(2);
  }

  // KPI aggregates (real data only — no invented metrics)
  const totals = $derived({
    requests: analytics.reduce((s, a) => s + (a.totalRequests || 0), 0),
    tokensIn: analytics.reduce((s, a) => s + (a.totalTokensIn || 0), 0),
    tokensOut: analytics.reduce((s, a) => s + (a.totalTokensOut || 0), 0),
    cost: analytics.reduce((s, a) => s + (a.totalCost || 0), 0),
    estPortion: analytics.reduce((s, a) => s + (a.estimatedCostPortion || 0), 0)
  });

  const grandTokens = $derived(totals.tokensIn + totals.tokensOut);

  // Provider pins per design §2.1: coral / ink / stone, then neutral steps
  const pinColors = ['var(--accent)', 'var(--fg)', 'var(--muted)', 'var(--meta)', 'var(--border-strong)'];
  function pinColor(i) { return pinColors[i % pinColors.length]; }

  function sharePct(item) {
    if (!grandTokens) return 0;
    return ((item.totalTokensIn + item.totalTokensOut) / grandTokens) * 100;
  }
</script>

<div class="space-y-3.5">
  <!-- Page title -->
  <div class="flex items-start justify-between gap-4">
    <div>
      <h1 class="page-h1">Router analytics</h1>
      <p class="mt-[7px] max-w-[62ch] text-[13.5px]" style="color: var(--muted);">Token traffic and snapshot cost per provider. Pricing is a point-in-time snapshot — costs are estimates for comparison only.</p>
    </div>
    <div class="flex shrink-0 items-center gap-2">
      <span class="status-pill status-ok"><span class="live-dot" style="width:6px;height:6px;"></span> Live tail</span>
      <button
        class="inline-flex h-9 items-center gap-2 rounded-full px-3.5 text-[13px] font-semibold transition-colors disabled:opacity-50"
        style="border: 1px solid var(--border); background: var(--surface); color: var(--fg);"
        onclick={fetchAnalytics}
        disabled={loading}
      >
        <RefreshCw class="h-4 w-4 {loading ? 'animate-spin' : ''}" />
        Refresh
      </button>
    </div>
  </div>

  {#if loading && analytics.length === 0}
    <div class="card-surface flex items-center justify-center py-10">
      <p class="text-[13.5px]" style="color: var(--muted);">Loading analytics...</p>
    </div>
  {:else if error && analytics.length === 0}
    <div class="rounded-xl p-4" style="background: var(--danger-soft); border: 1px solid color-mix(in srgb, var(--danger) 30%, transparent); color: var(--danger);">
      <p>Failed to load data: {error}</p>
      <button class="mt-2 text-[13px] font-semibold underline" onclick={fetchAnalytics}>Retry</button>
    </div>
  {:else if analytics.length === 0}
    <div class="card-surface flex flex-col items-center justify-center border-dashed py-12">
      <p class="text-[13.5px]" style="color: var(--muted);">No usage data found.</p>
      <p class="mt-1 text-xs" style="color: var(--meta);">Send a request through <span class="mono">/v1/chat/completions</span> to populate analytics.</p>
    </div>
  {:else}
    <!-- KPI row per §6.2: caps label → 26px value → foot -->
    <div class="grid grid-cols-2 gap-3.5 lg:grid-cols-4">
      <div class="card-surface flex flex-col" style="padding: 16px 16px 14px;">
        <div class="flex items-center justify-between">
          <div class="caps-label">Requests</div>
        </div>
        <strong class="mt-2.5 text-[26px] font-bold leading-none tnum" style="letter-spacing: -.03em;">{formatNumber(totals.requests)}</strong>
        <div class="mt-2 text-xs" style="color: var(--muted);">across {analytics.length} provider{analytics.length === 1 ? '' : 's'}</div>
      </div>
      <div class="card-surface flex flex-col" style="padding: 16px 16px 14px;">
        <div class="caps-label">Tokens in</div>
        <strong class="mt-2.5 text-[26px] font-bold leading-none tnum" style="letter-spacing: -.03em;">{formatNumber(totals.tokensIn)}</strong>
        <div class="mt-2 text-xs" style="color: var(--muted);">prompt tokens</div>
      </div>
      <div class="card-surface flex flex-col" style="padding: 16px 16px 14px;">
        <div class="caps-label">Tokens out</div>
        <strong class="mt-2.5 text-[26px] font-bold leading-none tnum" style="letter-spacing: -.03em;">{formatNumber(totals.tokensOut)}</strong>
        <div class="mt-2 text-xs" style="color: var(--muted);">completion tokens</div>
      </div>
      <div class="card-surface flex flex-col" style="padding: 16px 16px 14px;">
        <div class="caps-label">Est. cost</div>
        <strong class="mt-2.5 text-[26px] font-bold leading-none tnum" style="letter-spacing: -.03em;">{formatMoney(totals.cost)}</strong>
        <div class="mt-2 text-xs" style="color: var(--muted);">
          {totals.estPortion > 0 ? `measured · est ${formatMoney(totals.estPortion)}` : 'snapshot estimate'}
        </div>
      </div>
    </div>

    <!-- Provider performance table per §6.5 -->
    <div class="card-surface">
      <div class="flex items-center justify-between px-4 py-3" style="border-bottom: 1px solid var(--border);">
        <h3 class="panel-title">Provider performance</h3>
        <span class="text-xs" style="color: var(--muted);">sorted by total tokens</span>
      </div>
      <div class="overflow-x-auto">
        <table class="w-full text-left">
          <thead>
            <tr>
              <th class="caps-label px-4 py-2.5" style="background: var(--surface-head); border-bottom: 1px solid var(--border);">Provider</th>
              <th class="caps-label px-3 py-2.5 text-right" style="background: var(--surface-head); border-bottom: 1px solid var(--border);">Requests</th>
              <th class="caps-label px-3 py-2.5 text-right" style="background: var(--surface-head); border-bottom: 1px solid var(--border);">Tokens in</th>
              <th class="caps-label px-3 py-2.5 text-right" style="background: var(--surface-head); border-bottom: 1px solid var(--border);">Tokens out</th>
              <th class="caps-label px-3 py-2.5" style="background: var(--surface-head); border-bottom: 1px solid var(--border);">Token share</th>
              <th class="caps-label px-4 py-2.5 text-right" style="background: var(--surface-head); border-bottom: 1px solid var(--border);">Est. cost</th>
            </tr>
          </thead>
          <tbody>
            {#each analytics as item, i}
              <tr class="transition-colors hover:bg-accent" style="border-bottom: 1px solid var(--row-border);">
                <td class="px-4 py-3">
                  <div class="flex items-center gap-2">
                    <span class="h-2 w-2 shrink-0 rounded-full" style="background: {pinColor(i)};"></span>
                    <b class="mono text-[13px]" style="letter-spacing: -.01em;">{item.providerId}</b>
                    {#if i === 0}
                      <span class="status-pill" style="background: var(--surface-warm); border-color: color-mix(in srgb, var(--accent) 30%, transparent); color: var(--accent);">Top usage</span>
                    {/if}
                  </div>
                </td>
                <td class="px-3 py-3 text-right font-semibold tnum">{formatNumber(item.totalRequests)}</td>
                <td class="mono px-3 py-3 text-right text-xs" style="color: var(--fg-2);">{formatNumber(item.totalTokensIn)}</td>
                <td class="mono px-3 py-3 text-right text-xs" style="color: var(--fg-2);">{formatNumber(item.totalTokensOut)}</td>
                <td class="px-3 py-3">
                  <div class="flex items-center gap-2.5">
                    <div class="bar-track w-28">
                      <div class="bar-fill" style="width: {sharePct(item).toFixed(1)}%; background: {pinColor(i)};"></div>
                    </div>
                    <span class="mono w-12 shrink-0 text-right text-xs" style="color: var(--muted);">{sharePct(item).toFixed(1)}%</span>
                  </div>
                </td>
                <td class="px-4 py-3 text-right">
                  <span class="mono text-xs font-bold" style="color: var(--fg-2);">{formatMoney(item.totalCost)}</span>
                  {#if item.estimatedCostPortion > 0}
                    <span class="ml-1.5 text-[11px]" style="color: var(--meta);">• est</span>
                  {/if}
                </td>
              </tr>
            {/each}
          </tbody>
        </table>
      </div>
      <div class="flex flex-wrap items-center gap-4 px-4 py-3 text-xs" style="border-top: 1px solid var(--row-border); color: var(--muted);">
        <span>Pricing is a point-in-time snapshot. Costs are estimates for comparison only — not invoiced amounts.</span>
        {#if totals.estPortion > 0}
          <span class="mono">4 char ≈ 1 tok est. applied to zero-usage streams</span>
        {/if}
      </div>
    </div>
  {/if}
</div>
