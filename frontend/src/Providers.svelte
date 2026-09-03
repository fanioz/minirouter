<script>
  import { onMount } from 'svelte';
  import { toast } from 'svelte-sonner';
  import { Button } from '$lib/components/ui/button';
  import { Input } from '$lib/components/ui/input';
  import { Label } from '$lib/components/ui/label';
  import { Checkbox } from '$lib/components/ui/checkbox';
  import { Switch } from '$lib/components/ui/switch';
  import { Badge } from '$lib/components/ui/badge';
  import * as Dialog from '$lib/components/ui/dialog';
  import { Plus, Pencil, Trash2 } from '@lucide/svelte';
  import { authenticatedFetch } from '$lib/auth.js';

  let providers = $state([]);
  let loading = $state(true);
  let error = $state(null);

  // Modal State
  let isModalOpen = $state(false);
  let modalMode = $state('create');
  let submitting = $state(false);
  let testingConnection = $state(false);

  let formData = $state({
    id: '',
    name: '',
    baseUrl: '',
    apiKey: '',
    models: '',
    enabled: true,
    supportsStreamOptions: null,
    reportsStreamUsage: null,
    presetId: null
  });

  async function fetchProviders() {
    loading = true;
    error = null;
    try {
      const res = await authenticatedFetch('/api/providers');
      if (!res.ok) throw new Error(`Failed to fetch: HTTP ${res.status}`);
      providers = await res.json();
    } catch (e) {
      error = e.message;
      toast.error(e.message);
    } finally {
      loading = false;
    }
  }

  function openCreateModal() {
    modalMode = 'create';
    formData = { id: '', name: '', baseUrl: '', apiKey: '', models: '', enabled: true, supportsStreamOptions: null, reportsStreamUsage: null, presetId: null };
    isModalOpen = true;
  }

  function openEditModal(provider) {
    modalMode = 'edit';
    formData = {
      id: provider.id,
      name: provider.name || '',
      baseUrl: provider.baseUrl,
      apiKey: '',
      models: provider.models ? provider.models.join(', ') : '',
      enabled: provider.enabled,
      supportsStreamOptions: provider.supportsStreamOptions ?? null,
      reportsStreamUsage: provider.reportsStreamUsage ?? null,
      presetId: provider.presetId
    };
    isModalOpen = true;
  }

  function closeModal() {
    isModalOpen = false;
  }

  async function handleToggleStatus(provider) {
    try {
      const payload = {
        name: provider.name,
        baseUrl: provider.baseUrl,
        apiKey: '',
        enabled: !provider.enabled,
        models: provider.models
      };

      const res = await authenticatedFetch(`/api/providers/${provider.id}`, {
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

  async function handleSubmit(e) {
    e.preventDefault();
    submitting = true;

    const modelsArray = formData.models
      ? formData.models.split(',').map(s => s.trim()).filter(Boolean)
      : null;

    const payload = {
      name: formData.name,
      baseUrl: formData.baseUrl,
      enabled: formData.enabled,
      models: modelsArray,
      supportsStreamOptions: formData.supportsStreamOptions,
      reportsStreamUsage: formData.reportsStreamUsage
    };

    if (modalMode === 'create') {
      payload.id = formData.id;
    }

    if (formData.apiKey) {
      payload.apiKey = formData.apiKey;
    }

    try {
      let url = '/api/providers';
      let method = 'POST';

      if (modalMode === 'edit') {
        url = `/api/providers/${formData.id}`;
        method = 'PUT';
      }

      const res = await authenticatedFetch(url, {
        method,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });

      if (!res.ok) {
        const errData = await res.json().catch(() => ({}));
        throw new Error(errData.error || errData.message || `HTTP ${res.status}`);
      }

      toast.success(`Provider ${modalMode === 'create' ? 'created' : 'updated'} successfully`);
      closeModal();
      await fetchProviders();
    } catch (e) {
      toast.error(`Error: ${e.message}`);
    } finally {
      submitting = false;
    }
  }

  async function handleTestConnection() {
    if (!formData.baseUrl) {
      toast.error("Base URL is required to test connection.");
      return;
    }
    if (!formData.apiKey && modalMode === 'edit') {
      toast.error("Please re-enter the API key to test the connection.");
      return;
    }
    if (!formData.apiKey && modalMode === 'create') {
      toast.error("API key is required to test connection.");
      return;
    }

    testingConnection = true;
    try {
      const res = await authenticatedFetch('/api/providers/test', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ baseUrl: formData.baseUrl, apiKey: formData.apiKey })
      });

      if (!res.ok) {
        const errData = await res.json().catch(() => ({}));
        throw new Error(errData.error || errData.message || `HTTP ${res.status}`);
      }

      toast.success("Connection successful!");
    } catch (e) {
      toast.error(`Connection failed: ${e.message}`);
    } finally {
      testingConnection = false;
    }
  }

  async function handleDelete(id) {
    if (!confirm(`Are you sure you want to delete provider '${id}'?`)) return;

    try {
      const res = await authenticatedFetch(`/api/providers/${id}`, { method: 'DELETE' });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);

      toast.success(`Provider '${id}' deleted`);
      await fetchProviders();
    } catch (e) {
      toast.error(`Error deleting: ${e.message}`);
    }
  }

  onMount(() => {
    fetchProviders();
    window.addEventListener('providersChanged', fetchProviders);
    return () => window.removeEventListener('providersChanged', fetchProviders);
  });
</script>

<div class="space-y-3.5">
  <div class="flex items-start justify-between gap-4">
    <div>
      <h1 class="page-h1">Providers</h1>
      <p class="mt-[7px] max-w-[62ch] text-[13.5px]" style="color: var(--muted);">Manage routing targets. Requests use explicit routing — <span class="mono">providerId/model</span> — no retries.</p>
    </div>
    <Button onclick={openCreateModal}>
      <Plus class="mr-1.5 h-4 w-4" /> Add Provider
    </Button>
  </div>

  <div class="card-surface">
    {#if loading}
      <div class="flex items-center justify-center p-10">
        <p class="text-[13.5px]" style="color: var(--muted);">Loading providers...</p>
      </div>
    {:else if error}
      <div class="p-8 text-center" style="color: var(--danger);">
        <p>Failed to load data: {error}</p>
        <Button variant="outline" class="mt-4" onclick={fetchProviders}>Retry</Button>
      </div>
    {:else if providers.length === 0}
      <div class="flex flex-col items-center justify-center p-12">
        <p class="mb-4 text-[13.5px]" style="color: var(--muted);">No providers configured.</p>
        <Button variant="outline" onclick={openCreateModal}>Add Provider</Button>
      </div>
    {:else}
      <div class="overflow-x-auto">
        <table class="w-full text-left">
          <thead>
            <tr>
              <th class="caps-label px-4 py-2.5" style="background: var(--surface-head); border-bottom: 1px solid var(--border);">ID</th>
              <th class="caps-label px-3 py-2.5" style="background: var(--surface-head); border-bottom: 1px solid var(--border);">Name</th>
              <th class="caps-label px-3 py-2.5" style="background: var(--surface-head); border-bottom: 1px solid var(--border);">Base URL</th>
              <th class="caps-label px-3 py-2.5" style="background: var(--surface-head); border-bottom: 1px solid var(--border);">API Key</th>
              <th class="caps-label px-3 py-2.5" style="background: var(--surface-head); border-bottom: 1px solid var(--border);">Status</th>
              <th class="caps-label px-4 py-2.5 text-right" style="background: var(--surface-head); border-bottom: 1px solid var(--border);">Actions</th>
            </tr>
          </thead>
          <tbody>
            {#each providers as p}
              <tr class="transition-colors hover:bg-accent" style="border-bottom: 1px solid var(--row-border);">
                <td class="px-4 py-3">
                  <div class="flex items-center gap-2">
                    <b class="mono text-[13px]" style="letter-spacing: -.01em;">{p.id}</b>
                    {#if p.presetId}
                      <Badge variant="outline" class="text-[10px]">Preset</Badge>
                    {/if}
                  </div>
                </td>
                <td class="px-3 py-3 text-[13px]">{p.name || '-'}</td>
                <td class="mono max-w-[240px] truncate px-3 py-3 text-xs" style="color: var(--fg-2);" title={p.baseUrl}>{p.baseUrl}</td>
                <td class="mono px-3 py-3 text-xs" style="color: var(--muted);">{p.apiKeyMasked || '-'}</td>
                <td class="px-3 py-3">
                  {#if p.enabled}
                    <span class="status-pill status-ok">Live</span>
                  {:else}
                    <span class="status-pill status-muted">Disabled</span>
                  {/if}
                </td>
                <td class="px-4 py-3">
                  <div class="flex items-center justify-end gap-2">
                    <Switch
                      checked={p.enabled}
                      onCheckedChange={() => handleToggleStatus(p)}
                      aria-label="Toggle {p.id}"
                    />
                    <Button variant="ghost" size="icon" onclick={() => openEditModal(p)} title="Edit" aria-label="Edit {p.id}">
                      <Pencil class="h-4 w-4" />
                    </Button>
                    <Button variant="ghost" size="icon" class="text-destructive hover:bg-danger-soft hover:text-destructive" onclick={() => handleDelete(p.id)} title="Delete" aria-label="Delete {p.id}">
                      <Trash2 class="h-4 w-4" />
                    </Button>
                  </div>
                </td>
              </tr>
            {/each}
          </tbody>
        </table>
      </div>
      <div class="flex items-center gap-4 px-4 py-3 text-xs" style="border-top: 1px solid var(--row-border); color: var(--muted);">
        <span>{providers.length} provider{providers.length === 1 ? '' : 's'}</span>
        <span>management endpoints are unauthenticated — protect with an auth proxy if exposed publicly</span>
      </div>
    {/if}
  </div>
</div>

<Dialog.Root bind:open={isModalOpen}>
  <Dialog.Content class="sm:max-w-[500px]">
    <Dialog.Header>
      <Dialog.Title>{modalMode === 'create' ? 'Add New Provider' : 'Edit Provider'}</Dialog.Title>
      <Dialog.Description>
        Configure the details for this LLM API provider.
      </Dialog.Description>
      {#if modalMode === 'edit' && formData.presetId}
        <div class="mt-3 rounded-lg px-3 py-2 text-xs font-medium" style="background: var(--warn-soft); border: 1px solid color-mix(in srgb, var(--warn) 30%, transparent); color: var(--warn);">
          Created from the <strong>{formData.presetId}</strong> preset. Edits only affect this provider — the preset template is unchanged.
        </div>
      {/if}
    </Dialog.Header>
    <form onsubmit={handleSubmit} class="space-y-4 py-4">
      <div class="grid gap-2">
        <Label for="id">ID (Immutable)</Label>
        <Input
          id="id"
          bind:value={formData.id}
          placeholder="e.g. openai"
          required
          disabled={modalMode === 'edit'}
        />
      </div>

      <div class="grid gap-2">
        <Label for="name">Name</Label>
        <Input
          id="name"
          bind:value={formData.name}
          placeholder="e.g. OpenAI Prod"
          required
        />
      </div>

      <div class="grid gap-2">
        <Label for="baseUrl">Base URL</Label>
        <Input
          id="baseUrl"
          type="url"
          bind:value={formData.baseUrl}
          placeholder="https://api.openai.com/v1"
          required
        />
      </div>

      <div class="grid gap-2">
        <Label for="apiKey">API Key</Label>
        <Input
          id="apiKey"
          type="password"
          bind:value={formData.apiKey}
          placeholder={modalMode === 'edit' ? '(Leave blank to keep existing)' : 'sk-...'}
          required={modalMode === 'create'}
        />
      </div>

      <div class="grid gap-2">
        <Label for="models">Models (comma separated)</Label>
        <Input
          id="models"
          bind:value={formData.models}
          placeholder="gpt-4, gpt-3.5-turbo"
        />
      </div>

      <div class="flex items-center space-x-2 pt-2">
        <Checkbox id="enabled" bind:checked={formData.enabled} />
        <Label for="enabled" class="cursor-pointer">Provider Enabled</Label>
      </div>

      <div class="space-y-3 border-t pt-3" style="border-color: var(--border);">
        <p class="text-xs" style="color: var(--muted);">
          Compatibility workarounds — only change if this provider rejects <code>stream_options</code> or never returns usage data.
        </p>
        <div class="flex items-center space-x-2">
          <Checkbox id="supportsStreamOptions" checked={formData.supportsStreamOptions !== false} onCheckedChange={(v) => formData.supportsStreamOptions = v ? null : false} />
          <Label for="supportsStreamOptions" class="cursor-pointer text-sm">Accepts <code>stream_options</code> in requests</Label>
        </div>
        <div class="flex items-center space-x-2">
          <Checkbox id="reportsStreamUsage" checked={formData.reportsStreamUsage !== false} onCheckedChange={(v) => formData.reportsStreamUsage = v ? null : false} />
          <Label for="reportsStreamUsage" class="cursor-pointer text-sm">Reports usage in streaming responses</Label>
        </div>
      </div>

      <Dialog.Footer class="flex w-full items-center justify-between pt-4 sm:justify-between">
        <Button variant="secondary" type="button" onclick={handleTestConnection} disabled={testingConnection || submitting}>
          {testingConnection ? 'Testing...' : 'Test Connection'}
        </Button>
        <div class="space-x-2">
          <Button variant="outline" type="button" onclick={closeModal}>Cancel</Button>
          <Button type="submit" disabled={submitting || testingConnection}>
            {submitting ? 'Saving...' : 'Save Changes'}
          </Button>
        </div>
      </Dialog.Footer>
    </form>
  </Dialog.Content>
</Dialog.Root>
