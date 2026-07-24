# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Provisioned claims beyond roles. The provisioning response now projects the application's full
  `IProvisionedIdentity.Claims` set into a `customClaims` object, keyed by `custom*` wire name
  (`customRoles`, `customName`, `customTenant`, …), each an array. The hand-authored IdP flow
  maps each `customClaims.custom*` member to its own token claim by member path.
- OpenTelemetry: the provisioner callback is now wrapped in the Core
  `Cirreum.Identity.Provisioning` telemetry scope, tagged `provider = oidc`, emitting the
  provisioning span plus duration / outcome-count / minted-claim-count metrics.

### Changed

- Requires `Cirreum.IdentityProvider` `2.0.0`. The handler reads the reshaped
  `ProvisionResult.Allowed(Claims)` and projects `Claims.ToClaimMap()` onto the wire.
- **Wire response reshaped.** The former top-level `roles` field is gone; roles are now
  `customClaims.customRoles`, one member of the `custom*` claim object like any other claim. A
  hand-authored IdP flow that read `roles` must read `customClaims.customRoles` instead. The
  package's public .NET surface (registrar, settings) is unchanged.
- An allowed result with no claims is now a valid outcome (the app admits the user but mints
  nothing beyond what the IdP itself issues) — it returns a 200 with an empty `customClaims`
  object, instead of the former 500. The empty-roles guard is removed.

## [1.0.7] - 2026-07-20

### Updated

- Updated NuGet packages.

## [1.0.6] - 2026-07-19

### Updated

- Updated NuGet packages.

## [1.0.5] - 2026-07-04

### Updated

- Updated NuGet packages.

## [1.0.4] - 2026-07-04

### Updated

- Updated NuGet packages.

## [1.0.3] - 2026-05-07

### Updated

- Updated NuGet packages.

## [1.0.2] - 2026-05-01

### Updated

- Updated NuGet packages.
