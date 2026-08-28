# Introduction

This document outlines the architectural approach for enhancing MiniRouter with a decoupled Single Page Application (SPA) dashboard. Its primary goal is to serve as the guiding architectural blueprint for AI-driven development of new features while ensuring seamless integration with the existing system.

**Relationship to Existing Architecture:**
This document supplements existing project architecture by defining how new components will integrate with current systems. Where conflicts arise between new and existing patterns, this document provides guidance on maintaining consistency while implementing enhancements.

## Existing Project Analysis

Based on the existing project documentation, the current MiniRouter project is an API-only backend service acting as a minimal ASP.NET Core reverse proxy supporting multiple OpenAI-compatible LLM providers.

### Current Project State
- **Primary Purpose:** Minimal ASP.NET Core reverse proxy for LLM providers.
- **Current Tech Stack:** .NET (C#) 8.0/9.0+ compiled with Native AOT, ASP.NET Core Minimal APIs.
- **Architecture Style:** API-only backend with minimal dependencies and footprint.
- **Deployment Method:** Native AOT standalone binary.

### Available Documentation
- Tech Stack Documentation
- Source Tree/Architecture
- API Documentation
- External API Documentation
- Technical Debt Documentation

### Identified Constraints
- **Native AOT Constraints:** Reflection-based serializers or DI mechanisms will break the AOT build.
- **Memory Footprint:** The application is highly optimized (~35 MB idle memory). A UI layer directly in the .NET application would negatively impact this.
- **Stateless Round-Robin:** In-memory state for round-robin resets on application restart.

## Change Log

| Change | Date | Version | Description | Author |
|--------|------|---------|-------------|--------|
| Initial Draft | 2026-08-06 | 1.0 | Created Architecture for Decoupled UI Dashboard | Aria (@architect) |
