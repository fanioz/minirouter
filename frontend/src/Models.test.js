import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import Models from './Models.svelte';

describe('Models component', () => {
    afterEach(() => {
        cleanup();
    });

    it('renders the Models header', () => {
        const { getByText } = render(Models);
        expect(getByText('Models')).toBeInTheDocument();
    });

    it('renders loading state initially', () => {
        const { getByText } = render(Models);
        expect(getByText('Loading models...')).toBeInTheDocument();
    });
});
