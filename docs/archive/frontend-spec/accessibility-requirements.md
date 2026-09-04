# Accessibility Requirements

**Standard:** WCAG 2.1 AA

**Key Requirements:**
- **Visual:** High color contrast for text and subtle but clear focus rings (`ring-2 ring-accent`) on all interactive elements.
- **Interaction:** Full keyboard navigation support (Tab, Enter, Escape for Modals).
- **Content:** ARIA labels on icon-only buttons (Edit/Delete). Focus management (focus moves to modal when opened, returns to trigger button when closed).

**Testing Strategy:** Run Lighthouse accessibility audit on the SPA and use keyboard-only navigation for manual testing.
