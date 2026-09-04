# Tech Stack Alignment

## Existing Technology Stack

| Category | Current Technology | Version | Usage in Enhancement | Notes |
|----------|-------------------|---------|----------------------|-------|
| Runtime | .NET (C#) | 8.0/9.0+ | Adding Razor/CLI support | JIT compilation (AOT relaxed) |
| Framework | ASP.NET Core | Minimal APIs | API consumption | |
| Serialization| System.Text.Json | Source Gen | JSON communication | |
| Persistence | File System (JSON) | N/A | Underlying data store| |

## New Technology Additions

| Technology | Version | Purpose | Rationale | Integration Method |
|------------|---------|---------|-----------|--------------------|
| Razor Pages | 8.0+ | Dashboard UI | Native .NET server-rendered HTML | `Pages/` directory |
| Spectre.Console | Latest | Agentic CLI | Rich console interfaces for agents | `Cli/` directory |
