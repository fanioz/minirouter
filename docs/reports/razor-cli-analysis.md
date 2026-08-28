# Architectural Analysis: Razor Pages & CLI Interface vs. Vanilla HTML/JS

**Date:** 2026-08-06
**Analyst:** Atlas (@analyst)
**Topic:** Evaluating the proposal to use ASP.NET Core Razor Pages for the Dashboard UI and a CLI for the agentic interface.

---

## 1. Razor Pages for Dashboard UI under Native AOT Constraints

The proposal suggests using ASP.NET Core Razor Pages for the Dashboard UI. However, we must evaluate this against our strict **Native AOT (Ahead-of-Time) constraints**.

### Findings:
- **Incompatibility with Native AOT:** ASP.NET Core's Native AOT support (introduced in .NET 8 and expanded in .NET 9) explicitly **does not support** ASP.NET Core MVC or Razor Pages. 
- **The "Why":** Razor Pages and the underlying MVC framework rely heavily on dynamic code generation, runtime compilation, and extensive reflection for routing, model binding, and view rendering. Native AOT requires all code to be fully analyzable at publish time, stripping out unreferenced code and forbidding runtime dynamic code generation (JIT).
- **Workarounds:** While views can be precompiled, the MVC framework itself remains incompatible with Native AOT. To use Razor Pages, we would have to abandon the Native AOT constraint entirely for the web dashboard, which would significantly increase startup time, memory footprint, and binary size—counteracting the goals of building a lightweight CLI tool.

**Conclusion:** Using Razor Pages is structurally incompatible with a single-binary Native AOT distribution model. 

## 2. CLI for the Agentic Interface

The proposal suggests using a Command Line Interface (CLI) for the agentic interactions.

### Findings:
- **Alignment with AIOX Ethos:** As stated in our `AGENTS.md` Core Rules, the project priority is `CLI First -> Observability Second -> UI Third`. 
- **Feasibility:** Building a rich CLI (e.g., using `Spectre.Console`) for agent interactions is highly feasible and fully compatible with Native AOT. 
- **Workflow Synergy:** The CLI interface naturally supports the fast, terminal-driven workflows that developers prefer when interacting with agents (like `@analyst`, `@architect`, etc.).

**Conclusion:** Moving agentic interaction to the CLI is a perfect strategic fit and should be the primary interface.

## 3. Trade-offs & Feasibility

### Approach A: Proposed Pivot (Razor Pages Dashboard + CLI Agent Interface)
- **Pros:** 
  - CLI provides a fast, native feel for agent interactions.
  - Razor Pages offers server-side rendering for developers familiar with C#.
- **Cons:** 
  - **FATAL:** Breaks Native AOT compilation. We would either fail to compile, or we'd have to drop Native AOT, resulting in a bloated CLI application with slow startup times.
  - Tighter coupling between backend and frontend logic.

### Approach B: Current Path (Vanilla HTML/JS Static Files + CLI Agent Interface)
- **Pros:**
  - **100% Native AOT Compatible:** We can serve static files (`.html`, `.css`, `.js`) via a Minimal API `FileServer` which is fully supported by Native AOT.
  - **Separation of Concerns:** A clean boundary between the lightweight API backend and the static frontend UI.
  - CLI interface can still handle all agent interactions.
- **Cons:**
  - Requires writing raw JS/HTML or managing a separate build pipeline for the frontend, though keeping it Vanilla HTML/JS minimizes build complexity.

## 4. Final Recommendation

As your Analyst (@analyst), **I strongly recommend AGAINST pivoting to Razor Pages for the Dashboard UI, but I HIGHLY ENDORSE using the CLI for the agentic interface.**

**Recommended Action Plan:**
1. **Dashboard UI:** Stick to the **Vanilla HTML/JS static files** approach. It allows us to serve the dashboard using Native AOT-compatible Minimal APIs (`UseStaticFiles`) without violating our strict performance and binary size constraints.
2. **Agent Interface:** Fully adopt the **CLI** for all agent interactions. This aligns with our "CLI First" rule and provides the best developer experience.

*— Atlas, investigando a verdade 🔎*
