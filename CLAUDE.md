# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is **Cirreum.Identity.Oidc**, the Infrastructure-layer implementation of the Cirreum Identity provider pattern for OIDC identity providers. It registers a webhook-style pre-token provisioning endpoint compatible with Descope's HTTP Connector, Auth0 Actions, or any OIDC IdP that can POST a custom-claims payload to an HTTP endpoint.

## Build Commands

```bash
# Build the solution
dotnet build Cirreum.Identity.Oidc.slnx

# Build the project
dotnet build src/Cirreum.Identity.Oidc/Cirreum.Identity.Oidc.csproj

# Run tests (when added)
dotnet test

# Pack for local release (uses version 1.0.100-rc)
dotnet pack --configuration Release
```

## Architecture

### Core responsibility

For each enabled instance in `Cirreum:Identity:Providers:Oidc:Instances:*`, this package:

1. **Services phase** — registers a per-instance keyed-singleton `OidcSharedSecretValidator` (captures the settings for constant-time secret compare).
2. **Endpoints phase** — maps an anonymous `MapPost` at `settings.Route` that runs the `OidcConnectorHandler` flow.

### Request flow (`OidcConnectorHandler.HandleAsync`)

1. Validate shared secret (401 on mismatch).
2. Deserialize JSON body with source-generated `OidcJsonContext` (400 on bad body).
3. Require `externalUserId` (400 if missing).
4. Check `clientAppId` against `AllowedAppIds` when the allowlist is configured (403 on mismatch).
5. Resolve `IUserProvisioner` keyed by `settings.Source` (= instance key).
6. Invoke `ProvisionAsync` with the built `ProvisionContext`.
7. Map `ProvisionResult`:
   - `Allowed { Roles: [...] }` → 200 with response body
   - `Allowed { Roles: [] }` → 500 (provisioner bug — allow-no-roles)
   - `Denied` → 403 with `allowed: false` body
   - Exception → 500

### Key types

| Type | Namespace | Visibility | Purpose |
|---|---|---|---|
| `OidcIdentityProviderRegistrar` | `Cirreum.Identity` | public | Registers services + maps endpoints per instance |
| `OidcIdentityProviderSettings` | `Cirreum.Identity.Configuration` | public | Provider settings container |
| `OidcIdentityProviderInstanceSettings` | `Cirreum.Identity.Configuration` | public | Per-instance settings |
| `OidcConnectorHandler` | `Cirreum.Identity.Oidc` | internal | HTTP handler |
| `OidcSharedSecretValidator` | `Cirreum.Identity.Oidc` | internal | Constant-time header compare |
| `OidcJsonContext` | `Cirreum.Identity.Oidc` | internal | Source-gen JSON context |
| `OidcProvisionRequest` / `OidcProvisionResponse` | `Cirreum.Identity.Oidc.Models` | internal | Wire DTOs |

### RootNamespace

The csproj sets `<RootNamespace>Cirreum.Identity</RootNamespace>` so folder conventions map to the intended sub-namespaces:

- `src/Cirreum.Identity.Oidc/` → `Cirreum.Identity` (registrar)
- `src/Cirreum.Identity.Oidc/Oidc/` → `Cirreum.Identity.Oidc` (impl)
- `src/Cirreum.Identity.Oidc/Oidc/Models/` → `Cirreum.Identity.Oidc.Models` (wire DTOs)
- `src/Cirreum.Identity.Oidc/Configuration/` → `Cirreum.Identity.Configuration` (settings)

## Dependencies

- **Cirreum.IdentityProvider** — base registrar, provisioning contracts (`IUserProvisioner`, `ProvisionContext`, `ProvisionResult`), settings base types
- **Microsoft.AspNetCore.App** — `IEndpointRouteBuilder`, `HttpRequest`, `Results`, etc.

## What's not here

- **App-facing extensions** (`AddOidcIdentity`, `MapOidcIdentity`) — those live in the Runtime Extensions package `Cirreum.Runtime.Identity.Oidc`. The app never touches `OidcIdentityProviderRegistrar` directly.
- **The app's `IUserProvisioner`** — registered by the app via the Runtime Extensions layer as a keyed scoped service, resolved here at callback time.

## Development Notes

- Uses .NET 10.0 with latest C# language version
- Nullable reference types enabled
- Source-generated JSON serialization (`JsonSerializerContext`) for AOT-friendly, low-allocation body handling
- Per-instance validator avoids per-request UTF-8 encoding overhead on the expected secret bytes
- Handler is stateless — constructed per request from the keyed validator, settings (captured closure), IServiceProvider, and logger
- File-scoped namespaces throughout
- K&R braces, tabs for indentation (matches repo `.editorconfig`)
