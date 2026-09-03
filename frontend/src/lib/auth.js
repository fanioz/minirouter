// Authenticated fetch helper for management API calls (Story 8.7 F-3).
//
// Attaches the persisted API key to every request. On 401, surfaces a toast
// pointing the operator to the key popover. All dashboard views should use
// this instead of raw fetch().

import { toast } from 'svelte-sonner';
import { loadApiKey } from './persist.js';

/**
 * Fetch wrapper that attaches the stored API key header.
 * 
 * @param {string} url - The URL to fetch
 * @param {RequestInit} [options={}] - Fetch options
 * @returns {Promise<Response>}
 */
export async function authenticatedFetch(url, options = {}) {
  const apiKey = loadApiKey();
  
  const headers = new Headers(options.headers || {});
  
  // Only attach persisted key if caller hasn't supplied their own x-api-key
  if (apiKey && !headers.has('x-api-key')) {
    headers.set('x-api-key', apiKey);
  }
  
  const response = await fetch(url, {
    ...options,
    headers
  });
  
  if (response.status === 401) {
    toast.error('Authentication required', {
      description: 'Click the key icon in the top bar to set your API key'
    });
  }
  
  return response;
}
