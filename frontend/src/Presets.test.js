import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/svelte';
import Presets from './Presets.svelte';

vi.mock('mode-watcher', () => ({ ModeWatcher: vi.fn(), mode: { current: 'system' }, userPrefersMode: { current: 'system' }, setMode: vi.fn() }));

const mockPresets = [
  {
    id: 'opencode-free',
    name: 'OpenCode Free',
    category: 0,
    baseUrl: 'https://opencode.ai/zen',
    apiKeyRequired: false,
    display: { colorHex: '#E87040', textIcon: 'OC', websiteUrl: 'https://opencode.ai', notice: 'Free models' },
    connectedCount: 1,
    modelsUrl: 'https://opencode.ai/zen/v1/models'
  },
  {
    id: 'groq',
    name: 'Groq',
    category: 1,
    baseUrl: 'https://api.groq.com/openai',
    apiKeyRequired: true,
    display: { colorHex: '#F55036', textIcon: 'GQ', apiKeyUrl: 'https://console.groq.com/keys', notice: 'Fast inference' },
    connectedCount: 0,
    modelsUrl: null
  }
];

async function flush(ms = 150) {
  await new Promise(r => setTimeout(r, ms));
}

beforeEach(() => {
  vi.clearAllMocks();
  global.fetch = vi.fn().mockResolvedValue({ ok: true, json: async () => mockPresets });
});

describe('Presets - Initial Render', () => {
  it('renders the presets title and both tabs', async () => {
    render(Presets);
    await flush();

    expect(screen.getByText('Preset providers')).toBeInTheDocument();
    expect(screen.getByText('Free (1)')).toBeInTheDocument();
    expect(screen.getByText('API Key (1)')).toBeInTheDocument();
  });

  it('shows the free preset card with Enabled badge on the default tab', async () => {
    render(Presets);
    await flush();

    expect(screen.getByText('OpenCode Free')).toBeInTheDocument();
    expect(screen.getByText(/Enabled ×1/i)).toBeInTheDocument();
    expect(screen.getByText('Free')).toBeInTheDocument();
  });

  it('switches to the API Key tab and shows the apikey preset', async () => {
    render(Presets);
    await flush();

    fireEvent.click(screen.getByText('API Key (1)'));
    await flush();

    expect(screen.getByText('Groq')).toBeInTheDocument();
    expect(screen.getByText('API Key')).toBeInTheDocument();
  });
});

describe('Presets - Enable Modal', () => {
  it('opens the API key modal for an apikey preset', async () => {
    render(Presets);
    await flush();

    fireEvent.click(screen.getByText('API Key (1)'));
    await flush();
    fireEvent.click(screen.getByRole('button', { name: /enable/i }));
    await flush();

    expect(screen.getByLabelText(/api key/i)).toBeInTheDocument();
  });

  it('shows a validation error when enabling an apikey preset without a key', async () => {
    render(Presets);
    await flush();

    fireEvent.click(screen.getByText('API Key (1)'));
    await flush();
    fireEvent.click(screen.getByRole('button', { name: /enable/i }));
    await flush();

    const form = screen.getByText('Test & Enable').closest('form');
    fireEvent.submit(form);
    await flush();

    expect(screen.getByText(/api key is required/i)).toBeInTheDocument();
  });
});
