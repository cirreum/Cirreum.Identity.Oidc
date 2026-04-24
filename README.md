# Cirreum Identity Oidc

[![NuGet Version](https://img.shields.io/nuget/v/Cirreum.Identity.Oidc.svg?style=flat-square&labelColor=1F1F1F&color=003D8F)](https://www.nuget.org/packages/Cirreum.Identity.Oidc/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Cirreum.Identity.Oidc.svg?style=flat-square&labelColor=1F1F1F&color=003D8F)](https://www.nuget.org/packages/Cirreum.Identity.Oidc/)
[![GitHub Release](https://img.shields.io/github/v/release/cirreum/Cirreum.Identity.Oidc?style=flat-square&labelColor=1F1F1F&color=FF3B2E)](https://github.com/cirreum/Cirreum.Identity.Oidc/releases)
[![License](https://img.shields.io/github/license/cirreum/Cirreum.Identity.Oidc?style=flat-square&labelColor=1F1F1F&color=F2F2F2)](https://github.com/cirreum/Cirreum.Identity.Oidc/blob/main/LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-003D8F?style=flat-square&labelColor=1F1F1F)](https://dotnet.microsoft.com/)

**OIDC identity provider integration for Cirreum — the Infrastructure-layer library that implements a webhook-style pre-token provisioning callback compatible with Descope, Auth0, and any OIDC IdP that supports a custom-claims HTTP callback.**

## Overview

`Cirreum.Identity.Oidc` is the Infrastructure-layer implementation of the Cirreum Identity provider pattern for OIDC identity providers. It registers an HTTP endpoint per configured instance that an OIDC IdP can call during sign-in to:

1. Authenticate itself with a shared secret.
2. Supply the authenticating user's details (external ID, email, correlation ID, client app ID).
3. Receive a decision — allow (with roles) or deny.

The returned decision is mapped into the IdP's custom-claims response, so the issued token carries the provisioned roles.

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

```
200 OK (allowed) or 403 Forbidden (denied)

{
  "allowed":       true | false,
  "roles":         ["role1", "role2"],
  "correlationId": "<echoed from request>"
}
```

### Error responses

| Status | Trigger |
|---|---|
| 401 Unauthorized | Missing / invalid shared secret |
| 400 Bad Request  | Malformed JSON body or missing `externalUserId` |
| 403 Forbidden    | `clientAppId` not in configured allowlist, OR provisioner returned `Deny` |
| 500 Internal Server Error | Provisioner threw, or returned `Allow` with zero roles |

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

## What's not in this package

- **The app's `IUserProvisioner` implementation** — apps register theirs via `builder.AddIdentity().AddProvisioner<TProvisioner>("instance_key")` in the Runtime Extensions layer. This package only resolves the keyed service at callback time.
- **App-facing `AddOidcIdentity()` / `MapOidcIdentity()` extensions** — those live in `Cirreum.Runtime.Identity.Oidc`.
- **Entra External ID integration** — that's a separate package (`Cirreum.Identity.EntraExternalId`) because Entra uses a different protocol shape (token validation + claims issuance extension).

## License

MIT — see [LICENSE](LICENSE).

---

**Cirreum Foundation Framework**  
*Layered simplicity for modern .NET*
