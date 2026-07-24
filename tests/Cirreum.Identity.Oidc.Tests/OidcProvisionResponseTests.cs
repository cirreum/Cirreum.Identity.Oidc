namespace Cirreum.Identity.Oidc.Tests;

using System.Text.Json;
using Cirreum.Identity.Oidc;
using Cirreum.Identity.Oidc.Models;

public class OidcProvisionResponseTests {

	private static IReadOnlyDictionary<string, string[]> MapOf(params IdentityClaim[] claims) =>
		((IReadOnlyList<IdentityClaim>)claims).ToClaimMap();

	[Fact]
	public void Allow_carries_the_claim_map_and_echoes_the_correlation_id() {
		var response = OidcProvisionResponse.Allow("corr-1", MapOf(IdentityClaim.Roles("admin")));

		response.Allowed.Should().BeTrue();
		response.CorrelationId.Should().Be("corr-1");
		response.CustomClaims[CustomClaimNames.Roles].Should().BeEquivalentTo("admin");
	}

	[Fact]
	public void Deny_is_not_allowed_and_carries_no_claims() {
		var response = OidcProvisionResponse.Deny("corr-2");

		response.Allowed.Should().BeFalse();
		response.CorrelationId.Should().Be("corr-2");
		response.CustomClaims.Should().BeEmpty();
	}

	[Fact]
	public void Allow_with_no_claims_is_valid() {
		var response = OidcProvisionResponse.Allow("corr-3", MapOf());

		response.Allowed.Should().BeTrue();
		response.CustomClaims.Should().BeEmpty();
	}

	[Fact]
	public void Claims_serialize_under_a_customClaims_object_with_no_top_level_roles() {
		var response = OidcProvisionResponse.Allow("corr-9", MapOf(
			IdentityClaim.Roles("subscriber", "admin"),
			IdentityClaim.Name("Jane Smith"),
			IdentityClaim.Of("tenant", "acme")));

		var json = JsonSerializer.Serialize(response, OidcJsonContext.Default.OidcProvisionResponse);

		using var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;

		root.GetProperty("allowed").GetBoolean().Should().BeTrue();
		root.GetProperty("correlationId").GetString().Should().Be("corr-9");

		// No former top-level "roles" field.
		root.TryGetProperty("roles", out _).Should().BeFalse();

		var custom = root.GetProperty("customClaims");
		custom.GetProperty("customRoles").EnumerateArray().Select(e => e.GetString())
			.Should().BeEquivalentTo("subscriber", "admin");
		custom.GetProperty("customName").EnumerateArray().Select(e => e.GetString())
			.Should().BeEquivalentTo("Jane Smith");
		custom.GetProperty("customTenant").EnumerateArray().Select(e => e.GetString())
			.Should().BeEquivalentTo("acme");
	}

	[Fact]
	public void Every_custom_claim_serializes_as_an_array() {
		// OIDC emits arrays uniformly (unlike Entra's scalar-when-single), so the hand-authored
		// flow reads every claim the same way.
		var response = OidcProvisionResponse.Allow("c", MapOf(IdentityClaim.Name("Jane Smith")));

		var json = JsonSerializer.Serialize(response, OidcJsonContext.Default.OidcProvisionResponse);

		using var doc = JsonDocument.Parse(json);
		doc.RootElement.GetProperty("customClaims").GetProperty("customName").ValueKind
			.Should().Be(JsonValueKind.Array);
	}

	[Fact]
	public void A_denied_response_serializes_with_an_empty_customClaims_object() {
		var json = JsonSerializer.Serialize(OidcProvisionResponse.Deny("c"), OidcJsonContext.Default.OidcProvisionResponse);

		using var doc = JsonDocument.Parse(json);
		doc.RootElement.GetProperty("allowed").GetBoolean().Should().BeFalse();
		doc.RootElement.GetProperty("customClaims").EnumerateObject().Should().BeEmpty();
	}
}
