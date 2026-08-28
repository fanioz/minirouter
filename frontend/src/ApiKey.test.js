import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import ApiKey from './ApiKey.svelte';

describe('ApiKey component', () => {
    afterEach(() => {
        cleanup();
    });

    it('renders the API Keys header', () => {
        const { getByText } = render(ApiKey);
        expect(getByText('API keys')).toBeInTheDocument();
    });

    it('renders the Create New Key button', () => {
        const { getByText } = render(ApiKey);
        expect(getByText('Create New Key')).toBeInTheDocument();
    });
});
