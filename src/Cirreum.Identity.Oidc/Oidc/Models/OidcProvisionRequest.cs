namespace Cirreum.Identity.Oidc.Models;

using System.Text.Json.Serialization;

/// <summary>
/// The canonical request body POSTed by an OIDC IdP's pre-token webhook to a Cirreum
/// Identity Oidc provisioning endpoint.
/// </summary>
/// <remarks>
/// This is the Cirreum-neutral wire contract. Any OIDC IdP that supports a pre-token
/// webhook (Descope HTTP Connector, Auth0 Action, etc.) can be configured to emit a
/// payload matching this shape.
/// </remarks>
internal sealed record OidcProvisionRequest {

	/// <summary>
	/// The IdP's unique identifier for the user. Required.
	/// </summary>
	[JsonPropertyName("externalUserId")]
	public string ExternalUserId { get; init; } = "";

	/// <summary>
	/// The user's email address, if available from the sign-in flow context.
	/// </summary>
	/// <remarks>
	/// May be empty if the IdP does not supply an email address (for example certain
	/// social identity providers with email sharing disabled). Required when the
	/// configured provisioner uses invitation-based onboarding, since invitations are
	/// matched on email.
	/// </remarks>
	[JsonPropertyName("email")]
	public string Email { get; init; } = "";

	/// <summary>
	/// A flow-scoped correlation identifier (typically the flow execution ID). Used for
	/// end-to-end request tracing. Optional — echoed in the response.
	/// </summary>
	[JsonPropertyName("correlationId")]
	public string CorrelationId { get; init; } = "";

	/// <summary>
	/// The client application identifier that initiated the authentication flow. Validated
	/// against <see cref="Cirreum.Identity.Configuration.OidcIdentityProviderInstanceSettings.AllowedAppIds"/>
	/// when the instance configures an allowlist.
	/// </summary>
	[JsonPropertyName("clientAppId")]
	public string ClientAppId { get; init; } = "";
}
