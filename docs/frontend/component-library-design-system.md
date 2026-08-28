# Component Library / Design System

**Design System Approach:** Adopt **Radix UI** or **Shadcn UI** with **Tailwind CSS**. These provide unstyled, accessible primitives that align perfectly with the minimal footprint goals.

## Core Components
### Button
**Purpose:** Trigger actions (Submit, Cancel, Add, Delete).
**Variants:** Primary (Add/Save), Secondary (Cancel), Destructive (Delete).
**States:** Default, Hover, Active, Disabled, Loading (Spinner).

### Data Table
**Purpose:** Display the list of providers cleanly.
**Variants:** Default with sticky header.
**States:** Empty state, Loading state (skeletons), Error state.

### Modal / Dialog
**Purpose:** Focus user attention on forms and confirmations without leaving the page.
**Variants:** Form Modal, Alert Dialog (Delete confirmation).
**States:** Open, Closed.
