# Requirements

## Functional
- FR1: The dashboard shall display a list of all configured providers by calling `GET /providers`.
- FR2: The dashboard shall allow users to add a new provider by calling `POST /providers`.
- FR3: The dashboard shall allow users to edit existing providers by calling `PUT /providers/{id}`.
- FR4: The dashboard shall allow users to delete a provider by calling `DELETE /providers/{id}`.

## Non Functional
- NFR1: The dashboard must be a decoupled Single Page Application (SPA) to preserve the backend's Native AOT and low-memory benefits.
- NFR2: The UI should be responsive and accessible across standard desktop and mobile browsers.
- NFR3: The dashboard must gracefully handle API errors or backend unreachability.

## Compatibility Requirements
- CR1: API Compatibility: Must perfectly consume the existing `/providers` minimal API routes.
- CR2: Backend Architecture: Must not require adding reflection, MVC, or Razor Pages to the .NET backend.
- CR3: CORS: The backend `Program.cs` may need minor adjustments to allow CORS requests from the SPA domain.
