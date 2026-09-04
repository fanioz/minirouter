# Intro Project Analysis and Context

## Existing Project Overview

### Analysis Source
Document-project output available at: `../brownfield-architecture.md`

### Current Project State
MiniRouter is currently an API-only backend service acting as a minimal ASP.NET Core reverse proxy supporting multiple OpenAI-compatible LLM providers. It uses round-robin routing and model-aware forwarding, compiled as a Native AOT binary for an extremely low-memory footprint and fast startup. There is currently no UI present.

## Available Documentation Analysis
Using existing project analysis from document-project output.

### Available Documentation
- [x] Tech Stack Documentation 
- [x] Source Tree/Architecture 
- [ ] Coding Standards 
- [x] API Documentation 
- [x] External API Documentation 
- [ ] UX/UI Guidelines 
- [x] Technical Debt Documentation 
- [ ] Other: N/A

## Enhancement Scope Definition

### Enhancement Type
- [x] New Feature Addition
- [ ] Major Feature Modification
- [ ] Integration with New Systems
- [ ] Performance/Scalability Improvements
- [ ] UI/UX Overhaul
- [ ] Technology Stack Upgrade
- [ ] Bug Fix and Stability Improvements
- [ ] Other: N/A

### Enhancement Description
Introduction of a decoupled Single Page Application (SPA) dashboard for managing LLM providers. This UI will consume the existing management endpoints (`/providers`) to allow users to view, add, edit, and delete providers without relying on curl or Postman.

### Impact Assessment
- [x] Minimal Impact (isolated additions) - The UI will be a separate application consuming existing APIs.
- [ ] Moderate Impact (some existing code changes)
- [ ] Significant Impact (substantial existing code changes)
- [ ] Major Impact (architectural changes required)

## Goals and Background Context

### Goals
- Provide a user-friendly interface to manage LLM providers.
- Maintain the backend's minimal memory footprint and Native AOT compatibility by decoupling the UI.
- Enable CRUD operations on providers visually rather than via manual API calls.

### Background Context
MiniRouter was built as an API-only router managed via curl/Postman. While this is efficient, managing the `providers.json` configuration or using raw API calls can be cumbersome for users. The Architect has confirmed that adding a UI layer directly into the ASP.NET Core application would negatively impact the Native AOT footprint. Therefore, an external decoupled SPA dashboard is recommended to provide usability without sacrificing backend performance.

## Change Log

| Change | Date | Version | Description | Author |
|--------|------|---------|-------------|--------|
| Initial Draft | 2026-08-06 | 1.0 | Created PRD for Decoupled UI Dashboard | Morgan (@pm) |
