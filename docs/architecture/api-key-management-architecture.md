# API Key Management Architecture

## Overview
To fulfill the requirements of Epic 5 (Security & API Key Management), the MiniRouter must secure its `POST /v1/chat/completions` proxy endpoint. This involves storing API keys securely, providing a fast lookup mechanism to avoid degrading TTFB (Time to First Byte) for streaming responses, and correctly applying ASP.NET Core authorization patterns in a Native AOT compliant manner.

## 1. Authentication Middleware Strategy
Instead of full blown generic ASP.NET Core Authentication schemes which can bring reflection overhead, we will use a **Minimal API `IEndpointFilter`**.

### `ApiKeyEndpointFilter`
- **Responsibility:** Intercept requests to `/v1/chat/completions`, extract the `Authorization: Bearer <token>` header, validate the token, and inject the authenticated `ApiKey` context for logging.
- **Native AOT Compliance:** `IEndpointFilter` is fully supported by Native AOT and does not rely on dynamic code generation.
- **Implementation:**
  ```csharp
  public class ApiKeyEndpointFilter : IEndpointFilter
  {
      public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
      {
          // 1. Extract Bearer Token
          // 2. Validate via IApiKeyService (with caching)
          // 3. Return Results.Unauthorized() if invalid
          // 4. Store ApiKey ID in HttpContext.Items for ProxyService to log
          // 5. return await next(context);
      }
  }
  ```
- **Registration:**
  ```csharp
  app.MapPost("/v1/chat/completions", ...)
     .AddEndpointFilter<ApiKeyEndpointFilter>();
  ```

## 2. Performance & Caching (NFR2)
Querying the SQLite database (`minirouter.db`) on every single proxy request will introduce unacceptable latency. 

### Caching Strategy
- We will inject `IMemoryCache` into `IApiKeyService`.
- **Cache Key:** `$"apikey:{plaintextToken}"`
- **Cache Value:** `ApiKey` object.
- **Expiration:** Sliding expiration of 5 minutes.
- **Flow:**
  1. Filter extracts token.
  2. Filter calls `IApiKeyService.ValidateAsync(token)`.
  3. Service checks `IMemoryCache`. If present, returns immediately (fast path).
  4. If missing, hashes the token (if stored hashed) or queries SQLite directly (slow path).
  5. If valid, stores in `IMemoryCache` and fires a background task to update `LastUsedAt` and `UsageCount` in the DB to avoid blocking the proxy request.

## 3. Database Schema (NFR1)
The `api_keys` table currently likely stores the key. For maximum security, we should ideally store a cryptographic hash (e.g., SHA256) of the token, returning the plain text only once upon creation. 
- *Fallback for MVP:* If plain text is currently stored, we can proceed with plain text to reduce complexity, but the caching layer remains mandatory.

## 4. Cross-Cutting Concerns
- **Logging Integration:** The `ApiKeyEndpointFilter` will store the authenticated `ApiKey.Id` in `HttpContext.Items["ApiKeyId"]`. The `ProxyService` will read this and map it to `RequestLog.ApiKeyId` when generating the log entry.
- **UI Integration:** The Svelte SPA `ApiKey.svelte` page will interact with standard REST endpoints (`GET /api/keys`, `POST /api/keys`, `DELETE /api/keys/{id}`). These endpoints do **not** need the `ApiKeyEndpointFilter` since they are for internal administration (assuming the dashboard itself is either local-only or protected by a separate admin auth mechanism).

## Summary
By using a scoped `IEndpointFilter` combined with `IMemoryCache`, we guarantee that proxying remains lightning fast (sub-millisecond auth overhead) while keeping our Native AOT binary small and secure.
