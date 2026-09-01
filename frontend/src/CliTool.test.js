import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/svelte';
import CliTool from './CliTool.svelte';
import { toast } from 'svelte-sonner';

vi.mock('svelte-sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

const mockProviders = [
  { providerId: 'openai-primary', providerName: 'OpenAI Primary', models: ['gpt-4o-mini', 'gpt-4.1'] }
];

async function flush(ms = 150) {
  await new Promise(r => setTimeout(r, ms));
}

beforeEach(() => {
  vi.clearAllMocks();
  // Per-endpoint fetch mock: /health, /models.
  global.fetch = vi.fn((url) => {
    const target = String(url);
    if (target.includes('/models')) {
      return Promise.resolve({ ok: true, json: async () => mockProviders });
    }
    if (target.includes('/health')) {
      return Promise.resolve({ ok: true, json: async () => ({}) });
    }
    return Promise.resolve({ ok: false, json: async () => ({}) });
  });
  navigator.clipboard = { writeText: vi.fn().mockResolvedValue() };
});

describe('CliTool - Agent Cards', () => {
  it('renders all five agent headings', async () => {
    render(CliTool);
    await flush();

    expect(screen.getByText('Claude Code')).toBeInTheDocument();
    expect(screen.getByText('Codex CLI')).toBeInTheDocument();
    expect(screen.getByText('OpenCode')).toBeInTheDocument();
    expect(screen.getByText('Aider')).toBeInTheDocument();
    expect(screen.getByText('Gemini CLI')).toBeInTheDocument();
  });

  it('defaults the endpoint input to window.location.origin', async () => {
    render(CliTool);
    await flush();

    expect(screen.getByLabelText(/base url/i)).toHaveValue(window.location.origin);
  });

  it('updates the Claude snippet with ANTHROPIC_AUTH_TOKEN when a key is typed', async () => {
    const { container } = render(CliTool);
    await flush();

    fireEvent.input(screen.getByLabelText(/api key/i), { target: { value: 'mk-test123' } });
    await flush();

    expect(container.textContent).toContain('ANTHROPIC_AUTH_TOKEN="mk-test123"');
  });

  it('shows the Codex /v1/responses warning and the Gemini not-supported text', async () => {
    render(CliTool);
    await flush();

    expect(screen.getByText(/POSTs to \/v1\/responses/)).toBeInTheDocument();
    expect(screen.getByText(/no custom OpenAI-compatible base URL/i)).toBeInTheDocument();
  });

  it('copies a snippet to the clipboard and toasts on Copy', async () => {
    render(CliTool);
    await flush();

    const copyButtons = screen.getAllByRole('button', { name: /copy/i });
    fireEvent.click(copyButtons[0]);
    await flush();

    expect(navigator.clipboard.writeText).toHaveBeenCalledTimes(1);
    expect(toast.success).toHaveBeenCalledWith('Copied to clipboard');
  });
});
