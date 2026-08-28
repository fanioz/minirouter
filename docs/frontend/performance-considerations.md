# Performance Considerations

## Performance Goals
- **Page Load:** < 1s (Time to Interactive). Being a static SPA, it should load almost instantly.
- **Interaction Response:** < 100ms for UI state changes.

## Design Strategies
- Keep bundle size minimal by utilizing Tailwind and avoiding heavy libraries.
- Use Skeleton loaders instead of blocking spinners to make the UI feel faster during the API fetch.
