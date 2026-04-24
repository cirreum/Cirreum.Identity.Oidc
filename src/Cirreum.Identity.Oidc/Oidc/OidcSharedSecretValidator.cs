namespace Cirreum.Identity.Oidc;

using Cirreum.Identity.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Validates the shared-secret authorization header sent by an OIDC IdP's pre-token
/// webhook to a Cirreum Identity Oidc provisioning endpoint.
/// </summary>
/// <remarks>
/// <para>
/// Webhooks authenticate outbound calls by attaching a static credential to each request
/// (typically an <c>Authorization: Bearer &lt;token&gt;</c> header or an <c>X-API-Key</c>
/// header). This validator performs a constant-time comparison between the configured
/// <see cref="OidcIdentityProviderInstanceSettings.SharedSecret"/> and the header value
/// supplied by the IdP.
/// </para>
/// <para>
/// The validator does not inspect the request body. If stronger per-request authenticity
/// guarantees are required (for example HMAC over the body, or an IdP-signed JWT), replace
/// this validator with an application-specific implementation.
/// </para>
/// </remarks>
internal sealed class OidcSharedSecretValidator(
	OidcIdentityProviderInstanceSettings settings,
	ILogger<OidcSharedSecretValidator> logger) {

	private readonly byte[] _expectedBytes = Encoding.UTF8.GetBytes(settings.SharedSecret);

	public bool Validate(HttpRequest request) {

		if (string.IsNullOrEmpty(settings.SharedSecret)) {
			logger.LogError(
				"OidcIdentityProviderInstanceSettings.SharedSecret is not configured for instance '{Source}'.",
				settings.Source);
			return false;
		}

		if (!request.Headers.TryGetValue(settings.AuthorizationHeaderName, out var headerValues)
			|| headerValues.Count == 0) {
			logger.LogWarning(
				"Missing '{Header}' header on Oidc provisioning request for instance '{Source}'.",
				settings.AuthorizationHeaderName, settings.Source);
			return false;
		}

		var headerValue = headerValues[0];
		if (string.IsNullOrWhiteSpace(headerValue)) {
			logger.LogWarning(
				"Empty '{Header}' header on Oidc provisioning request for instance '{Source}'.",
				settings.AuthorizationHeaderName, settings.Source);
			return false;
		}

		var presented = ExtractPresentedSecret(headerValue, settings.AuthorizationScheme);
		if (presented is null) {
			logger.LogWarning(
				"'{Header}' header did not include the expected '{Scheme}' scheme for instance '{Source}'.",
				settings.AuthorizationHeaderName, settings.AuthorizationScheme, settings.Source);
			return false;
		}

		var presentedBytes = Encoding.UTF8.GetBytes(presented);
		var match = CryptographicOperations.FixedTimeEquals(presentedBytes, this._expectedBytes);

		if (!match) {
			logger.LogWarning(
				"Oidc shared secret did not match the configured value for instance '{Source}'.",
				settings.Source);
		}

		return match;
	}

	private static string? ExtractPresentedSecret(string headerValue, string scheme) {
		headerValue = headerValue.Trim();

		if (string.IsNullOrEmpty(scheme)) {
			return headerValue;
		}

		var prefix = scheme + " ";
		if (!headerValue.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) {
			return null;
		}

		return headerValue[prefix.Length..].Trim();
	}
}
