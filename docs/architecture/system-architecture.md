# MiniRouter Brownfield Architecture Document

## Introduction

This document captures the CURRENT STATE of the MiniRouter codebase, including technical debt, workarounds, and real-world patterns. It serves as a reference for AI agents working on enhancements or bug fixes.

### Document Scope

Comprehensive documentation of the entire system as a multi-provider Native AOT LLM Router.

### Change Log

| Date | Version | Description | Author |
|------|---------|-------------|--------|
| 2026-08-06 | 1.0 | Initial brownfield analysis | Aria (Architect) |

## Quick Reference - Key Files and Entry Points

### Critical Files for Understanding the System

- **Main Entry & Endpoints**: `Program.cs` - Handles DI setup, configuration, provider CRUD endpoints, and the main proxy endpoint for `/v1/chat/completions`.
- **Project Configuration**: `MininRouter.csproj` - Defines .NET 10 TargetFramework and Native AOT compilation settings.
- **Provider Management Service**: `Services/ProviderService.cs` - In-memory state and JSON file persistence for LLM providers.
- **Data Models**: `Models/Provider.cs` - Defines the `Provider` models, DTOs, and the `AppJsonContext` for AOT-safe source generation JSON serialization.
- **Example Config**: `providers.example.json`

## High Level Architecture

### Technical Summary
MiniRouter is a minimalistic API reverse proxy built with ASP.NET Core Minimal APIs. It routes requests for OpenAI-compatible `/v1/chat/completions` endpoints to various backend LLM providers using a round-robin strategy based on requested models. It stores provider configurations in a flat JSON file on disk, making it highly portable and simple to deploy as a Native AOT self-contained binary.

### Actual Tech Stack

| Category | Technology | Version | Notes |
|----------|------------|---------|--------|
| Runtime | .NET / ASP.NET Core | 10.0 | Native AOT enabled (`PublishAot=true`) |
| Serialization | System.Text.Json | 10.0 | Uses Source Generators exclusively (no reflection) |
| Database | JSON File | N/A | Persistent state is a single JSON file read/written synchronously via locks |

### Repository Structure Reality Check

- Type: Monorepo (Single App)
- Package Manager: NuGet
- Notable: Highly condensed structure with endpoints living directly in `Program.cs`. 

## Source Tree and Module Organization

### Project Structure (Actual)

```text
minirouter/
├── Models/
│   └── Provider.cs           # Provider data records, DTOs, and System.Text.Json context
├── Services/
│   └── ProviderService.cs    # Thread-safe in-memory provider list backed by a JSON file
├── Program.cs                # Entry point, DI container configuration, Minimal API routing, Reverse Proxy logic
├── MininRouter.csproj        # Build configuration, Native AOT flags
├── appsettings.json          # ASP.NET Core default config
├── providers.example.json    # Example list of LLM providers
├── publish/                  # Output directory for Linux x64 AOT binaries
├── publish-osx/              # Output directory for macOS ARM64 AOT binaries
└── scripts/                  # Helper/deployment scripts (if any)
```

### Key Modules and Their Purpose

- **Router Proxy**: Located inside `Program.cs` under the `app.MapPost("/v1/chat/completions", ...)` route. It buffers the incoming request, inspects the requested `model`, selects a provider, rewrites the request body (if a model override is set), forwards the request using an upstream `HttpClient`, and pipes the raw response (headers + body) back to the client to support streaming without parsing.
- **Provider Service**: `Services/ProviderService.cs` - Handles the logic to round-robin over enabled providers matching the requested model. Manages persistence to `PROVIDERS_CONFIG_PATH` via `System.Text.Json` source-generated models.

## Data Models and APIs

### Data Models

See `Models/Provider.cs` for definitions:
- **Provider**: The core entity storing `Id`, `Name`, `BaseUrl`, `ApiKey`, `Enabled`, `Model` (override), and `Models` (supported models).
- **DTOs**: `CreateProviderDto`, `UpdateProviderDto`, `MaskedProvider` (hides API keys).
- **JsonContext**: `AppJsonContext` - Essential for AOT compilation, explicitly registers types for `System.Text.Json`.

### API Specifications

Endpoints defined in `Program.cs`:
- **GET `/health`**: Simple health check.
- **GET `/providers`**: Lists all providers (API keys masked).
- **GET `/providers/{id}`**: Gets a specific provider (API key masked).
- **POST `/providers`**: Creates a new provider.
- **PUT `/providers/{id}`**: Updates an existing provider.
- **DELETE `/providers/{id}`**: Deletes a provider.
- **POST `/v1/chat/completions`**: The main proxy endpoint. Accepts an OpenAI-compatible completion request and streams the response from the selected upstream provider.

## Technical Debt and Known Issues

### Critical Technical Debt

1. **Global Round-Robin Counter**: In `Services/ProviderService.cs`, `_currentIndex` is a single integer shared across all requests regardless of the model being requested. This means the round-robin sequence is globally advanced and not per-model, which could lead to uneven request distribution or skipping of providers for a specific model under high concurrency.
2. **File-Based Persistence Concurrency**: `ProviderService` uses a coarse-grained lock (`_lock`) for all CRUD operations, and `SaveProviders()` writes the entire list to a JSON file synchronously. This will not scale horizontally (cannot run multiple instances of MiniRouter easily) and may cause performance bottlenecks if provider CRUD operations are frequent.
3. **Endpoint Logic Coupling**: The proxy logic for `/v1/chat/completions` is entirely written inline in `Program.cs` as an anonymous function. This limits testability.
4. **Body Buffering**: The proxy endpoint buffers the entire request body into memory (`StreamReader.ReadToEndAsync`) to inspect the model. For large request payloads (e.g., massive context windows or images), this could cause high memory consumption, negating some benefits of the lightweight AOT build.

### Workarounds and Gotchas

- **Native AOT Limitations**: Do not introduce reflection-based libraries (like Newtonsoft.Json) or default `JsonSerializer` calls without `JsonSerializerContext`. Always use `AppJsonContext.Default`.
- **Environment Variables**: The provider configuration path relies on `PROVIDERS_CONFIG_PATH`.
- **Header Parsing**: The proxy manually copies and strips specific headers (`Host`, `Transfer-Encoding`, `Authorization`). Future OpenAI API header changes might require updates to this exclusion list to prevent proxy errors.

## Integration Points and External Dependencies

### External Services

| Service | Purpose | Integration Type | Key Files |
|---------|---------|------------------|-----------|
| Any OpenAI-Compatible API | Upstream LLM Provider | REST API Proxy | `Program.cs` |

### Internal Integration Points

- **JSON File Store**: Uses standard `System.IO.File` to read and write to `providers.json`.

## Development and Deployment

### Local Development Setup

1. Run `cp providers.example.json providers.json`
2. Run `dotnet run` or `dotnet watch run` for hot reload. 

### Build and Deployment Process

- **Native AOT Linux Build**: 
  `dotnet publish -c Release -r linux-x64 --self-contained -p:PublishAot=true -o ./publish`
- **Native AOT macOS Build**: 
  `dotnet publish -c Release -r osx-arm64 --self-contained -p:PublishAot=true -o ./publish-osx`
- Note: Cross-compilation to Linux from macOS for AOT requires a Linux linker or running inside Docker.

## Testing Reality

### Current Test Coverage

- Unit Tests: None found.
- E2E / Load Tests: Handled via shell scripts (`load-test.sh`, `memory-measure.sh`).
- Manual Testing: Primary QA method via `curl` as documented in `README.md`.

## Appendix - Useful Commands and Scripts

### Frequently Used Commands

```bash
dotnet run                 # Run in dev mode
PORT=8080 ./publish/MininRouter # Run production binary on specific port
```

### Debugging and Troubleshooting

- **Proxy Failures**: Output uses `Console.WriteLine` directly in `Program.cs`. Watch standard out for `[Proxy] Request to ... failed` messages.
- **Serialization Issues**: Ensure any new DTOs are registered in `AppJsonContext` within `Models/Provider.cs`.
