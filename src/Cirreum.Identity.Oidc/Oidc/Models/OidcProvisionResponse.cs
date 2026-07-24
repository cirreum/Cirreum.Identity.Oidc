namespace Cirreum.Identity.Oidc.Models;

using System.Text.Json.Serialization;

/// <summary>
/// The canonical response body returned by a Cirreum Identity Oidc provisioning endpoint
/// to an OIDC IdP's pre-token webhook.
/// </summary>
/// <remarks>
/// IdP-side flow actions read the <c>allowed</c> flag to admit or fail the sign-in, and map each
/// member of the <c>customClaims</c> object (<c>customRoles</c>, <c>customName</c>, …) to its own
/// token claim by member path.
/// </remarks>
internal sealed record OidcProvisionResponse {

	/// <summary>
	/// <see langword="true"/> when the provisioner allowed the user;
	/// <see langword="false"/> when the user was denied.
	/// </summary>
	[JsonPropertyName("allowed")]
	public bool Allowed { get; init; }

	/// <summary>
	/// The provisioned claims to embed in the issued token, keyed by their <c>custom*</c> wire
	/// name. Empty when the user was denied, and a legitimate empty when the user is admitted
	/// with no minted claims.
	/// </summary>
	[JsonPropertyName("customClaims")]
	public IReadOnlyDictionary<string, string[]> CustomClaims { get; init; } = Empty;

	/// <summary>
	/// Echoes the correlation identifier supplied in the request.
	/// </summary>
	[JsonPropertyName("correlationId")]
	public string CorrelationId { get; init; } = "";

	private static readonly IReadOnlyDictionary<string, string[]> Empty = new Dictionary<string, string[]>();

	/// <summary>An admit response carrying the projected <c>custom*</c> claim map.</summary>
	internal static OidcProvisionResponse Allow(
		string correlationId,
		IReadOnlyDictionary<string, string[]> claims) =>
		new() {
			Allowed = true,
			CustomClaims = claims,
			CorrelationId = correlationId
		};

	/// <summary>A deny response — no claims.</summary>
	internal static OidcProvisionResponse Deny(string correlationId) =>
		new() {
			Allowed = false,
			CorrelationId = correlationId
		};

}
