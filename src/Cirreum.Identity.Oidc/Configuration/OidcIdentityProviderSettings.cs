namespace Cirreum.Identity.Configuration;

/// <summary>
/// Settings container for Oidc identity provider instances.
/// Maps to: <c>Cirreum:Identity:Providers:Oidc</c>.
/// </summary>
public sealed class OidcIdentityProviderSettings
	: IdentityProviderSettings<OidcIdentityProviderInstanceSettings>;
