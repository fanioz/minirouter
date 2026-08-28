<script>
  import { onMount } from 'svelte';
  import { toast } from 'svelte-sonner';
  import { Button } from '$lib/components/ui/button';
  import { Input } from '$lib/components/ui/input';
  import { Label } from '$lib/components/ui/label';
  import { Plus, Trash2, Key, Copy, Check, Power, PowerOff, Edit2, X } from 'lucide-svelte';

  let apiKeys = $state([]);
  let isLoading = $state(true);

  // Create state
  let isCreating = $state(false);
  let newKeyName = $state('');
  let newlyCreatedKey = $state(null);
  let copied = $state(false);

  // Edit state
  let editingId = $state(null);
  let editName = $state('');

  onMount(async () => {
    await loadApiKeys();
  });

  async function loadApiKeys() {
    try {
      isLoading = true;
      const res = await fetch('/api/keys');
      if (res.ok) {
        apiKeys = await res.json();
      } else {
        toast.error('Failed to load API keys');
      }
    } catch (e) {
      toast.error('Error connecting to backend');
    } finally {
      isLoading = false;
    }
  }

  async function createKey() {
    if (!newKeyName.trim()) {
      toast.error('Key name is required');
      return;
    }

    try {
      const res = await fetch('/api/keys', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name: newKeyName })
      });

      if (res.ok) {
        const newKey = await res.json();
        newlyCreatedKey = newKey;
        newKeyName = '';
        isCreating = false;
        await loadApiKeys();
        toast.success('API Key created successfully');
      } else {
        toast.error('Failed to create API key');
      }
    } catch (e) {
      toast.error('Error creating key');
    }
  }

  async function deleteKey(id) {
    if (!confirm('Are you sure you want to delete this key? This action cannot be undone.')) {
      return;
    }

    try {
      const res = await fetch(`/api/keys/${id}`, {
        method: 'DELETE'
      });

      if (res.ok) {
        apiKeys = apiKeys.filter(k => k.id !== id);
        toast.success('API Key deleted');
      } else {
        toast.error('Failed to delete API key');
      }
    } catch (e) {
      toast.error('Error deleting key');
    }
  }

  async function toggleStatus(key) {
    try {
      const res = await fetch(`/api/keys/${key.id}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name: key.name, enabled: !key.enabled })
      });

      if (res.ok) {
        const updated = await res.json();
        const index = apiKeys.findIndex(k => k.id === key.id);
        if (index !== -1) {
          apiKeys[index] = updated;
        }
        toast.success(`Key ${updated.enabled ? 'enabled' : 'disabled'}`);
      } else {
        toast.error('Failed to update status');
      }
    } catch (e) {
      toast.error('Error updating key');
    }
  }

  async function saveEdit(key) {
    if (!editName.trim()) {
      toast.error('Name cannot be empty');
      return;
    }

    try {
      const res = await fetch(`/api/keys/${key.id}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name: editName, enabled: key.enabled })
      });

      if (res.ok) {
        const updated = await res.json();
        const index = apiKeys.findIndex(k => k.id === key.id);
        if (index !== -1) {
          apiKeys[index] = updated;
        }
        editingId = null;
        toast.success('Key renamed');
      } else {
        toast.error('Failed to update name');
      }
    } catch (e) {
      toast.error('Error updating key');
    }
  }

  function startEdit(key) {
    editingId = key.id;
    editName = key.name;
  }

  function cancelEdit() {
    editingId = null;
    editName = '';
  }

  function copyToClipboard(text) {
    navigator.clipboard.writeText(text);
    copied = true;
    setTimeout(() => { copied = false; }, 2000);
    toast.success('Copied to clipboard');
  }

  function formatDate(dateStr) {
    if (!dateStr) return 'Never';
    return new Date(dateStr).toLocaleString();
  }
</script>

<div class="flex flex-col gap-3.5">
  <div class="flex items-start justify-between gap-4">
    <div>
      <h1 class="page-h1">API keys</h1>
      <p class="mt-[7px] max-w-[62ch] text-[13.5px]" style="color: var(--muted);">Access tokens for the proxy endpoints. Plaintext keys are shown once at creation.</p>
    </div>
    <Button onclick={() => isCreating = true}>
      <Plus class="mr-1.5 h-4 w-4" /> Create New Key
    </Button>
  </div>

  {#if isCreating}
    <div class="card-surface max-w-md p-5">
      <h3 class="panel-title mb-4">Create New API Key</h3>
      <div class="space-y-4">
        <div class="space-y-2">
          <Label for="keyName">Key Name</Label>
          <Input
            id="keyName"
            bind:value={newKeyName}
            placeholder="e.g. Production Frontend"
            onkeydown={(e) => e.key === 'Enter' && createKey()}
          />
        </div>
        <div class="flex justify-end gap-2">
          <Button variant="ghost" onclick={() => { isCreating = false; newKeyName = ''; }}>Cancel</Button>
          <Button onclick={createKey} disabled={!newKeyName.trim()}>Create</Button>
        </div>
      </div>
    </div>
  {/if}

  {#if newlyCreatedKey}
    <div class="rounded-xl p-5" style="background: var(--success-soft); border: 1px solid color-mix(in srgb, var(--success) 30%, transparent);">
      <div class="flex items-start gap-4">
        <div class="grid h-10 w-10 shrink-0 place-items-center rounded-full" style="background: var(--success); color: white;">
          <Key class="h-5 w-5" />
        </div>
        <div class="min-w-0 flex-1">
          <h3 class="panel-title" style="color: var(--success);">API Key Created Successfully</h3>
          <p class="mt-1 text-[13px]" style="color: var(--muted);">
            Please copy this key and store it securely. For security reasons, <strong>you will not be able to view it again</strong>.
          </p>
          <div class="mt-3 flex items-center gap-2">
            <code class="mono flex-1 rounded-lg border border-border bg-background px-3 py-2 text-[13px] break-all">{newlyCreatedKey.plaintextKey}</code>
            <Button variant="secondary" class="shrink-0" onclick={() => copyToClipboard(newlyCreatedKey.plaintextKey)}>
              {#if copied}
                <Check class="mr-1.5 h-4 w-4" /> Copied
              {:else}
                <Copy class="mr-1.5 h-4 w-4" /> Copy
              {/if}
            </Button>
          </div>
          <button class="mt-3 text-[13px] font-semibold underline" style="color: var(--success);" onclick={() => newlyCreatedKey = null}>
            I have saved this key
          </button>
        </div>
      </div>
    </div>
  {/if}

  <div class="card-surface">
    {#if isLoading}
      <div class="flex flex-col items-center p-10">
        <div class="mb-4 h-8 w-8 animate-spin rounded-full border-4" style="border-color: var(--accent); border-top-color: transparent;"></div>
        <p class="text-[13.5px]" style="color: var(--muted);">Loading API keys...</p>
      </div>
    {:else if apiKeys.length === 0}
      <div class="flex flex-col items-center p-12">
        <Key class="mb-3 h-10 w-10" style="color: var(--meta); opacity: .35;" />
        <h3 class="text-[13.5px] font-bold">No API keys found</h3>
        <p class="mt-1 text-xs" style="color: var(--muted);">Create an API key to securely access the proxy endpoints.</p>
      </div>
    {:else}
      <div class="overflow-x-auto">
        <table class="w-full text-left">
          <thead>
            <tr>
              <th class="caps-label px-4 py-2.5" style="background: var(--surface-head); border-bottom: 1px solid var(--border);">Name</th>
              <th class="caps-label px-3 py-2.5" style="background: var(--surface-head); border-bottom: 1px solid var(--border);">Key Prefix</th>
              <th class="caps-label px-3 py-2.5" style="background: var(--surface-head); border-bottom: 1px solid var(--border);">Status</th>
              <th class="caps-label px-3 py-2.5" style="background: var(--surface-head); border-bottom: 1px solid var(--border);">Created</th>
              <th class="caps-label px-3 py-2.5" style="background: var(--surface-head); border-bottom: 1px solid var(--border);">Last Used</th>
              <th class="caps-label px-4 py-2.5 text-right" style="background: var(--surface-head); border-bottom: 1px solid var(--border);">Actions</th>
            </tr>
          </thead>
          <tbody>
            {#each apiKeys as key}
              <tr class="group transition-colors hover:bg-accent" style="border-bottom: 1px solid var(--row-border);">
                <td class="px-4 py-3">
                  {#if editingId === key.id}
                    <div class="flex items-center gap-2">
                      <Input bind:value={editName} class="h-7 w-44 text-xs" onkeydown={(e) => e.key === 'Enter' && saveEdit(key)} />
                      <button class="transition-colors" style="color: var(--success);" onclick={() => saveEdit(key)} aria-label="Save name"><Check class="h-4 w-4" /></button>
                      <button class="transition-colors" style="color: var(--muted);" onclick={cancelEdit} aria-label="Cancel edit"><X class="h-4 w-4" /></button>
                    </div>
                  {:else}
                    <div class="flex items-center gap-2">
                      <span class="text-[13px] font-bold" style="letter-spacing: -.01em;">{key.name}</span>
                      <button onclick={() => startEdit(key)} class="opacity-0 transition-opacity group-hover:opacity-100 focus:opacity-100" style="color: var(--muted);" title="Rename" aria-label="Rename key">
                        <Edit2 class="h-3 w-3" />
                      </button>
                    </div>
                  {/if}
                </td>
                <td class="mono px-3 py-3 text-xs" style="color: var(--muted);">
                  <div class="flex items-center gap-2" title={"ID: " + key.id}>
                    <span>{key.keyPrefix}...</span>
                    <button onclick={() => copyToClipboard(key.id)} class="transition-colors hover:text-foreground" style="color: var(--muted);" title="Copy Key ID" aria-label="Copy key ID">
                      <Copy class="h-3 w-3" />
                    </button>
                  </div>
                </td>
                <td class="px-3 py-3">
                  {#if key.enabled}
                    <span class="status-pill status-ok">Active</span>
                  {:else}
                    <span class="status-pill status-muted">Disabled</span>
                  {/if}
                </td>
                <td class="px-3 py-3 text-xs" style="color: var(--muted);">{formatDate(key.createdAt)}</td>
                <td class="px-3 py-3 text-xs" style="color: var(--muted);">{formatDate(key.lastUsedAt)}</td>
                <td class="px-4 py-3">
                  <div class="flex items-center justify-end gap-1">
                    <button onclick={() => toggleStatus(key)} class="grid h-8 w-8 place-items-center rounded-[9px] transition-colors hover:bg-accent hover:text-foreground" style="color: var(--muted);" title={key.enabled ? "Disable Key" : "Enable Key"} aria-label={key.enabled ? "Disable key" : "Enable key"}>
                      {#if key.enabled}
                        <PowerOff class="h-4 w-4" />
                      {:else}
                        <Power class="h-4 w-4" />
                      {/if}
                    </button>
                    <button onclick={() => deleteKey(key.id)} class="grid h-8 w-8 place-items-center rounded-[9px] transition-colors hover:bg-danger-soft hover:text-destructive" style="color: var(--muted);" title="Delete Key" aria-label="Delete key">
                      <Trash2 class="h-4 w-4" />
                    </button>
                  </div>
                </td>
              </tr>
            {/each}
          </tbody>
        </table>
      </div>
    {/if}
  </div>
</div>
