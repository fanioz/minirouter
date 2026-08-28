import { defineConfig, mergeConfig } from 'vitest/config';
import viteConfig from './vite.config.js';

export default mergeConfig(viteConfig, defineConfig({
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/setupTests.js'],
    globals: true,
    server: {
      deps: {
        inline: [/@testing-library\/svelte/]
      }
    }
  },
  resolve: {
    conditions: ['browser']
  }
}));
