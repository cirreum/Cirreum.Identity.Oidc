namespace Cirreum.Identity.Configuration;

/// <summary>
/// Settings for a single Oidc identity provider instance.
/// Maps to: <c>Cirreum:Identity:Providers:Oidc:Instances:{key}</c>.
/// </summary>
/// <remarks>
/// <para>
/// The Oidc provider implements a webhook-style pre-token provisioning callback. Any
/// OIDC IdP that supports a custom-claims HTTP callback (Descope HTTP Connector, Auth0
/// Action, etc.) can be configured to POST a payload matching the Cirreum-neutral wire
/// contract to this endpoint.
/// </para>
/// <para>
/// Each instance represents a distinct IdP integration — for example two Descope projects
/// serving different client applications, each with its own shared secret and route.
/// </para>
/// </remarks>
public sealed class OidcIdentityProviderInstanceSettings
	: IdentityProviderInstanceSettings {

	/// <summary>
	/// The shared secret used to authenticate inbound calls from the IdP's webhook.
	/// Required.
	/// </summary>
	/// <remarks>
	/// Generate a long random value (32+ bytes, base64/hex-encoded) and configure it both
	/// on the IdP's webhook settings and here. Never commit the secret to source control —
	/// prefer user-secrets, Key Vault, or an equivalent secret store.
	/// </remarks>
	public required string SharedSecret { get; set; }

	/// <summary>
	/// The HTTP header the IdP uses to transmit the shared secret.
	/// Defaults to <c>Authorization</c>.
	/// </summary>
	public string AuthorizationHeaderName { get; set; } = "Authorization";

	/// <summary>
	/// The scheme prefix the IdP places before the shared secret in the authorization
	/// header. Defaults to <c>Bearer</c>. Set to an empty string to compare the header
	/// value directly (API-key style).
	/// </summary>
	public string AuthorizationScheme { get; set; } = "Bearer";

	/// <summary>
	/// Optional comma- or semicolon-separated list of client application IDs allowed to
	/// trigger this endpoint. If empty, <c>ClientAppId</c> enforcement is disabled.
	/// </summary>
	/// <remarks>
	/// When configured, the handler rejects (403) requests whose <c>clientAppId</c> is
	/// not in this allowlist. This prevents a leaked shared secret from being used to
	/// provision users into a different application that happens to share the secret.
	/// </remarks>
	public string AllowedAppIds { get; set; } = "";

	/// <summary>
	/// Parses <see cref="AllowedAppIds"/> into a set for fast lookup.
	/// </summary>
	internal HashSet<string> GetAllowedAppIdSet() =>
		[.. this.AllowedAppIds.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

	/// <summary>
	/// Returns <see langword="true"/> if <see cref="AllowedAppIds"/> has been configured
	/// with at least one entry.
	/// </summary>
	internal bool HasAllowedAppIds() =>
		!string.IsNullOrWhiteSpace(this.AllowedAppIds);
}
