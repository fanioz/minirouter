<script>
  import { onMount } from 'svelte';
  import { toast } from 'svelte-sonner';
  import { Button } from '$lib/components/ui/button';
  import { Input } from '$lib/components/ui/input';
  import { Label } from '$lib/components/ui/label';
  import * as Dialog from '$lib/components/ui/dialog';
  import { authenticatedFetch } from '$lib/auth.js';
  import { Loader2, Key } from '@lucide/svelte';

  let presets = $state([]);
  let loading = $state(true);
  let error = $state(null);
  let activeTab = $state('free');

  let enableModal = $state(false);
  let selectedPreset = $state(null);
  let apiKeyInput = $state('');
  let nameInput = $state('');
  let enabling = $state(false);
  let enableError = $state(null);

  let suggestedModels = $state([]);
  let loadingModels = $state(false);
  let appliedModels = $state(null);
  let applyingModels = $state(false);

  async function fetchPresets() {
    loading = true;
    error = null;
    try {
      const res = await authenticatedFetch('/api/presets');
      if (!res.ok) throw new Error(`Failed to fetch presets: HTTP ${res.status}`);
      presets = await res.json();
    } catch (e) {
      error = e.message;
      toast.error(e.message);
    } finally {
      loading = false;
    }
  }

  let freePresets = $derived(presets.filter(p => p.category === 0));
  let apikeyPresets = $derived(presets.filter(p => p.category === 1));
  let visiblePresets = $derived(activeTab === 'free' ? freePresets : apikeyPresets);

  function openEnableModal(preset) {
    selectedPreset = preset;
    apiKeyInput = '';
    nameInput = '';
    enableError = null;
    suggestedModels = [];
    appliedModels = null;
    enableModal = true;
  }

  function closeModal() {
    enableModal = false;
    selectedPreset = null;
  }

  async function handleEnable() {
    if (!selectedPreset) return;
    enabling = true;
    enableError = null;

    const payload = {};
    if (selectedPreset.apiKeyRequired) {
      if (!apiKeyInput.trim()) {
        enableError = 'API key is required';
        enabling = false;
        return;
      }
      payload.apiKey = apiKeyInput.trim();
    }
    if (nameInput.trim()) payload.name = nameInput.trim();

    try {
      const res = await authenticatedFetch(`/api/presets/${selectedPreset.id}/enable`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });

      if (!res.ok) {
        const errData = await res.json().catch(() => ({}));
        throw new Error(errData.error || `HTTP ${res.status}`);
      }

      const provider = await res.json();
      toast.success(`Provider '${provider.id}' enabled`);
      window.dispatchEvent(new CustomEvent('providersChanged'));

      if (selectedPreset.modelsUrl) {
        fetchSuggestedModels(selectedPreset, provider.id);
      } else {
        closeModal();
        await fetchPresets();
      }
    } catch (e) {
      enableError = e.message;
    } finally {
      enabling = false;
    }
  }

  async function enableFreePreset(preset) {
    enabling = true;
    try {
      const res = await authenticatedFetch(`/api/presets/${preset.id}/enable`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({})
      });

      if (!res.ok) {
        const errData = await res.json().catch(() => ({}));
        throw new Error(errData.error || `HTTP ${res.status}`);
      }

      const provider = await res.json();
      toast.success(`Provider '${provider.id}' enabled`);
      window.dispatchEvent(new CustomEvent('providersChanged'));

      if (preset.modelsUrl) {
        selectedPreset = preset;
        appliedModels = provider.id;
        enableModal = true;
        fetchSuggestedModels(preset, provider.id);
      }
      await fetchPresets();
    } catch (e) {
      toast.error(`Failed to enable: ${e.message}`);
    } finally {
      enabling = false;
    }
  }

  async function fetchSuggestedModels(preset, providerId) {
    loadingModels = true;
    suggestedModels = [];
    appliedModels = providerId;
    try {
      const res = await authenticatedFetch(`/api/presets/${preset.id}/models`);
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      suggestedModels = await res.json();
    } catch (e) {
      toast.error(`Failed to fetch suggested models: ${e.message}`);
    } finally {
      loadingModels = false;
    }
  }

  async function applySuggestedModels() {
    if (!appliedModels || suggestedModels.length === 0) return;
    applyingModels = true;
    try {
      const res = await authenticatedFetch(`/api/providers/${appliedModels}`);
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const provider = await res.json();

      const putRes = await authenticatedFetch(`/api/providers/${appliedModels}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          name: provider.name,
          baseUrl: provider.baseUrl,
          apiKey: '',
          enabled: provider.enabled,
          models: suggestedModels
        })
      });

      if (!putRes.ok) {
        const errData = await putRes.json().catch(() => ({}));
        throw new Error(errData.error || `HTTP ${putRes.status}`);
      }

      toast.success(`${suggestedModels.length} free models applied to '${appliedModels}'`);
      window.dispatchEvent(new CustomEvent('providersChanged'));
      closeModal();
      await fetchPresets();
    } catch (e) {
      toast.error(`Failed to apply models: ${e.message}`);
    } finally {
      applyingModels = false;
    }
  }

  function skipModels() {
    closeModal();
  }

  onMount(() => {
    fetchPresets();
  });
</script>

<div class="space-y-3.5">
  <div class="flex items-start justify-between gap-4">
    <div>
      <h1 class="page-h1">Preset providers</h1>
      <p class="mt-[7px] max-w-[62ch] text-[13.5px]" style="color: var(--muted);">One-click provider templates — enabling a preset creates a regular, fully editable provider.</p>
    </div>
  </div>

  <!-- Pill tabs per §6.1 (.tab) -->
  <div class="flex gap-1.5">
    <button
      class="h-7 rounded-full px-3 text-xs font-semibold transition-all"
      style="border: 1px solid {activeTab === 'free' ? 'var(--fg)' : 'var(--border)'}; background: {activeTab === 'free' ? 'var(--fg)' : 'var(--surface)'}; color: {activeTab === 'free' ? 'var(--bg)' : 'var(--muted)'};"
      onclick={() => activeTab = 'free'}
    >
      Free ({freePresets.length})
    </button>
    <button
      class="h-7 rounded-full px-3 text-xs font-semibold transition-all"
      style="border: 1px solid {activeTab === 'apikey' ? 'var(--fg)' : 'var(--border)'}; background: {activeTab === 'apikey' ? 'var(--fg)' : 'var(--surface)'}; color: {activeTab === 'apikey' ? 'var(--bg)' : 'var(--muted)'};"
      onclick={() => activeTab = 'apikey'}
    >
      API Key ({apikeyPresets.length})
    </button>
  </div>

  {#if loading}
    <div class="card-surface flex items-center justify-center p-12">
      <p class="text-[13.5px]" style="color: var(--muted);">Loading presets...</p>
    </div>
  {:else if error}
    <div class="rounded-xl p-4" style="background: var(--danger-soft); border: 1px solid color-mix(in srgb, var(--danger) 30%, transparent); color: var(--danger);">
      <p>Failed to load presets: {error}</p>
      <button class="mt-2 text-[13px] font-semibold underline" onclick={fetchPresets}>Retry</button>
    </div>
  {:else if visiblePresets.length === 0}
    <div class="card-surface p-12 text-center" style="color: var(--muted);">No presets in this category.</div>
  {:else}
    <div class="grid grid-cols-1 gap-3.5 md:grid-cols-2">
      {#each visiblePresets as preset (preset.id)}
        <div class="card-surface flex flex-col">
          <div class="flex items-center gap-3 p-4" style="border-bottom: 1px solid var(--border);">
            <div
              class="grid h-10 w-10 shrink-0 place-items-center rounded-[9px] text-sm font-bold text-white"
              style="background-color: {preset.display.colorHex}"
            >
              {preset.display.textIcon}
            </div>
            <div class="min-w-0 flex-1">
              <div class="flex items-center gap-2">
                <h3 class="panel-title truncate">{preset.name}</h3>
                {#if preset.category === 0}
                  <span class="status-pill status-ok">Free</span>
                {:else}
                  <span class="status-pill status-muted">API Key</span>
                {/if}
                {#if preset.connectedCount > 0}
                  <span class="status-pill" style="background: var(--surface-warm); border-color: var(--border); color: var(--muted);">Enabled ×{preset.connectedCount}</span>
                {/if}
              </div>
              <div class="mono mt-1 truncate text-xs" style="color: var(--meta);">{preset.baseUrl}</div>
            </div>
          </div>
          <div class="flex-1 space-y-2 p-4">
            {#if preset.display.notice}
              <p class="text-[13px]" style="color: var(--muted);">{preset.display.notice}</p>
            {/if}
            <div class="flex flex-wrap gap-1.5 text-xs">
              {#if preset.display.websiteUrl}
                <a href={preset.display.websiteUrl} target="_blank" rel="noopener" class="font-semibold">Website</a>
              {/if}
              {#if preset.display.apiKeyUrl}
                <span style="color: var(--meta);">·</span>
                <a href={preset.display.apiKeyUrl} target="_blank" rel="noopener" class="font-semibold">Get API key</a>
              {/if}
            </div>
          </div>
          <div class="p-4 pt-2" style="border-top: 1px solid var(--border);">
            {#if preset.category === 0}
              <Button class="w-full" onclick={() => enableFreePreset(preset)} disabled={enabling}>
                {#if enabling}<Loader2 class="mr-1.5 h-4 w-4 animate-spin" />{/if}
                {preset.connectedCount > 0 ? 'Enable Another' : 'Enable'}
              </Button>
            {:else}
              <Button class="w-full" variant="outline" onclick={() => openEnableModal(preset)}>
                <Key class="mr-1.5 h-4 w-4" />
                {preset.connectedCount > 0 ? 'Add Another Key' : 'Enable'}
              </Button>
            {/if}
          </div>
        </div>
      {/each}
    </div>
  {/if}
</div>

<Dialog.Root bind:open={enableModal}>
  <Dialog.Content class="sm:max-w-[520px]">
    <Dialog.Header>
      <Dialog.Title>
        {#if suggestedModels.length > 0 || (selectedPreset?.modelsUrl && appliedModels)}
          Suggested Free Models
        {:else if selectedPreset}
          Enable {selectedPreset.name}
        {/if}
      </Dialog.Title>
      <Dialog.Description>
        {#if suggestedModels.length > 0 || (selectedPreset?.modelsUrl && appliedModels)}
          These free models were found for this provider. Apply them or skip.
        {:else}
          Enter your API key to connect this provider. The key is validated before saving.
        {/if}
      </Dialog.Description>
    </Dialog.Header>

    {#if selectedPreset && !(suggestedModels.length > 0 || appliedModels)}
      <form onsubmit={(e) => { e.preventDefault(); handleEnable(); }} class="space-y-4 py-4">
        <div class="grid gap-2">
          <Label for="apiKey">API Key</Label>
          <Input
            id="apiKey"
            type="password"
            bind:value={apiKeyInput}
            placeholder="sk-..."
            required
            autofocus
          />
          {#if selectedPreset.display.apiKeyUrl}
            <a href={selectedPreset.display.apiKeyUrl} target="_blank" rel="noopener" class="text-xs font-semibold">
              Get an API key from {selectedPreset.name}
            </a>
          {/if}
        </div>

        <div class="grid gap-2">
          <Label for="presetName">Provider Name (optional)</Label>
          <Input id="presetName" bind:value={nameInput} placeholder={selectedPreset.name} />
        </div>

        {#if enableError}
          <p class="text-[13px]" style="color: var(--danger);">{enableError}</p>
        {/if}

        <Dialog.Footer class="pt-2">
          <Button variant="outline" type="button" onclick={closeModal}>Cancel</Button>
          <Button type="submit" disabled={enabling}>
            {#if enabling}<Loader2 class="mr-1.5 h-4 w-4 animate-spin" />{/if}
            {enabling ? 'Validating...' : 'Test & Enable'}
          </Button>
        </Dialog.Footer>
      </form>
    {:else}
      <div class="space-y-4 py-4">
        {#if loadingModels}
          <div class="flex items-center gap-2 py-4 text-[13px]" style="color: var(--muted);">
            <Loader2 class="h-4 w-4 animate-spin" /> Fetching free models...
          </div>
        {:else if suggestedModels.length > 0}
          <div class="flex max-h-48 flex-wrap gap-1.5 overflow-y-auto">
            {#each suggestedModels as model}
              <span class="mono rounded-full px-2.5 py-1 text-[11px] font-semibold" style="background: var(--surface-warm); border: 1px solid var(--border); color: var(--fg-2);">{model}</span>
            {/each}
          </div>
        {:else}
          <p class="text-[13px]" style="color: var(--muted);">No free models found.</p>
        {/if}

        <Dialog.Footer class="pt-2">
          <Button variant="outline" type="button" onclick={skipModels}>Skip</Button>
          <Button onclick={applySuggestedModels} disabled={applyingModels || loadingModels || suggestedModels.length === 0}>
            {#if applyingModels}<Loader2 class="mr-1.5 h-4 w-4 animate-spin" />{/if}
            {applyingModels ? 'Applying...' : `Use These ${suggestedModels.length} Models`}
          </Button>
        </Dialog.Footer>
      </div>
    {/if}
  </Dialog.Content>
</Dialog.Root>
