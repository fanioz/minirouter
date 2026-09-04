# Infrastructure and Deployment Integration

## Existing Infrastructure
**Current Deployment:** Native AOT binary deployed standalone.
**Infrastructure Tools:** .NET SDK.
**Environments:** Local development and Production.

## Enhancement Deployment Strategy
**Deployment Approach:** The application is deployed as a standard JIT .NET binary serving Razor Pages natively, alongside a CLI tool distributed via `.NET Tool` or direct execution.
**Infrastructure Changes:** Requires enabling Razor Pages in `Program.cs`.
**Pipeline Integration:** Standard `dotnet build`. No separate frontend build step.

## Rollback Strategy
**Rollback Method:** Revert the SPA deployment independently. Revert `Program.cs` CORS policy if necessary.
**Risk Mitigation:** The decoupled architecture ensures frontend failures cannot crash the backend router.
**Monitoring:** Browser console logs for SPA. Existing console logs for backend.
