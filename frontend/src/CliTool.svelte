<script>
  import { onMount } from 'svelte';
  import { toast } from 'svelte-sonner';
  import { Copy, AlertTriangle, Info } from '@lucide/svelte';
  import { Button } from '$lib/components/ui/button';
  import { Input } from '$lib/components/ui/input';
  import { Label } from '$lib/components/ui/label';
  import { AGENTS, buildConfig, DEFAULT_EXAMPLE_MODEL } from '$lib/cliAgents.js';

  // Amber accents for the Codex warning (no --warning token in app.css).
  const WARN_FG = '#d97706';
  const WARN_BG = 'color-mix(in srgb, #f59e0b 12%, transparent)';
  const WARN_BORDER = 'color-mix(in srgb, #f59e0b 35%, transparent)';

  // Component state only — the key is never persisted (no localStorage).
  let baseUrl = $state(window.location.origin);
  let apiKey = $state('');
  let exampleModel = $state(DEFAULT_EXAMPLE_MODEL);

  // 'checking' | 'reachable' | 'unreachable'
  let health = $state('checking');

  onMount(async () => {
    try {
      const res = await fetch('/health');
      health = res.ok ? 'reachable' : 'unreachable';
    } catch {
      health = 'unreachable';
    }

    // Derive the first example `providerId/modelName` from /models.
    try {
      const res = await fetch('/models');
      if (res.ok) {
        const providers = await res.json();
        const first = Array.isArray(providers) ? providers[0] : null;
        const raw = first?.models?.[0];
        const name = typeof raw === 'string' ? raw : (raw?.name ?? raw?.model ?? raw?.id);
        if (first?.providerId && name) {
          exampleModel = `${first.providerId}/${name}`;
        }
      }
    } catch {
      // Keep the fallback example model.
    }
  });

  function copyAgent(agentId) {
    const blocks = buildConfig(agentId, { baseUrl, apiKey, exampleModel });
    navigator.clipboard.writeText(blocks.map((b) => b.code).join('\n\n'));
    toast.success('Copied to clipboard');
  }

  let verifyCommand = $derived(`curl -s ${baseUrl}/models -H "Authorization: Bearer ${apiKey || '<key>'}" | head`);
</script>

<div class="flex flex-col gap-3.5">
  <div class="flex items-start justify-between gap-4">
    <div>
      <h1 class="page-h1">CLI tool</h1>
      <p class="mt-[7px] max-w-[62ch] text-[13.5px]" style="color: var(--muted);">
        Copy-paste configuration for CLI coding agents that talk to MiniRouter's OpenAI- and Anthropic-compatible endpoints.
      </p>
    </div>
  </div>

  <!-- Header card: endpoint + backend health -->
  <div class="card-surface p-5">
    <div class="flex items-center justify-between gap-3">
      <h3 class="panel-title">Endpoint</h3>
      {#if health === 'reachable'}
        <span class="status-pill status-ok">Backend reachable</span>
      {:else if health === 'unreachable'}
        <span class="status-pill status-fail">Backend unreachable</span>
      {:else}
        <span class="status-pill status-muted">Checking…</span>
      {/if}
    </div>
    <p class="mb-3 mt-1 text-[13px]" style="color: var(--muted);">
      Base URL the agents below will call. Most tools want the <code class="mono">/v1</code> suffix added; Claude Code wants the bare root.
    </p>
    <div class="max-w-xl space-y-2">
      <Label for="cli-endpoint">Base URL</Label>
      <Input id="cli-endpoint" bind:value={baseUrl} autocomplete="off" spellcheck="false" />
    </div>
  </div>

  <!-- API key card -->
  <div class="card-surface p-5">
    <h3 class="panel-title">API key</h3>
    <p class="mb-3 mt-1 max-w-[70ch] text-[13px]" style="color: var(--muted);">
      Plaintext keys are shown only once at creation (see the API Keys tab). Local/loopback requests work without a key. The key lives in this form's state only — it is never persisted or sent anywhere.
    </p>
    <div class="max-w-xl space-y-2">
      <Label for="cli-key">API key</Label>
      <Input id="cli-key" type="password" placeholder="mk-..." bind:value={apiKey} autocomplete="off" />
    </div>
  </div>

  <!-- Agent cards -->
  {#each AGENTS as agent (agent.id)}
    {@const blocks = buildConfig(agent.id, { baseUrl, apiKey, exampleModel })}
    <div class="card-surface p-5">
      <div class="flex items-center justify-between gap-3">
        <div class="flex items-center gap-2.5">
          <h3 class="panel-title">{agent.name}</h3>
          {#if agent.chip.tone === 'ok'}
            <span class="status-pill status-ok">{agent.chip.label}</span>
          {:else if agent.chip.tone === 'warn'}
            <span class="status-pill" style="background: {WARN_BG}; color: {WARN_FG}; border-color: {WARN_BORDER};">{agent.chip.label}</span>
          {:else}
            <span class="status-pill status-muted">{agent.chip.label}</span>
          {/if}
        </div>
        {#if blocks.length > 0}
          <Button variant="secondary" class="shrink-0" onclick={() => copyAgent(agent.id)}>
            <Copy class="mr-1.5 h-4 w-4" /> Copy
          </Button>
        {/if}
      </div>
      <p class="mt-1 text-[13px]" style="color: var(--muted);">{agent.description}</p>
      <p class="mt-1 text-[12px]" style="color: var(--muted);">
        MiniRouter requires <code class="mono">providerId/modelName</code> model ids — e.g. <code class="mono">{exampleModel}</code>.
      </p>

      {#if agent.bodyText}
        <div class="mt-3 flex items-start gap-2.5 rounded-[10px] p-3" style="background: var(--surface-warm); border: 1px dashed var(--border);">
          <Info class="mt-0.5 h-4 w-4 shrink-0" style="color: var(--muted);" />
          <p class="text-[13px]" style="color: var(--muted);">{agent.bodyText}</p>
        </div>
      {/if}

      {#each blocks as block}
        <div class="mt-3">
          <div class="caps-label mb-1.5">{block.label}</div>
          <pre class="mono overflow-x-auto rounded-[10px] p-3 text-[12px] leading-relaxed" style="background: var(--surface-warm); border: 1px solid var(--border);"><code>{block.code}</code></pre>
          {#if block.note}
            {#if agent.id === 'codex'}
              <div class="mt-2 flex items-start gap-2 rounded-[10px] p-3" style="background: {WARN_BG}; border: 1px solid {WARN_BORDER};">
                <AlertTriangle class="mt-0.5 h-4 w-4 shrink-0" style="color: {WARN_FG};" />
                <p class="text-[12.5px] font-medium" style="color: {WARN_FG};">{block.note}</p>
              </div>
            {:else}
              <p class="mt-2 text-[12px]" style="color: var(--muted);">{block.note}</p>
            {/if}
          {/if}
        </div>
      {/each}
    </div>
  {/each}

  <!-- Verify hint -->
  <div class="card-surface p-5">
    <h3 class="panel-title">Verify</h3>
    <p class="mb-3 mt-1 text-[13px]" style="color: var(--muted);">Confirm authentication and model discovery from the shell:</p>
    <pre class="mono overflow-x-auto rounded-[10px] p-3 text-[12px] leading-relaxed" style="background: var(--surface-warm); border: 1px solid var(--border);"><code>{verifyCommand}</code></pre>
  </div>
</div>
