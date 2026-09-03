<script>
  import { toast } from 'svelte-sonner';
  import { Button } from '$lib/components/ui/button';
  import { Input } from '$lib/components/ui/input';
  import { Label } from '$lib/components/ui/label';
  import * as Popover from '$lib/components/ui/popover';
  import { KeyRound } from '@lucide/svelte';
  import { loadApiKey, saveApiKey, clearApiKey } from '$lib/persist.js';

  let apiKey = $state('');
  let isOpen = $state(false);

  function loadKey() {
    const stored = loadApiKey();
    apiKey = stored || '';
  }

  function handleSave() {
    if (!apiKey || !apiKey.trim()) {
      toast.error('API key cannot be empty');
      return;
    }
    saveApiKey(apiKey.trim());
    toast.success('API key saved', {
      description: 'Management API calls will now include authentication'
    });
    isOpen = false;
  }

  function handleClear() {
    clearApiKey();
    apiKey = '';
    toast.info('API key cleared', {
      description: 'You will need to re-enter it for remote access'
    });
  }

  function handleOpenChange(open) {
    isOpen = open;
    if (open) {
      loadKey();
    }
  }
</script>

<Popover.Root open={isOpen} onOpenChange={handleOpenChange}>
  <Popover.Trigger>
    <Button variant="ghost" size="icon" class="h-9 w-9">
      <KeyRound class="h-4 w-4" />
      <span class="sr-only">Set API Key</span>
    </Button>
  </Popover.Trigger>
  <Popover.Content class="w-80">
    <div class="space-y-4">
      <div class="space-y-2">
        <h4 class="font-medium leading-none">Management API Key</h4>
        <p class="text-sm text-muted-foreground">
          Required for remote dashboard access. Leave empty when connecting via localhost.
        </p>
      </div>
      <div class="space-y-2">
        <Label for="api-key-input">API Key</Label>
        <Input
          id="api-key-input"
          type="password"
          placeholder="Enter your API key"
          bind:value={apiKey}
          onkeydown={(e) => {
            if (e.key === 'Enter') {
              handleSave();
            }
          }}
        />
      </div>
      <div class="flex gap-2">
        <Button onclick={handleSave} class="flex-1">Save</Button>
        <Button onclick={handleClear} variant="outline">Clear</Button>
      </div>
    </div>
  </Popover.Content>
</Popover.Root>
