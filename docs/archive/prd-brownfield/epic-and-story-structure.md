# Epic and Story Structure

**Epic Structure Decision**: Single Epic. The scope is limited to introducing a decoupled SPA dashboard for provider management.

## Epic 1: Provider Management Dashboard

**Epic Goal**: Provide a visual, decoupled web interface for managing LLM providers without relying on command-line tools.

**Integration Requirements**: UI must consume the existing `/providers` API. Backend requires CORS enablement.

### Story 1.1 Scaffold Frontend Application and Configure CORS
As a Developer,
I want to scaffold a new SPA and enable CORS on the backend,
so that the frontend can communicate with the MiniRouter API.

**Acceptance Criteria**
1: A new SPA is generated in the `frontend/` directory.
2: `Program.cs` is updated to include a CORS policy allowing requests from the frontend development server.
3: Native AOT build still completes with zero trimming warnings.

**Integration Verification**
- IV1: Verify that existing curl/Postman requests to the API still succeed.
- IV2: Verify that the SPA can make a simple `GET /providers` request successfully.

### Story 1.2 Implement Provider List View
As an Administrator,
I want to see a list of all configured providers,
so that I can quickly review my system's routing targets.

**Acceptance Criteria**
1: The UI fetches data from `GET /providers`.
2: A table or grid displays the provider ID, BaseUrl, and supported models.
3: Graceful loading and error states are shown during the API request.

**Integration Verification**
- IV1: Ensure the API does not expose API keys in the list (verify backend behavior or mask in UI).

### Story 1.3 Implement Provider Create and Edit Forms
As an Administrator,
I want to add new providers and edit existing ones via a form,
so that I don't have to manually edit `providers.json`.

**Acceptance Criteria**
1: A form is provided to input `Id`, `BaseUrl`, `ApiKey`, and `Models`.
2: Submitting the form makes a `POST /providers` or `PUT /providers/{id}` request.
3: Form validation prevents submission with missing required fields.
4: The list view is updated after a successful save.

**Integration Verification**
- IV1: Add a provider via UI and verify `providers.json` is successfully updated on disk.
- IV2: Restart the backend and verify the newly added provider persists.

### Story 1.4 Implement Provider Deletion
As an Administrator,
I want to delete a provider via the UI,
so that I can remove obsolete routing targets.

**Acceptance Criteria**
1: A delete button is available on the provider list.
2: Clicking delete prompts a confirmation dialog.
3: Confirming makes a `DELETE /providers/{id}` request and removes the item from the UI.

**Integration Verification**
- IV1: Delete a provider via UI and verify it's removed from `providers.json`.
