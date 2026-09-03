<script>
  import { onMount } from 'svelte';
  import { toast } from 'svelte-sonner';
  import { Copy, Check, Plug, AlertTriangle } from '@lucide/svelte';
  import { Button } from '$lib/components/ui/button';
  import { Badge } from '$lib/components/ui/badge';
  import * as Card from '$lib/components/ui/card';
  import { authenticatedFetch } from '$lib/auth.js';

  let authPassthrough = $state(false);
  let loading = $state(true);
  let copiedId = $state('');

  const baseUrl = $derived(window.location.origin);

  onMount(async () => {
    try {
      const res = await authenticatedFetch('/api/config/auth-passthrough');
      if (res.ok) {
        const data = await res.json();
        authPassthrough = data.enabled ?? false;
      }
    } catch {
      // Default to false if unreachable
    } finally {
      loading = false;
    }
  });

  async function copyText(id, text) {
    try {
      await navigator.clipboard.writeText(text);
      copiedId = id;
      toast.success('Copied to clipboard');
      setTimeout(() => { copiedId = ''; }, 2000);
    } catch {
      toast.error('Failed to copy');
    }
  }

  const claudeCodeCommands = $derived(
    `claude config set apiBaseUrl ${baseUrl}\nclaude config set apiKey any-value`
  );

  // Codex CLI ≥0.122 requires wire_api = "responses" and config.toml — not env vars.
  // The /v1/responses endpoint is tracked in Epic 11 / Story 11.1.
  const codexTomlConfig = $derived(
`model                = "providerId/modelName"
model_provider         = "minirouter"
model_context_window   = 128000

[model_providers.minirouter]
name     = "MiniRouter"
base_url = "${baseUrl}/v1"
env_key  = "MINIROUTER_API_KEY"
wire_api = "responses"`
  );
</script>

<div class="p-6 max-w-3xl mx-auto space-y-6">
  <div class="flex items-center justify-between">
    <div>
      <h1 class="text-2xl font-semibold">Integrations</h1>
      <p class="text-muted-foreground mt-1 text-sm">
        Point your AI coding tools at MiniRouter to route all LLM traffic through your configured providers.
      </p>
    </div>
    {#if !loading}
      <Badge variant={authPassthrough ? 'default' : 'outline'} class={authPassthrough ? 'bg-green-600 text-white' : ''}>
        AUTH_PASSTHROUGH: {authPassthrough ? 'ON' : 'OFF'}
      </Badge>
    {/if}
  </div>

  {#if authPassthrough}
    <div class="rounded-md border border-yellow-400 bg-yellow-50 dark:bg-yellow-950/30 px-4 py-3 text-sm text-yellow-800 dark:text-yellow-300">
      ⚠️ Auth passthrough is enabled — any API key value is accepted. Do not expose this port publicly.
    </div>
  {:else}
    <div class="rounded-md border px-4 py-3 text-sm text-muted-foreground">
      API key validation is active. Create a key in the <strong>API Keys</strong> tab and use it as the key value below, or set <code>AUTH_PASSTHROUGH=true</code> to skip validation.
    </div>
  {/if}

  <!-- Claude Code — fully supported -->
  <Card.Root>
    <Card.Header>
      <Card.Title class="flex items-center gap-2">
        <Plug class="h-5 w-5" />
        Claude Code
        <Badge variant="outline" class="ml-1 text-green-700 border-green-400 bg-green-50 dark:bg-green-950/30 dark:text-green-400">Supported</Badge>
      </Card.Title>
      <Card.Description>
        Routes requests through MiniRouter via the Anthropic Messages API (<code>/v1/messages</code>).
      </Card.Description>
    </Card.Header>
    <Card.Content class="space-y-3">
      <p class="text-sm text-muted-foreground">Run these commands in your project directory or globally:</p>
      <div class="relative rounded-md bg-muted font-mono text-sm p-4">
        <pre class="whitespace-pre-wrap">{claudeCodeCommands}</pre>
        <Button
          variant="ghost"
          size="sm"
          class="absolute top-2 right-2"
          onclick={() => copyText('claude', claudeCodeCommands)}
          aria-label="Copy Claude Code commands"
        >
          {#if copiedId === 'claude'}
            <Check class="h-4 w-4 text-green-500" />
          {:else}
            <Copy class="h-4 w-4" />
          {/if}
        </Button>
      </div>
      <p class="text-xs text-muted-foreground">
        Claude Code posts to <code>{baseUrl}/v1/messages</code>. MiniRouter translates Anthropic → OpenAI format and routes to your configured providers.
      </p>
    </Card.Content>
  </Card.Root>

  <!-- Codex CLI — needs Responses API (Epic 11) -->
  <Card.Root>
    <Card.Header>
      <Card.Title class="flex items-center gap-2">
        <Plug class="h-5 w-5" />
        Codex CLI
        <Badge variant="outline" class="ml-1 text-yellow-700 border-yellow-400 bg-yellow-50 dark:bg-yellow-950/30 dark:text-yellow-400">Coming soon</Badge>
      </Card.Title>
      <Card.Description>
        Requires the OpenAI Responses API (<code>POST /v1/responses</code>) — tracked in Epic 11.
      </Card.Description>
    </Card.Header>
    <Card.Content class="space-y-4">

      <!-- Blocker notice -->
      <div class="flex items-start gap-2.5 rounded-md border border-yellow-400 bg-yellow-50 dark:bg-yellow-950/30 px-4 py-3">
        <AlertTriangle class="mt-0.5 h-4 w-4 shrink-0 text-yellow-600 dark:text-yellow-400" />
        <div class="text-sm text-yellow-800 dark:text-yellow-300 space-y-1">
          <p class="font-medium">Responses API adapter not yet implemented</p>
          <p>
            Codex CLI v0.122+ dropped <code>wire_api = "chat"</code> and exclusively uses
            <code>wire_api = "responses"</code>, which POSTs to <code>/v1/responses</code>.
            MiniRouter currently implements <code>/v1/chat/completions</code> and <code>/v1/messages</code> only.
            Epic 11 / Story 11.1 adds the translation layer.
          </p>
        </div>
      </div>

      <!-- Config preview — ready to paste once 11.1 ships -->
      <div>
        <p class="text-sm text-muted-foreground mb-2">
          Once Epic 11 ships, add this to <code>~/.codex/config.toml</code>:
        </p>
        <div class="relative rounded-md bg-muted font-mono text-sm p-4 opacity-60">
          <pre class="whitespace-pre-wrap">{codexTomlConfig}</pre>
          <Button
            variant="ghost"
            size="sm"
            class="absolute top-2 right-2"
            onclick={() => copyText('codex', codexTomlConfig)}
            aria-label="Copy Codex config.toml"
          >
            {#if copiedId === 'codex'}
              <Check class="h-4 w-4 text-green-500" />
            {:else}
              <Copy class="h-4 w-4" />
            {/if}
          </Button>
        </div>
        <p class="text-xs text-muted-foreground mt-2">
          Replace <code>providerId/modelName</code> with a model from your <a href="#models" class="underline underline-offset-2">Models</a> tab, e.g. <code>deepseek/deepseek-chat</code>.
          Set <code>MINIROUTER_API_KEY</code> to any API key from the <a href="#apikey" class="underline underline-offset-2">API Keys</a> tab (or any value when <code>AUTH_PASSTHROUGH=true</code>).
        </p>
      </div>

    </Card.Content>
  </Card.Root>
</div>
