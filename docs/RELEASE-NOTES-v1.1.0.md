# Cirreum.Identity.Oidc 1.1.0 — provisioned claims beyond roles

## Why this release exists

The OIDC adapter could return exactly one thing for the token: a **roles** list. That was enough
when the IdP federated a full profile for free, but under a pure IdP-as-a-service backing (Descope,
Auth0, any OIDC IdP with a pre-token webhook) — where the application is the authority for the
user's attributes — the app had no channel to project its own claims (display name, tenant,
entitlements) into the token. This release widens that channel to the application's full claim set,
tracking the `Cirreum.IdentityProvider 2.0.0` contract.

## What's new

The response now projects the whole `IProvisionedIdentity.Claims` set into a `customClaims`
object, keyed by `custom*` wire name — each claim an array:

```jsonc
// before
{ "allowed": true, "roles": ["subscriber", "admin"], "correlationId": "…" }

// after
{ "allowed": true,
  "customClaims": {
    "customRoles":  ["subscriber", "admin"],
    "customName":   ["Jane Smith"],
    "customTenant": ["acme"]
  },
  "correlationId": "…" }
```

Every provisioned claim lives in a `custom*` namespace, so an app-minted claim can never collide
with a native IdP claim. The hand-authored flow maps each `customClaims.custom*` member to its own
token claim by member path — declarative flow tools do member-path references natively.

**Allowed-with-no-claims is now valid.** A provisioner that admits a user but mints nothing returns
a 200 with an empty `customClaims` object. The former "allowed with no roles → 500" guard is gone —
a roleless identity is a deliberate application choice (ABAC / ownership models), expressed by the
shape of the app's own provisioned-identity type.

**Observability.** The provisioner call is wrapped in the Core `Cirreum.Identity.Provisioning`
telemetry scope (tagged `provider = oidc`): one span per callback plus duration, outcome-count, and
minted-claim-count metrics. No user identifier or email is tagged.

## Per-claim IdP declaration

Each `custom*` member is a distinct token claim the operator declares once, IdP-side — for Descope,
one flow mapping per claim (`customRoles ← connectors.cirreumProvision.customClaims.customRoles`, …).
The `custom*` name is kept end-to-end; the client canonicalizes `customRoles → roles` during
`ClaimsPrincipal` construction, or the operator canonicalizes at the IdP and skips the client extender.

## Compatibility

- Requires `Cirreum.IdentityProvider 2.0.0`.
- The package's public .NET surface (registrar, settings) is unchanged — this is a minor bump.
- The **wire contract** changed: the top-level `roles` field is gone, replaced by
  `customClaims.customRoles`. A hand-authored IdP flow that read `roles` must be updated to read
  `customClaims.customRoles`, and to map any additional `custom*` claims the provisioner mints.

## See also

- `Cirreum.IdentityProvider 2.0.0` — the reshaped provisioning contract this adapter projects.
- `Cirreum.Identity.EntraExternalId 2.1.0` — the sibling adapter, same claim set as flat inline
  Entra token claims.
