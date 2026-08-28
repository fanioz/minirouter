<script>
  import { onMount } from 'svelte';
  import { toast } from 'svelte-sonner';
  import { RefreshCw, Copy, Play } from 'lucide-svelte';

  let models = $state([]);
  let loading = $state(true);
  let error = $state(null);
  let isTesting = $state({});

  async function fetchModels() {
    loading = true;
    error = null;
    try {
      const res = await fetch('/models');
      if (!res.ok) throw new Error(`Failed to fetch: HTTP ${res.status}`);
      const json = await res.json();

      models = (json || []).flatMap(provider =>
        (provider.models || []).map(m => ({
          providerId: provider.providerId,
          providerName: provider.providerName,
          id: m.id,
          status: m.circuitStatus
        }))
      );
    } catch (e) {
      error = e.message;
      toast.error(`Error fetching models: ${e.message}`);
    } finally {
      loading = false;
    }
  }

  async function copyToClipboard(id) {
    try {
      await navigator.clipboard.writeText(id);
      toast.success("Model ID copied to clipboard");
    } catch (err) {
      toast.error("Failed to copy Model ID");
    }
  }

  async function testModel(modelId) {
    isTesting[modelId] = true;
    try {
      const res = await fetch('/v1/chat/completions', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          model: modelId,
          messages: [{ role: 'user', content: 'say hello in 7 words' }]
        })
      });
      if (!res.ok) throw new Error(`Failed to test: HTTP ${res.status}`);
      const data = await res.json();
      const content = data.choices?.[0]?.message?.content || "No content returned";
      toast.success(`Response: ${content}`);
    } catch (e) {
      toast.error(`Error testing model: ${e.message}`);
    } finally {
      isTesting[modelId] = false;
    }
  }

  onMount(() => {
    fetchModels();
  });

  function circuitClass(status) {
    const s = (status || '').toLowerCase();
    if (s === 'healthy' || s === 'closed') return 'status-ok';
    if (s === 'open') return 'status-fail';
    return 'status-muted';
  }
</script>

<div class="space-y-3.5">
  <div class="flex items-start justify-between gap-4">
    <div>
      <h1 class="page-h1">Models</h1>
      <p class="mt-[7px] max-w-[62ch] text-[13.5px]" style="color: var(--muted);">Aggregated models from enabled, healthy providers — cached per <span class="mono">MODELS_CACHE_TTL_SECONDS</span>.</p>
    </div>
    <button
      class="inline-flex h-9 shrink-0 items-center gap-2 rounded-full px-3.5 text-[13px] font-semibold transition-colors disabled:opacity-50"
      style="border: 1px solid var(--border); background: var(--surface); color: var(--fg);"
      onclick={fetchModels}
      disabled={loading}
    >
      <RefreshCw class="h-4 w-4 {loading ? 'animate-spin' : ''}" /> Refresh
    </button>
  </div>

  <div class="card-surface">
    {#if loading && models.length === 0}
      <div class="flex items-center justify-center p-10">
        <p class="text-[13.5px]" style="color: var(--muted);">Loading models...</p>
      </div>
    {:else if error && models.length === 0}
      <div class="p-4">
        <div class="rounded-xl p-4" style="background: var(--danger-soft); border: 1px solid color-mix(in srgb, var(--danger) 30%, transparent); color: var(--danger);">
          <p>Failed to load models: {error}</p>
          <button class="mt-2 text-[13px] font-semibold underline" onclick={fetchModels}>Retry</button>
        </div>
      </div>
    {:else if models.length === 0}
      <div class="flex flex-col items-center justify-center p-12">
        <p class="text-[13.5px]" style="color: var(--muted);">No models found.</p>
        <p class="mt-1 text-xs" style="color: var(--meta);">Make sure your providers are configured and enabled.</p>
      </div>
    {:else}
      <div class="overflow-x-auto">
        <table class="w-full text-left">
          <thead>
            <tr>
              <th class="caps-label px-4 py-2.5" style="background: var(--surface-head); border-bottom: 1px solid var(--border);">Provider</th>
              <th class="caps-label px-3 py-2.5" style="background: var(--surface-head); border-bottom: 1px solid var(--border);">Model ID</th>
              <th class="caps-label px-3 py-2.5" style="background: var(--surface-head); border-bottom: 1px solid var(--border);">Circuit</th>
              <th class="caps-label px-4 py-2.5 text-right" style="background: var(--surface-head); border-bottom: 1px solid var(--border);">Actions</th>
            </tr>
          </thead>
          <tbody>
            {#each models as model}
              <tr class="transition-colors hover:bg-accent" style="border-bottom: 1px solid var(--row-border);">
                <td class="px-4 py-3 text-[13px]" style="color: var(--muted);">{model.providerName}</td>
                <td class="mono px-3 py-3 text-xs font-medium" style="color: var(--fg-2);">{model.id}</td>
                <td class="px-3 py-3">
                  <span class="status-pill {circuitClass(model.status)}">{model.status}</span>
                </td>
                <td class="px-4 py-3">
                  <div class="flex items-center justify-end gap-1.5">
                    <button
                      class="inline-flex h-7 items-center gap-1.5 rounded-full px-3 text-xs font-semibold transition-colors"
                      style="border: 1px solid var(--border); background: var(--surface); color: var(--fg);"
                      onclick={() => copyToClipboard(model.id)}
                      title="Copy model ID"
                    >
                      <Copy class="h-3.5 w-3.5" /> Copy
                    </button>
                    <button
                      class="inline-flex h-7 items-center gap-1.5 rounded-full px-3 text-xs font-semibold transition-colors disabled:opacity-50"
                      style="border: 1px solid var(--border); background: var(--surface); color: var(--fg);"
                      onclick={() => testModel(model.id)}
                      disabled={isTesting[model.id]}
                    >
                      {#if isTesting[model.id]}
                        <RefreshCw class="h-3.5 w-3.5 animate-spin" /> Testing...
                      {:else}
                        <Play class="h-3.5 w-3.5" /> Test
                      {/if}
                    </button>
                  </div>
                </td>
              </tr>
            {/each}
          </tbody>
        </table>
      </div>
      <div class="flex flex-wrap items-center gap-4 px-4 py-3 text-xs" style="border-top: 1px solid var(--row-border); color: var(--muted);">
        <span>{models.length} model{models.length === 1 ? '' : 's'} from {new Set(models.map(m => m.providerId)).size} provider(s)</span>
        <span class="mono">test uses explicit routing: providerId/model</span>
      </div>
    {/if}
  </div>
</div>
