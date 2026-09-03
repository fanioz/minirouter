<script>
  import { onMount } from 'svelte';
  import { toast } from 'svelte-sonner';
  import { Plus, Pencil, Trash2, ChevronDown, ChevronRight, X } from 'lucide-svelte';
  import * as Dialog from '$lib/components/ui/dialog';
  import { apiKeyStore } from '$lib/stores/apiKeyStore';
  import { authenticatedFetch } from '$lib/auth.js';

  let chains = $state([]);
  let loading = $state(true);
  let isModalOpen = $state(false);
  let modalMode = $state('create'); // 'create' | 'edit'
  let submitting = $state(false);
  let expandedChain = $state(null); // chain name string for expandable target list
  let validationError = $state('');

  let formData = $state({
    name: '',
    description: '',
    modelsText: '' // textarea: one "providerId/modelId" per line
  });

  async function fetchChains() {
    loading = true;
    try {
      const res = await authenticatedFetch('/api/model-chains', {
        headers: { 'X-Api-Key': $apiKeyStore }
      });
      if (!res.ok) throw new Error(`Failed to fetch: HTTP ${res.status}`);
      chains = await res.json();
    } catch (e) {
      toast.error(`Error fetching chains: ${e.message}`);
    } finally {
      loading = false;
    }
  }

  onMount(() => {
    fetchChains();
  });

  function openCreateModal() {
    modalMode = 'create';
    formData = { name: '', description: '', modelsText: '' };
    validationError = '';
    isModalOpen = true;
  }

  function openEditModal(chain) {
    modalMode = 'edit';
    formData = {
      name: chain.name,
      description: chain.description || '',
      modelsText: (chain.models || []).join('\n')
    };
    validationError = '';
    isModalOpen = true;
  }

  function toggleExpanded(name) {
    expandedChain = expandedChain === name ? null : name;
  }

  function validateModels(text) {
    const lines = text.split('\n').map(l => l.trim()).filter(l => l);
    const errors = [];
    for (const line of lines) {
      if (!line.includes('/')) {
        errors.push(`"${line}" must contain '/' (providerId/modelName)`);
      }
    }
    return { valid: errors.length === 0, errors };
  }

  async function handleSubmit() {
    validationError = '';
    const { valid, errors } = validateModels(formData.modelsText);
    if (!valid) {
      validationError = errors.join('; ');
      return;
    }
    if (!formData.name.trim()) {
      validationError = 'Chain name is required';
      return;
    }
    if (formData.name.includes('/')) {
      validationError = 'Chain name must not contain "/"';
      return;
    }

    submitting = true;
    try {
      const models = formData.modelsText.split('\n').map(l => l.trim()).filter(l => l);
      const body = {
        name: formData.name.trim(),
        description: formData.description.trim() || null,
        models
      };

      let res;
      if (modalMode === 'create') {
        res = await authenticatedFetch('/api/model-chains', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            'X-Api-Key': $apiKeyStore
          },
          body: JSON.stringify(body)
        });
      } else {
        res = await authenticatedFetch(`/api/model-chains/${formData.name}`, {
          method: 'PUT',
          headers: {
            'Content-Type': 'application/json',
            'X-Api-Key': $apiKeyStore
          },
          body: JSON.stringify({
            description: body.description,
            models: body.models
          })
        });
      }

      if (!res.ok) {
        const err = await res.json();
        throw new Error(err.detail || 'Operation failed');
      }

      toast.success(modalMode === 'create' ? 'Chain created' : 'Chain updated');
      isModalOpen = false;
      fetchChains();
    } catch (e) {
      toast.error(`Error: ${e.message}`);
    } finally {
      submitting = false;
    }
  }

  async function deleteChain(name) {
    if (!confirm(`Delete chain "${name}"? This cannot be undone.`)) return;

    try {
      const res = await authenticatedFetch(`/api/model-chains/${name}`, {
        method: 'DELETE',
        headers: { 'X-Api-Key': $apiKeyStore }
      });
      if (!res.ok) {
        const err = await res.json();
        throw new Error(err.detail || 'Delete failed');
      }
      toast.success('Chain deleted');
      fetchChains();
    } catch (e) {
      toast.error(`Error: ${e.message}`);
    }
  }

  function getTargetCount(chain) {
    return (chain.models || []).length;
  }
</script>

<div class="space-y-3.5">
  <div class="flex items-start justify-between gap-4">
    <div>
      <h1 class="page-h1">Model Chains</h1>
      <p class="mt-[7px] max-w-[62ch] text-[13.5px]" style="color: var(--muted);">
        Define named fallback sequences. A chain resolves to an ordered list of
        <span class="mono">providerId/modelId</span> targets executed as a waterfall.
      </p>
    </div>
    <button
      class="inline-flex h-9 shrink-0 items-center gap-2 rounded-full px-3.5 text-[13px] font-semibold transition-colors"
      style="border: 1px solid var(--border); background: var(--fg); color: var(--bg);"
      onclick={openCreateModal}
    >
      <Plus class="h-4 w-4" /> New Chain
    </button>
  </div>

  <div class="card-surface">
    {#if loading && chains.length === 0}
      <div class="flex items-center justify-center p-10">
        <p class="text-[13.5px]" style="color: var(--muted);">Loading chains...</p>
      </div>
    {:else if chains.length === 0}
      <div class="flex flex-col items-center justify-center p-12">
        <p class="text-[13.5px]" style="color: var(--muted);">No model chains defined.</p>
        <p class="mt-1 text-xs" style="color: var(--meta);">Create your first chain to define fallback sequences.</p>
        <button
          class="mt-4 inline-flex h-9 items-center gap-2 rounded-full px-4 text-[13px] font-semibold transition-colors"
          style="border: 1px solid var(--border); background: var(--surface); color: var(--fg);"
          onclick={openCreateModal}
        >
          <Plus class="h-4 w-4" /> Create Chain
        </button>
      </div>
    {:else}
      <div class="overflow-x-auto">
        <table class="w-full text-left">
          <thead>
            <tr>
              <th class="caps-label px-4 py-2.5" style="background: var(--surface-head); border-bottom: 1px solid var(--border);">Name</th>
              <th class="caps-label px-3 py-2.5" style="background: var(--surface-head); border-bottom: 1px solid var(--border);">Description</th>
              <th class="caps-label px-3 py-2.5" style="background: var(--surface-head); border-bottom: 1px solid var(--border);">Targets</th>
              <th class="caps-label px-4 py-2.5 text-right" style="background: var(--surface-head); border-bottom: 1px solid var(--border);">Actions</th>
            </tr>
          </thead>
          <tbody>
            {#each chains as chain}
              <tr class="transition-colors hover:bg-accent" style="border-bottom: 1px solid var(--row-border);">
                <td class="px-4 py-3">
                  <span class="mono font-medium" style="color: var(--fg);">{chain.name}</span>
                </td>
                <td class="px-3 py-3">
                  <span class="text-[13px]" style="color: var(--muted);">
                    {chain.description || '—'}
                  </span>
                </td>
                <td class="px-3 py-3">
                  <button
                    class="inline-flex items-center gap-1.5 text-[13px] font-medium transition-colors"
                    style="color: var(--fg);"
                    onclick={() => toggleExpanded(chain.name)}
                  >
                    {getTargetCount(chain)}
                    {#if expandedChain === chain.name}
                      <ChevronDown class="h-4 w-4" />
                    {:else}
                      <ChevronRight class="h-4 w-4" />
                    {/if}
                  </button>
                </td>
                <td class="px-4 py-3 text-right">
                  <div class="flex items-center justify-end gap-1">
                    <button
                      class="grid h-8 w-8 place-items-center rounded-md transition-colors hover:bg-accent"
                      style="color: var(--muted);"
                      onclick={() => openEditModal(chain)}
                      title="Edit chain"
                    >
                      <Pencil class="h-4 w-4" />
                    </button>
                    <button
                      class="grid h-8 w-8 place-items-center rounded-md transition-colors hover:bg-danger/10"
                      style="color: var(--danger);"
                      onclick={() => deleteChain(chain.name)}
                      title="Delete chain"
                    >
                      <Trash2 class="h-4 w-4" />
                    </button>
                  </div>
                </td>
              </tr>
              {#if expandedChain === chain.name}
                <tr style="background: var(--surface);">
                  <td colspan="4" class="px-8 py-3">
                    <div class="rounded-lg border border-dashed" style="border-color: var(--border);">
                      <div class="caps-label px-3 py-2 border-b" style="border-color: var(--border); background: var(--surface-head); font-size: 11px;">
                        Fallback Sequence
                      </div>
                      <div class="p-3 space-y-1.5">
                        {#each chain.models || [] as target, i}
                          <div class="flex items-center gap-2 text-[13px]" style="color: var(--fg);">
                            <span class="mono text-xs font-medium opacity-50" style="color: var(--meta);">{i + 1}</span>
                            <span class="mono">{target}</span>
                          </div>
                        {/each}
                      </div>
                    </div>
                  </td>
                </tr>
              {/if}
            {/each}
          </tbody>
        </table>
      </div>
    {/if}
  </div>
</div>

<!-- Create/Edit Modal -->
{#if isModalOpen}
  <div class="fixed inset-0 z-50 flex items-center justify-center">
    <!-- Backdrop -->
    <div
      class="absolute inset-0"
      style="background: color-mix(in srgb, var(--bg) 80%, transparent);"
      onclick={() => isModalOpen = false}
      role="button"
      tabindex="-1"
      onkeydown={(e) => e.key === 'Escape' && (isModalOpen = false)}
    ></div>

    <!-- Modal Content -->
    <div class="relative w-full max-w-lg rounded-xl border shadow-xl" style="background: var(--surface); border-color: var(--border);">
      <div class="flex items-center justify-between border-b px-5 py-3.5" style="border-color: var(--border);">
        <h2 class="text-[15px] font-semibold" style="color: var(--fg);">
          {modalMode === 'create' ? 'New Chain' : 'Edit Chain'}
        </h2>
        <button
          class="grid h-7 w-7 place-items-center rounded-md transition-colors hover:bg-accent"
          style="color: var(--muted);"
          onclick={() => isModalOpen = false}
        >
          <X class="h-4 w-4" />
        </button>
      </div>

      <div class="p-5 space-y-4">
        <!-- Name Field -->
        <div>
          <label for="chain-name" class="caps-label mb-1.5 block" style="color: var(--fg);">
            Chain Name
          </label>
          <input
            id="chain-name"
            type="text"
            bind:value={formData.name}
            placeholder="e.g., tier1"
            class="h-9 w-full rounded-lg border px-3 text-[13px] outline-none transition-colors"
            style="border-color: var(--border); background: var(--surface-warm); color: var(--fg);"
            disabled={modalMode === 'edit'}
          />
          {#if modalMode === 'edit'}
            <p class="mt-1 text-xs" style="color: var(--meta);">Name cannot be changed after creation.</p>
          {/if}
        </div>

        <!-- Description Field -->
        <div>
          <label for="chain-description" class="caps-label mb-1.5 block" style="color: var(--fg);">
            Description <span class="normal-case font-normal" style="color: var(--meta);">(optional)</span>
          </label>
          <input
            id="chain-description"
            type="text"
            bind:value={formData.description}
            placeholder="e.g., Production fallback chain"
            class="h-9 w-full rounded-lg border px-3 text-[13px] outline-none transition-colors"
            style="border-color: var(--border); background: var(--surface-warm); color: var(--fg);"
          />
        </div>

        <!-- Models Textarea -->
        <div>
          <label for="chain-models" class="caps-label mb-1.5 block" style="color: var(--fg);">
            Targets <span class="normal-case font-normal" style="color: var(--meta);">(one per line)</span>
          </label>
          <textarea
            id="chain-models"
            bind:value={formData.modelsText}
            placeholder="providerId/modelName&#10;e.g., openrouter/kimi-3.0"
            rows="6"
            class="w-full rounded-lg border p-3 text-[13px] outline-none transition-colors mono"
            style="border-color: var(--border); background: var(--surface-warm); color: var(--fg); resize: vertical;"
          ></textarea>
          <p class="mt-1 text-xs" style="color: var(--meta);">
            Format: <span class="mono">providerId/modelName</span> — each line is tried in order until one succeeds.
          </p>
        </div>

        <!-- Validation Error -->
        {#if validationError}
          <div class="rounded-lg p-3 text-[13px]" style="background: var(--danger-soft); border: 1px solid color-mix(in srgb, var(--danger) 30%, transparent); color: var(--danger);">
            {validationError}
          </div>
        {/if}
      </div>

      <div class="flex items-center justify-end gap-2 border-t px-5 py-3.5" style="border-color: var(--border);">
        <button
          class="h-9 rounded-full px-4 text-[13px] font-semibold transition-colors"
          style="border: 1px solid var(--border); background: var(--surface); color: var(--fg);"
          onclick={() => isModalOpen = false}
        >
          Cancel
        </button>
        <button
          class="h-9 rounded-full px-4 text-[13px] font-semibold transition-colors disabled:opacity-50"
          style="border: 1px solid var(--border); background: var(--fg); color: var(--bg);"
          onclick={handleSubmit}
          disabled={submitting}
        >
          {submitting ? 'Saving...' : (modalMode === 'create' ? 'Create Chain' : 'Save Changes')}
        </button>
      </div>
    </div>
  </div>
{/if}
