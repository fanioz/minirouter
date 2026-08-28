# MiniRouter Frontend Documentation

This directory contains two related sets of documents:

1. **The original UI/UX specification** (this file's TOC below) — the provider-management dashboard brief, accessibility targets, responsiveness strategy, etc.
2. **The MiniRouter Neutral Modern design system** ([`design/`](./design/README.md)) — a complete, source-backed package with tokens, previews, applied kit, and provenance. **This is the current source of truth for the dashboard's visual language.**

When building or reviewing frontend surfaces, read the [design system README](./design/README.md) first. The spec below is the original brief; the design package is what the implementation should follow.

---

# MiniRouter Provider Management Dashboard UI/UX Specification

## Table of Contents

- [MiniRouter Provider Management Dashboard UI/UX Specification](#table-of-contents)
  - [Introduction](./introduction.md)
    - [Overall UX Goals & Principles](./introduction.md#overall-ux-goals-principles)
      - [Target User Personas](./introduction.md#target-user-personas)
      - [Usability Goals](./introduction.md#usability-goals)
      - [Design Principles](./introduction.md#design-principles)
    - [Change Log](./introduction.md#change-log)
  - [Information Architecture (IA)](./information-architecture-ia.md)
    - [Site Map / Screen Inventory](./information-architecture-ia.md#site-map-screen-inventory)
    - [Navigation Structure](./information-architecture-ia.md#navigation-structure)
  - [User Flows](./user-flows.md)
    - [Flow: View Providers](./user-flows.md#flow-view-providers)
    - [Flow: Add / Edit Provider](./user-flows.md#flow-add-edit-provider)
    - [Flow: Delete Provider](./user-flows.md#flow-delete-provider)
  - [Wireframes & Mockups](./wireframes-mockups.md)
    - [Key Screen Layouts](./wireframes-mockups.md#key-screen-layouts)
      - [Provider Dashboard List](./wireframes-mockups.md#provider-dashboard-list)
      - [Provider Form (Create/Edit)](./wireframes-mockups.md#provider-form-createedit)
  - [Component Library / Design System](./component-library-design-system.md)
    - [Core Components](./component-library-design-system.md#core-components)
      - [Button](./component-library-design-system.md#button)
      - [Data Table](./component-library-design-system.md#data-table)
      - [Modal / Dialog](./component-library-design-system.md#modal-dialog)
  - [Branding & Style Guide](./branding-style-guide.md)
    - [Visual Identity](./branding-style-guide.md#visual-identity)
    - [Color Palette](./branding-style-guide.md#color-palette)
    - [Typography](./branding-style-guide.md#typography)
    - [Spacing & Layout](./branding-style-guide.md#spacing-layout)
  - [Accessibility Requirements](./accessibility-requirements.md)
  - [Responsiveness Strategy](./responsiveness-strategy.md)
    - [Breakpoints](./responsiveness-strategy.md#breakpoints)
    - [Adaptation Patterns](./responsiveness-strategy.md#adaptation-patterns)
  - [Animation & Micro-interactions](./animation-micro-interactions.md)
  - [Performance Considerations](./performance-considerations.md)
    - [Performance Goals](./performance-considerations.md#performance-goals)
    - [Design Strategies](./performance-considerations.md#design-strategies)
  - [Next Steps](./next-steps.md)
    - [Immediate Actions](./next-steps.md#immediate-actions)
    - [Design Handoff Checklist](./next-steps.md#design-handoff-checklist)
