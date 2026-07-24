# Cirreum Identity Oidc

[![NuGet Version](https://img.shields.io/nuget/v/Cirreum.Identity.Oidc.svg?style=flat-square&labelColor=1F1F1F&color=003D8F)](https://www.nuget.org/packages/Cirreum.Identity.Oidc/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Cirreum.Identity.Oidc.svg?style=flat-square&labelColor=1F1F1F&color=003D8F)](https://www.nuget.org/packages/Cirreum.Identity.Oidc/)
[![GitHub Release](https://img.shields.io/github/v/release/cirreum/Cirreum.Identity.Oidc?style=flat-square&labelColor=1F1F1F&color=FF3B2E)](https://github.com/cirreum/Cirreum.Identity.Oidc/releases)
[![License](https://img.shields.io/badge/license-MIT-F2F2F2?style=flat-square&labelColor=1F1F1F)](https://github.com/cirreum/Cirreum.Identity.Oidc/blob/main/LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-003D8F?style=flat-square&labelColor=1F1F1F)](https://dotnet.microsoft.com/)

**OIDC identity provider integration for Cirreum — the Infrastructure-layer library that implements a webhook-style pre-token provisioning callback compatible with Descope, Auth0, and any OIDC IdP that supports a custom-claims HTTP callback.**

## Overview

`Cirreum.Identity.Oidc` is the Infrastructure-layer implementation of the Cirreum Identity provider pattern for OIDC identity providers. It registers an HTTP endpoint per configured instance that an OIDC IdP can call during sign-in to:

1. Authenticate itself with a shared secret.
2. Supply the authenticating user's details (external ID, email, correlation ID, client app ID).
3. Receive a decision — allow (with a `custom*` claim set) or deny.

The returned decision is mapped into the IdP's custom-claims response, so the issued token carries the provisioned claims (roles, name, tenant, …).

## Installation

Apps do **not** reference this package directly. Install `Cirreum.Runtime.Identity.Oidc` (or the umbrella `Cirreum.Runtime.Identity`), which brings this package in transitively and exposes the app-facing `builder.AddIdentity()` / `app.MapIdentity()` extensions.

## Wire contract

Cirreum-neutral. Any OIDC IdP's pre-token webhook can be configured to emit a payload matching this shape.

### Request

```
POST {settings.Route}
Authorization: {AuthorizationScheme} {SharedSecret}     // default "Bearer <secret>"
Content-Type: application/json

{
  "externalUserId": "<required>",
  "email":          "<optional — required in Invitation mode to do lookup>",
  "correlationId":  "<optional — echoed in response>",
  "clientAppId":    "<optional — validated against AllowedAppIds when that allowlist is configured>"
}
```

### Response

Every provisioned claim lives in a `custom*` namespace and rides in a `customClaims` object, each
member an array. The former top-level `roles` field is gone — roles are `customClaims.customRoles`,
one member like any other claim:

```
200 OK (allowed) or 403 Forbidden (denied)

{
  "allowed":       true | false,
  "customClaims": {
    "customRoles":  ["role1", "role2"],
    "customName":   ["Jane Smith"],
    "customTenant": ["acme"]
  },
  "correlationId": "<echoed from request>"
}
```

A provisioner that admits the user but mints nothing returns a 200 with an empty `customClaims` object.

### Error responses

| Status | Trigger |
|---|---|
| 401 Unauthorized | Missing / invalid shared secret |
| 400 Bad Request  | Malformed JSON body or missing `externalUserId` |
| 403 Forbidden    | `clientAppId` not in configured allowlist, OR provisioner returned `Deny` |
| 500 Internal Server Error | Provisioner threw |

## Configuration

```json
{
  "Cirreum": {
    "Identity": {
      "Providers": {
        "Oidc": {
          "Instances": {
            "clientA_descope": {
              "Enabled": true,
              "Route": "/auth/clientA/provision",
              "SharedSecret": "<long-random-value>",
              "AuthorizationHeaderName": "Authorization",
              "AuthorizationScheme": "Bearer",
              "AllowedAppIds": "P2Xn9Kq...,P2YaH7t..."
            },
            "clientB_descope": {
              "Enabled": true,
              "Route": "/auth/clientB/provision",
              "SharedSecret": "<long-random-value>",
              "AllowedAppIds": "P2Zm3Lw..."
            }
          }
        }
      }
    }
  }
}
```

### Per-instance settings

| Key | Default | Notes |
|---|---|---|
| `Enabled` | `false` | Instance is skipped during registration when `false`. |
| `Route` | — | Required. The HTTP route where the IdP posts its webhook. |
| `SharedSecret` | — | Required. Long random value; store in Key Vault / user-secrets. |
| `AuthorizationHeaderName` | `"Authorization"` | Header carrying the secret. Override for API-key-style headers (e.g. `X-Api-Key`). |
| `AuthorizationScheme` | `"Bearer"` | Prefix expected before the secret. Set to `""` for raw header value. |
| `AllowedAppIds` | `""` | Optional comma/semicolon-separated list of permitted `clientAppId` values. Empty = disabled. |

### Instance key = Source name

The instance key (e.g. `clientA_descope`) is auto-populated into `ProvisionContext.Source` and is also the keyed-DI key under which the app registers its `IUserProvisioner` (via `AddProvisioner<T>(key)` in the Runtime Extensions layer). Do **not** set `Source` in configuration — it will fail loudly on mismatch.

## Security notes

- **Shared-secret validation uses constant-time comparison** (`CryptographicOperations.FixedTimeEquals`) to prevent timing attacks.
- **Secret rotation** — update the value in config and on the IdP simultaneously. There is no overlap / grace-period support at this layer; if you need rotation without downtime, run two enabled instances with different routes and cut over.
- **`AllowedAppIds`** defends against a leaked shared secret being reused against a different application that happens to share the secret. Enable it whenever the IdP populates `clientAppId`.
- **Body integrity is not verified.** If you need stronger guarantees (HMAC over the body, signed JWT envelope), replace `OidcSharedSecretValidator` with an application-specific implementation.

## Configuring Descope

Descope is the reference integration for this package. The wire contract above is
IdP-neutral; this section is the practical wiring — including the gotchas that fail
*silently* if missed.

### Flow wiring

In the Descope flow (the one your OIDC federated app runs at sign-in), add an
**HTTP connector** step calling the provisioning endpoint, with a request body template
matching the wire contract:

```json
{
  "externalUserId": "{{user.userId}}",
  "email": "{{user.email}}",
  "correlationId": "{{flow.executionId}}",
  "clientAppId": "<project-or-app-id literal>"
}
```

- `{{flow.executionId}}` needs the dot syntax (`{{flowExecutionId}}` resolves empty).
- `{{app.clientId}}` resolves null in B2C/direct-SDK projects — hardcode the ID literal.
- **Fail closed:** route the connector's error branches through a *Reset Auth Info* step
  into `END - FAILURE`. Only a success-status `END` mints a JWT, and a provisioning
  outage must deny sign-in, not skip provisioning.
- The flow editor's **Test Run does not call real connectors** — only a live browser
  sign-in exercises the HTTP connector path.

### Applying the returned claims — `roles` is a reserved claim name

After the connector step, branch on its `allowed` output and apply each member of its
`customClaims` output with a **Custom Claims** action — map `customClaims.customRoles` to the
`customRoles` claim, `customClaims.customName` to `customName`, and so on. Because the response
already names every member `custom*`, the connector-output path and the target claim name are the
same string. The critical gotcha this convention exists for:

> **A custom claim named `roles` is silently dropped.** `roles` is a Descope system
> claim (the RBAC projection, alongside `amr`, `drn`, `tenants`, `permissions`). The
> action reports success and the JWT is simply unchanged. The `custom*` namespace sidesteps
> this for every claim, not just roles — **`customRoles`** never collides.

To verify a custom claim actually registered, decode the **refresh JWT** and check its
`dcl` (declared custom claims) array — the claim name must appear there.

### Getting the claims into the ID token — `descope.custom_claims`

Custom claims are stored on the refresh token and copied into session tokens on refresh.
For an OIDC federated app, they reach the **ID token** only when the client's
authorization request includes the **`descope.custom_claims` scope**:

```csharp
// Blazor WASM client (Cirreum.Runtime.Wasm.Oidc)
idp.DefaultScopes.Add("customRoles");
idp.DefaultScopes.Add("descope.custom_claims");
```

Without that scope the failure is asymmetric and confusing: the **access token** carries
`customRoles` (APIs work), but the **ID token** doesn't — and browser clients build
their principal from the ID token, so client-side role checks silently deny while the
server behaves normally.

On the client, no extender is needed: `Cirreum.Runtime.Wasm` (1.1.0+) canonicalizes every
provisioned `custom*` claim automatically during authentication — `customRoles` aliases to the
configured role claim type and its JSON-array value splits into individual claims, so
`User.IsInRole` just works. Register an `IClaimsExtender` (via `AddClaimsExtender<T>()` on the
Wasm.Oidc builder) only for app-specific transformations, such as resolving precedence when a
token carries both a native claim and its minted `custom*` counterpart.

### Freshness model

The custom claims are captured **once, when this webhook runs** (at flow execution), and
then ride the entire refresh chain — a role change reaches the client only on a fresh
sign-in. Treat client-side token roles as UI gating, and keep server-side authorization
authoritative: when the access token carries no `roles` claim, Cirreum's server claims
transformation resolves roles from the application store per request via the app's
registered `IApplicationUserResolver`, so revocation is immediate where it matters.

## What's not in this package

- **The app's `IUserProvisioner` implementation** — apps register theirs via `builder.AddIdentity().AddProvisioner<TProvisioner>("instance_key")` in the Runtime Extensions layer. This package only resolves the keyed service at callback time.
- **App-facing `AddOidcIdentity()` / `MapOidcIdentity()` extensions** — those live in `Cirreum.Runtime.Identity.Oidc`.
- **Entra External ID integration** — that's a separate package (`Cirreum.Identity.EntraExternalId`) because Entra uses a different protocol shape (token validation + claims issuance extension).

## License

MIT — see [LICENSE](LICENSE).

---

**Cirreum Foundation Framework**  
*Layered simplicity for modern .NET*
