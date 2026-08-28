# Combined Home and About PRD

## 1. Goals and Background Context

### Goals:
- Provide immediate context and product information ("About" details) directly on the Home page.
- Ensure new users understand what MiniRouter is and its current status the moment they load the dashboard.
- Keep the navigation flat and simple by avoiding the creation of a separate "About" tab.

### Background Context:
Currently, the MiniRouter Dashboard serves purely functional content but lacks contextual information explaining what the product is, its current version, or how it operates. Rather than creating a separate "About" page—which forces users to navigate away from the main dashboard—we want to integrate this educational and contextual information directly into the Home page. This provides a unified, "at-a-glance" experience.

### Change Log:
| Date | Version | Description | Author |
|---|---|---|---|
| 2026-08-10 | 1.0 | Initial Draft | Morgan (PM) |

## 2. Requirements

### Functional:
- **FR1:** The Home page must include a new "About MiniRouter" section (or hero banner).
- **FR2:** This section must display the application's core purpose (e.g., "A lightweight AI provider proxy"), current version, and basic usage instructions.
- **FR3:** The layout must balance this informational content with the existing functional dashboard elements (e.g., Provider list or metrics) so it doesn't push important actionable items below the fold.

### Non-Functional:
- **NFR1:** The integrated "About" content must be visually distinct from the functional dashboard components (e.g., using a different background card or typography hierarchy).
- **NFR2:** The layout must remain responsive, potentially collapsing the "About" section on smaller mobile screens to prioritize actionable dashboard data.

## 3. User Interface Design Goals

### Overall UX Vision:
A minimalist, informative dashboard that immediately grounds the user in the context of the application before presenting actionable data. The "About" content should serve as a welcoming header or side-panel that doesn't distract from the core router metrics/provider lists.

### Key Interaction Paradigms:
- **Static Informational Header/Card:** The "About" section is read-only and requires no complex interaction.
- **Visual Hierarchy:** Use typography and spacing to clearly separate the informational "About" content from the functional dashboard tables below it.

### Core Screens and Views:
- **Home / Dashboard View:** The single, unified screen containing both the "About" context (at the top or side) and the existing functional data (Provider List).

### Accessibility:
- WCAG AA compliant contrast ratios for text.
- Semantic HTML tags (e.g., `<section>`, `<aside>`) to ensure screen readers can logically navigate between the "About" content and the dashboard data.

### Branding:
- Align with the existing minimalist MiniRouter style (currently utilizing standard system-ui fonts, dark headers `#1f2937`, and a light gray `#f3f4f6` background).

### Target Device and Platforms:
- Web Responsive (must look good on Desktop, but stack gracefully on Mobile devices).

## 4. Technical Assumptions

### Repository Structure: 
Monorepo

### Service Architecture:
Monolith (ASP.NET Core Razor Pages application).

### Testing Requirements:
- **Unit Only / Manual:** The changes are almost entirely presentation-layer HTML/CSS within `Index.cshtml`. Basic manual verification of the layout responsiveness and visual checks are sufficient.

### Additional Technical Assumptions and Requests:
- We will leverage the existing Razor Pages structure without adding new frontend frameworks (like React or Vue).
- The "About" content will be statically rendered via standard HTML within the Razor page, avoiding the need for database storage or complex backend models for this specific text.

## 5. Epic List

- **Epic 1: Unified Home Page Experience:** Enhance the primary dashboard landing page to include integrated "About" context, eliminating the need for a separate educational page.

## 6. Epic Details

### Epic 1: Unified Home Page Experience
**Goal:** Enhance the primary dashboard landing page to include integrated "About" context, eliminating the need for a separate educational page.

#### Story 1.1: Integrate "About" context into the Home Dashboard
**As a** new user,
**I want** to see what MiniRouter is and how to use it immediately upon loading the dashboard,
**so that** I don't have to navigate to a separate page to understand the context of the application.

**Acceptance Criteria:**
1. Update `Pages/Index.cshtml` to include a visually distinct header, card, or banner at the top of the main content area containing the "About" information.
2. The "About" section must clearly state the application's purpose (e.g., "MiniRouter Dashboard").
3. The content must scale and display correctly on both desktop and mobile resolutions without pushing the core functional data off-screen unnecessarily.
4. Any legacy routing logic pointing to an `/about` route (if any exist) should seamlessly load or redirect to the Index page.
