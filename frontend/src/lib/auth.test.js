import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { authenticatedFetch } from './auth.js';
import { loadApiKey } from './persist.js';
import { toast } from 'svelte-sonner';

vi.mock('./persist.js', () => ({
  loadApiKey: vi.fn()
}));

vi.mock('svelte-sonner', () => ({
  toast: {
    error: vi.fn()
  }
}));

describe('authenticatedFetch', () => {
  let originalFetch;

  beforeEach(() => {
    originalFetch = global.fetch;
    global.fetch = vi.fn();
    vi.clearAllMocks();
  });

  afterEach(() => {
    global.fetch = originalFetch;
  });

  it('attaches API key header when key is stored', async () => {
    const mockKey = 'test-api-key-12345';
    loadApiKey.mockReturnValue(mockKey);
    global.fetch.mockResolvedValue(new Response('{}', { status: 200 }));

    await authenticatedFetch('/api/providers');

    expect(global.fetch).toHaveBeenCalledWith(
      '/api/providers',
      expect.objectContaining({
        headers: expect.any(Headers)
      })
    );

    const callArgs = global.fetch.mock.calls[0];
    const headers = callArgs[1].headers;
    expect(headers.get('x-api-key')).toBe(mockKey);
  });

  it('works without API key when none is stored', async () => {
    loadApiKey.mockReturnValue(null);
    global.fetch.mockResolvedValue(new Response('{}', { status: 200 }));

    await authenticatedFetch('/api/logs');

    expect(global.fetch).toHaveBeenCalled();
    const callArgs = global.fetch.mock.calls[0];
    const headers = callArgs[1].headers;
    expect(headers.get('x-api-key')).toBeNull();
  });

  it('shows toast on 401 response', async () => {
    loadApiKey.mockReturnValue(null);
    global.fetch.mockResolvedValue(new Response('Unauthorized', { status: 401 }));

    const response = await authenticatedFetch('/api/providers');

    expect(response.status).toBe(401);
    expect(toast.error).toHaveBeenCalledWith(
      'Authentication required',
      expect.objectContaining({
        description: expect.stringContaining('key icon')
      })
    );
  });

  it('preserves existing headers', async () => {
    loadApiKey.mockReturnValue('key-abc');
    global.fetch.mockResolvedValue(new Response('{}', { status: 200 }));

    await authenticatedFetch('/api/providers', {
      headers: { 'Content-Type': 'application/json' }
    });

    const callArgs = global.fetch.mock.calls[0];
    const headers = callArgs[1].headers;
    expect(headers.get('x-api-key')).toBe('key-abc');
    expect(headers.get('Content-Type')).toBe('application/json');
  });

  it('passes through all fetch options', async () => {
    loadApiKey.mockReturnValue('key-xyz');
    global.fetch.mockResolvedValue(new Response('{}', { status: 201 }));

    await authenticatedFetch('/api/keys', {
      method: 'POST',
      body: JSON.stringify({ name: 'test' })
    });

    expect(global.fetch).toHaveBeenCalledWith(
      '/api/keys',
      expect.objectContaining({
        method: 'POST',
        body: expect.any(String)
      })
    );
  });
});
