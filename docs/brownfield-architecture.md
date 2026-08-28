# MininRouter Brownfield Architecture Document

## Introduction

This document captures the CURRENT STATE of the MininRouter codebase, including technical debt, workarounds, and real-world patterns. It serves as a reference for AI agents working on enhancements.

### Document Scope

Comprehensive documentation of the entire system (Backend C# / Native AOT + Frontend Svelte).

### Change Log

| Date | Version | Description | Author |
|------|---------|-------------|--------|
| 2026-08-13 | 1.0 | Initial brownfield analysis | @architect (Aria) |

## Quick Reference - Key Files and Entry Points

### Critical Files for Understanding the System

- **Main Entry**: `Program.cs` - Handles the CLI parsing, HTTP server setup, Minimal APIs, and Dependency Injection.
- **Backend Services**: `Services/` (e.g., `ProviderService.cs`, `ProxyService.cs`, `LogService.cs`, `ApiKeyService.cs`)
- **Backend Models**: `Models/` folder
- **Frontend App**: `frontend/` folder with Svelte 5 + Vite configuration.
- **Build configuration**: `MininRouter.csproj` - Defines .NET 10 Native AOT, and includes a target `BuildFrontend` which runs `npm run build` inside `frontend/`.

## High Level Architecture

### Technical Summary

MininRouter acts as an intelligent proxy and router for LLM API providers (OpenAI, Anthropic). It accepts standard LLM completion requests and forwards them to various configured downstream providers based on API keys, circuit breakers, and load balancing logic.

### Actual Tech Stack

| Category | Technology | Version | Notes |
|----------|------------|---------|--------|
| Backend Runtime | .NET / C# | 10.0 | Native AOT (`PublishAot=true`) |
| Backend Framework| ASP.NET Core Minimal APIs | 10.0 | Built-in JSON serialization contexts for AOT |
| Database | SQLite | 10.0.10 | via `Microsoft.Data.Sqlite` |
| CLI framework | Spectre.Console.Cli | 0.49.0 | Used for the `minirouter` executable |
| Frontend Runtime| Node.js | - | via npm scripts |
| Frontend UI | Svelte | 5.56.8 | Using Vite 8.2.0 |
| Styling | TailwindCSS | 4.3.3 | With `bits-ui`, `lucide-svelte` |

### Repository Structure Reality Check

- Type: Monorepo (Backend + Frontend together)
- Package Manager: NuGet (Backend) / npm (Frontend)
- Notable: The frontend is embedded and built automatically by MSBuild when building the `.csproj`.

## Source Tree and Module Organization

### Project Structure (Actual)

```text
minirouter/
├── MininRouter.csproj   # Main .NET project file
├── Program.cs           # API endpoints, CLI routing, DI container setup
├── Services/            # Core business logic
│   ├── ProviderService.cs # Provider management
│   ├── ProxyService.cs    # Proxy/routing and completion logic
│   ├── LogService.cs      # Request/response logging
│   ├── ApiKeyService.cs   # API key validation/management
│   ├── ErrorClassifier.cs # LLM Provider error handling
│   └── Translation/       # Translation logic (e.g., Anthropic to OpenAI)
├── Models/              # Data models and DTOs
├── Cli/                 # Spectre.Console CLI commands and management
├── frontend/            # Svelte 5 SPA application
│   ├── src/             # Frontend components and logic
│   ├── package.json     
│   └── vite.config.js   
├── minirouter.db        # SQLite database (created at runtime)
└── wwwroot/             # Built frontend assets (served by backend)
```

### Key Modules and Their Purpose

- **ProxyService**: `Services/ProxyService.cs` - Handles the core proxy logic for `/v1/chat/completions` and `/v1/messages`. It includes Anthropic messages translation logic.
- **ProviderService**: `Services/ProviderService.cs` - CRUD operations for AI Providers, handles model availability fetching (`/v1/models`).
- **CLI Commands**: `Cli/Commands/` - Operations like `restart`, `list`, `add`, `edit`, `delete` exposed via terminal.

## Data Models and APIs

### Data Models

- Backend models are available in the `Models/` directory.

### API Specifications

- `/v1/chat/completions` (POST) - Primary OpenAI compatible chat completion endpoint.
- `/v1/messages` (POST) - Anthropic compatible messaging endpoint (translated to chat completion internally).
- `/api/providers` (GET, POST, PUT, DELETE) - Provider management.
- `/api/providers/test` (POST) - Connection testing for providers.
- `/api/keys` (GET, POST, PUT, DELETE) - API Key management.
- `/api/logs` (GET) - Log retrieval.
- `/api/analytics/*` (GET) - Token and Key analytics.

## Technical Debt and Known Issues

### Critical Technical Debt

1. **AOT Serialization Contexts**: Because it uses .NET Native AOT, ALL JSON serialization must be registered in `AppJsonContext.Default`. Careless additions to APIs or Models without registering them will crash at runtime.
2. **Coupled Build Process**: `MininRouter.csproj` runs `npm run build` synchronously in the `BeforeBuild` target.

### Workarounds and Gotchas

- **Models Cache TTL**: `AggregatedModels` is cached for 300 seconds by default. Cache clearing relies on explicit `MemoryCache.Remove("aggregated_models")` calls on provider CRUD operations.
- **Port hardcoding**: The application frequently assumes or binds to `http://localhost:8080`.
- **CLI vs Server Process**: `Program.cs` contains complex branching at the start of the file to determine if it should run as a CLI tool (`-m`, `restart`, `list`) or spin up the Web API Server.

## Integration Points and External Dependencies

### External Services

| Service | Purpose | Integration Type | Key Files |
|---------|---------|------------------|-----------|
| Downstream LLMs | Providers like OpenAI, Anthropic, Gemini | REST API Proxy | `ProxyService.cs`, `ProviderService.cs` |

### Internal Integration Points

- **Frontend Communication**: The Svelte frontend expects the API at `/api/` or `/v1/`. CORS is configured for `http://localhost:5173` and `http://localhost:3000`.

## Development and Deployment

### Local Development Setup

1. Backend: `dotnet run` (also triggers a frontend build by default, unless targeted).
2. Frontend: `cd frontend && npm install && npm run dev` (running on Vite dev server).

### Build and Deployment Process

- **Build Command**: `dotnet publish -c Release` (This builds Native AOT binaries and embeds the frontend).
- **Database**: SQLite database is automatically generated as `minirouter.db`.

## Testing Reality

### Current Test Coverage

- C# Tests exist in `Tests/` folder.
- Frontend Tests: Vitest configured with `@testing-library/svelte` in `frontend/`.

### Running Tests

```bash
dotnet test Tests/MiniRouter.Tests.csproj
cd frontend && npm test
```
