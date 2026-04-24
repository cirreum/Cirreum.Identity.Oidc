namespace Cirreum.Identity.Oidc.Models;

using System.Text.Json.Serialization;

/// <summary>
/// The canonical response body returned by a Cirreum Identity Oidc provisioning endpoint
/// to an OIDC IdP's pre-token webhook.
/// </summary>
/// <remarks>
/// IdP-side flow actions read the <c>allowed</c> and <c>roles</c> fields to drive
/// conditional branches, set custom claims, or fail the sign-in.
/// </remarks>
internal sealed record OidcProvisionResponse {

	/// <summary>
	/// <see langword="true"/> when the provisioner allowed the user;
	/// <see langword="false"/> when the user was denied.
	/// </summary>
	[JsonPropertyName("allowed")]
	public bool Allowed { get; init; }

	/// <summary>
	/// The roles to embed in the issued token. Empty when <see cref="Allowed"/> is
	/// <see langword="false"/>.
	/// </summary>
	[JsonPropertyName("roles")]
	public IReadOnlyList<string> Roles { get; init; } = [];

	/// <summary>
	/// Echoes the correlation identifier supplied in the request.
	/// </summary>
	[JsonPropertyName("correlationId")]
	public string CorrelationId { get; init; } = "";
}
