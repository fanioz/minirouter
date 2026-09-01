<script>
  import { onMount } from 'svelte';
  import { SendHorizontal, Loader2, Bot, User, Trash2 } from 'lucide-svelte';
  import { toast } from 'svelte-sonner';

  /** @typedef {Object} ModelOption
   * @property {string} value - "providerId/modelId"
   * @property {string} label - "model-name — Provider Name"
   * @property {string} modelId - bare model id
   * @property {string} circuitStatus - "healthy" | "degraded" | "open"
   */

  /** @typedef {Object} ProviderGroup
   * @property {string} providerId
   * @property {string} providerName
   * @property {string | null} error
   * @property {ModelOption[]} models
   */

  let messages = $state([]);
  let inputMessage = $state('');
  let isGenerating = $state(false);
  let selectedModel = $state('');
  let apiKey = $state('');
  let availableModels = $state(/** @type {ProviderGroup[]} */ []);
  let hasWildcard = $state(false);
  let chatContainer;

  /**
   * Returns emoji prefix for circuit status
   * @param {string} status - "healthy" | "degraded" | "open"
   * @returns {string}
   */
  function circuitDot(status) {
    if (status === 'open') return '🔴 ';
    if (status === 'degraded') return '🟡 ';
    return '🟢 '; // healthy or unknown
  }

  onMount(async () => {
    try {
      const res = await fetch('/models');
      if (res.ok) {
        const data = await res.json();
        const groups = [];
        let wc = false;

        for (const provider of data) {
          const group = {
            providerId: provider.providerId,
            providerName: provider.providerName,
            error: provider.error ?? null,
            models: []
          };

          if (provider.models && Array.isArray(provider.models)) {
            if (provider.models.length === 0) wc = true;
            for (const m of provider.models) {
              if (!m.id) continue;
              const value = `${provider.providerId}/${m.id}`;
              group.models.push({
                value,
                label: `${m.id} — ${provider.providerName}`,
                modelId: m.id,
                circuitStatus: m.circuitStatus ?? 'healthy'
              });
            }
          } else {
            wc = true;
          }

          groups.push(group);
        }

        hasWildcard = wc;
        availableModels = groups;

        // Select first model from first group if not already selected
        const firstModel = groups.flatMap(g => g.models)[0];
        if (firstModel && !selectedModel) {
          selectedModel = firstModel.value;
        }
      }
    } catch (e) {
      console.error('Failed to load models', e);
    }
  });

  function scrollToBottom() {
    setTimeout(() => {
      if (chatContainer) {
        chatContainer.scrollTop = chatContainer.scrollHeight;
      }
    }, 50);
  }

  function clearChat() {
    messages = [];
  }

  async function sendMessage() {
    if (!inputMessage.trim() || isGenerating) return;

    const userMsg = { role: 'user', content: inputMessage };
    messages = [...messages, userMsg];
    inputMessage = '';
    isGenerating = true;
    scrollToBottom();

    // Create a placeholder for assistant message
    messages = [...messages, { role: 'assistant', content: '' }];

    try {
      const headers = {
        'Content-Type': 'application/json'
      };
      if (apiKey) {
        headers['Authorization'] = `Bearer ${apiKey}`;
      }

      const response = await fetch('/v1/chat/completions', {
        method: 'POST',
        headers,
        body: JSON.stringify({
          model: selectedModel,
          messages: messages.slice(0, -1), // Send all except the empty assistant placeholder
          stream: true
        })
      });

      if (!response.ok) {
        const err = await response.text();
        messages[messages.length - 1].content = `Error: ${response.status} - ${err}`;
        isGenerating = false;
        scrollToBottom();
        return;
      }

      const reader = response.body.getReader();
      const decoder = new TextDecoder('utf-8');
      let done = false;

      while (!done) {
        const { value, done: readerDone } = await reader.read();
        done = readerDone;
        if (value) {
          const chunk = decoder.decode(value, { stream: true });
          const lines = chunk.split(/\r?\n+/).filter(line => line.trim() !== '');
          for (const line of lines) {
            if (line === 'data: [DONE]') {
              done = true;
              break;
            }
            if (line.startsWith('data: ')) {
              try {
                const data = JSON.parse(line.substring(6));
                if (data.choices && data.choices.length > 0 && data.choices[0].delta && data.choices[0].delta.content) {
                  messages[messages.length - 1].content += data.choices[0].delta.content;
                  messages = [...messages]; // trigger reactivity
                  scrollToBottom();
                }
              } catch (e) {
                // Ignore parse errors on incomplete chunks
              }
            }
          }
        }
      }
    } catch (error) {
      toast.error('Failed to communicate with API');
      messages[messages.length - 1].content += `\n\n[Connection Error: ${error.message}]`;
    } finally {
      isGenerating = false;
    }
  }

  function handleKeydown(e) {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      sendMessage();
    }
  }
</script>

<div class="card-surface flex h-[calc(100vh-14rem)] max-h-[800px] flex-col">
  <!-- Panel header per §6.2 -->
  <div class="flex items-center justify-between gap-4 p-4" style="border-bottom: 1px solid var(--border);">
    <div class="flex flex-1 items-start gap-4">
      <div>
        <label for="model-select" class="caps-label mb-1.5 block">Model</label>
        <select
          id="model-select"
          bind:value={selectedModel}
          class="h-9 rounded-full border border-border bg-surface px-3.5 text-[13px] font-semibold outline-none transition-colors"
          style="color: var(--fg);"
        >
          {#if availableModels.length === 0}
            <option value="auto">auto</option>
          {:else}
            {#each availableModels as model}
              <option value={model.value}>{model.label}</option>
            {/each}
          {/if}
        </select>
      </div>
      <div>
        <label for="api-key" class="caps-label mb-1.5 block">API Key <span class="normal-case" style="letter-spacing: 0;">(optional)</span></label>
        <input
          id="api-key"
          type="password"
          bind:value={apiKey}
          placeholder="sk-..."
          class="mono h-9 w-48 rounded-[10px] border border-border bg-surface px-3 text-[13px] outline-none transition-colors placeholder:text-meta focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50"
          style="color: var(--fg);"
        />
      </div>
    </div>
    <button
      onclick={clearChat}
      class="grid h-9 w-9 shrink-0 place-items-center rounded-[9px] transition-colors hover:bg-accent"
      style="color: var(--muted);"
      title="Clear Chat"
      aria-label="Clear chat"
    >
      <Trash2 class="h-[18px] w-[18px]" />
    </button>
  </div>

  <!-- Messages -->
  <div bind:this={chatContainer} class="flex-1 space-y-4 overflow-y-auto p-4">
    {#if messages.length === 0}
      <div class="flex h-full flex-col items-center justify-center">
        <Bot class="mb-3 h-10 w-10" style="color: var(--meta); opacity: .35;" />
        <p class="text-[13.5px]" style="color: var(--muted);">Send a message to start testing the proxy.</p>
        <p class="mono mt-1 text-xs" style="color: var(--meta);">stream: true • explicit routing: providerId/model</p>
      </div>
    {:else}
      {#each messages as msg}
        <div class="flex gap-3 {msg.role === 'user' ? 'justify-end' : 'justify-start'}">
          {#if msg.role === 'assistant'}
            <div class="grid h-8 w-8 shrink-0 place-items-center rounded-full" style="background: var(--surface-warm); border: 1px solid var(--border);">
              <Bot class="h-[18px] w-[18px]" style="color: var(--muted);" />
            </div>
          {/if}
          <div
            class="max-w-[80%] whitespace-pre-wrap break-words rounded-xl px-4 py-2.5 text-[13.5px] {msg.role === 'user' ? 'rounded-br-sm' : 'rounded-bl-sm'}"
            style="{msg.role === 'user'
              ? 'background: var(--fg); color: var(--bg);'
              : 'background: var(--surface-warm); border: 1px solid var(--border-soft); color: var(--fg);'}"
          >
            {msg.content}
            {#if msg.role === 'assistant' && msg.content === '' && isGenerating}
              <span class="ml-1 inline-block h-4 w-2 animate-pulse" style="background: var(--muted);"></span>
            {/if}
          </div>
          {#if msg.role === 'user'}
            <div class="grid h-8 w-8 shrink-0 place-items-center rounded-full" style="background: var(--surface-warm); border: 1px solid var(--border);">
              <User class="h-[18px] w-[18px]" style="color: var(--muted);" />
            </div>
          {/if}
        </div>
      {/each}
    {/if}
  </div>

  <!-- Input -->
  <div class="p-4" style="border-top: 1px solid var(--border);">
    <div class="flex gap-2">
      <textarea
        bind:value={inputMessage}
        onkeydown={handleKeydown}
        placeholder="Type a message... (Press Enter to send, Shift+Enter for newline)"
        class="min-h-[44px] max-h-32 flex-1 resize-none overflow-y-auto rounded-xl border border-border bg-surface px-3.5 py-2.5 text-[13.5px] outline-none transition-colors placeholder:text-meta focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50"
        style="color: var(--fg);"
        rows="1"
        disabled={isGenerating}
      ></textarea>
      <button
        onclick={sendMessage}
        disabled={!inputMessage.trim() || isGenerating}
        class="grid h-[44px] w-[44px] shrink-0 place-items-center rounded-full bg-primary text-white transition-colors hover:bg-primary-hover active:bg-primary-active disabled:pointer-events-none disabled:opacity-50"
        aria-label="Send message"
      >
        {#if isGenerating}
          <Loader2 class="h-5 w-5 animate-spin" />
        {:else}
          <SendHorizontal class="h-5 w-5" />
        {/if}
      </button>
    </div>
  </div>
</div>
