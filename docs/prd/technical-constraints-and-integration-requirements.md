# Technical Constraints and Integration Requirements

## Existing Technology Stack
**Languages**: C# (.NET 8.0/9.0+)
**Frameworks**: ASP.NET Core Minimal APIs
**Database**: File System (JSON File)
**Infrastructure**: Standard JIT .NET compilation (Native AOT constraint explicitly relaxed for experimentation)
**External Dependencies**: Upstream LLM APIs

## Integration Approach
**Database Integration Strategy**: N/A (Handled via existing API).
**API/CLI Integration Strategy**: A new CLI tool will be developed for the agentic interface. The web UI will be integrated as server-rendered Razor Pages directly in the .NET backend.
**Frontend Integration Strategy**: ASP.NET Core Razor Pages will be used for the Dashboard UI.
**Testing Integration Strategy**: Unit tests and manual testing of CLI and Razor views.

## Code Organization and Standards
**File Structure Approach**: Razor `.cshtml` files under `Pages/`. CLI logic inside a new `Cli/` folder or separate CLI project.
**Naming Conventions**: Standard C# and Razor conventions.
**Coding Standards**: C# 12 conventions.
**Documentation Standards**: Include CLI usage and web access instructions in `README.md`.

## Deployment and Operations
**Build Process Integration**: Standard `dotnet build`. No Node.js build process.
**Deployment Strategy**: Deploy as a standard framework-dependent or self-contained JIT .NET binary.
**Monitoring and Logging**: Standard ASP.NET Core logging.
**Configuration Management**: The frontend must support environment variables for configuring the backend API URL.

## Risk Assessment and Mitigation
**Technical Risks**: Shifting away from Native AOT means a larger memory footprint and slower startup times.
**Integration Risks**: Integrating Razor Pages with existing Minimal APIs may require careful routing configuration.
**Deployment Risks**: Minimal.
**Mitigation Strategies**: Document the performance differences vs Native AOT.
