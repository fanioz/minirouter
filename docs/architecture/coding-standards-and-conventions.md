# Coding Standards and Conventions

## Existing Standards Compliance
**Code Style:** C# standard formatting.
**Linting Rules:** Default .NET analyzers.
**Testing Patterns:** xUnit.
**Documentation Style:** Markdown.

## Enhancement-Specific Standards
- **React Standards:** Functional components, Hooks, strict TypeScript typing.
- **CSS Standards:** Exclusively use Tailwind CSS classes. No custom CSS files unless strictly necessary.

## Critical Integration Rules
- **Existing API Compatibility:** The SPA must never rely on API endpoints not present in the backend.
- **Database Integration:** The SPA must never read `providers.json` directly.
- **Error Handling:** Graceful UI degradation if the backend is unreachable.
- **Logging Consistency:** Standard browser console output.
