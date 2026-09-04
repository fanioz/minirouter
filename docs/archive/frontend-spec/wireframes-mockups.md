# Wireframes & Mockups

**Primary Design Files:** Since this is a new project, we recommend using a rapid UI component library like Shadcn UI or Material-UI to build standard layouts without needing heavy Figma files.

## Key Screen Layouts
### Provider Dashboard List
**Purpose:** Main view for administrators.
**Key Elements:**
- Top Navigation (Logo, Title)
- Header Action: "Add Provider" Button
- Data Table: Columns for ID, Base URL, Models, Actions (Edit/Delete icons)
**Interaction Notes:** Hovering over a row highlights it. Action buttons have tooltips.

### Provider Form (Create/Edit)
**Purpose:** Data entry for provider details.
**Key Elements:**
- Text Input: Provider ID
- Text Input: Base URL (URL validation)
- Password Input: API Key (masked by default)
- Tag Input / Multi-select: Supported Models
- Footer Actions: "Cancel", "Save Provider"
**Interaction Notes:** Real-time form validation. Submit button disables while saving.
