namespace Cirreum.Identity.Oidc;

using Cirreum.Identity.Configuration;
using Cirreum.Identity.Oidc.Models;
using Cirreum.Identity.Provisioning;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles an OIDC IdP's pre-token webhook invocation for a single configured Oidc instance.
/// Validates the shared secret, checks the calling app against any configured allowlist,
/// dispatches to the app's <see cref="IUserProvisioner"/>, and maps the result to the
/// expected JSON response.
/// </summary>
internal sealed partial class OidcConnectorHandler(
	OidcIdentityProviderInstanceSettings settings,
	OidcSharedSecretValidator secretValidator,
	IServiceProvider services,
	ILogger<OidcConnectorHandler> logger) {

	public async Task<IResult> HandleAsync(HttpRequest request, CancellationToken cancellationToken = default) {

		// 1. Validate shared secret
		if (!secretValidator.Validate(request)) {
			return Results.Unauthorized();
		}

		// 2. Deserialize payload
		OidcProvisionRequest? payload;
		try {
			payload = await request.ReadFromJsonAsync(
				OidcJsonContext.Default.OidcProvisionRequest,
				cancellationToken);
		} catch (Exception ex) {
			Log.DeserializationFailed(logger, ex, settings.Source);
			return Results.BadRequest("Invalid request body");
		}

		if (payload is null) {
			Log.DeserializationFailed(logger, null, settings.Source);
			return Results.BadRequest("Invalid request body");
		}

		// 3. Validate required fields
		if (string.IsNullOrWhiteSpace(payload.ExternalUserId)) {
			Log.MissingExternalUserId(logger, settings.Source);
			return Results.BadRequest("Missing externalUserId");
		}

		// 4. Validate calling app (when an allowlist is configured)
		if (settings.HasAllowedAppIds()) {
			var allowedApps = settings.GetAllowedAppIdSet();
			if (!allowedApps.Contains(payload.ClientAppId)) {
				Log.AppNotAllowed(logger, payload.ClientAppId, settings.Source);
				return Results.Forbid();
			}
		}

		// 5. Provision user
		var provisionContext = new ProvisionContext {
			Source = settings.Source,
			ExternalUserId = payload.ExternalUserId,
			CorrelationId = payload.CorrelationId,
			ClientAppId = payload.ClientAppId,
			Email = payload.Email
		};

		var provisioner = services.GetRequiredKeyedService<IUserProvisioner>(settings.Source);
		ProvisionResult provisionResult;
		try {
			provisionResult = await provisioner.ProvisionAsync(provisionContext, cancellationToken);
		} catch (Exception ex) {
			Log.ProvisionerFailed(logger, ex, provisionContext.ExternalUserId, settings.Source);
			return Results.Problem("User provisioning failed.", statusCode: 500);
		}

		// 6. Map provision result to response
		if (provisionResult is ProvisionResult.Denied) {
			Log.UserDenied(logger, provisionContext.ExternalUserId, settings.Source);
			var denyBody = new OidcProvisionResponse {
				Allowed = false,
				Roles = [],
				CorrelationId = payload.CorrelationId
			};
			return Results.Json(
				denyBody,
				OidcJsonContext.Default.OidcProvisionResponse,
				statusCode: StatusCodes.Status403Forbidden);
		}

		if (provisionResult is not ProvisionResult.Allowed { Roles: { Count: > 0 } roles }) {
			if (provisionResult is ProvisionResult.Allowed) {
				Log.ProvisionerAllowedWithNoRoles(logger, provisionContext.ExternalUserId, settings.Source);
			} else {
				Log.ProvisionerFailed(logger, null, provisionContext.ExternalUserId, settings.Source);
			}
			return Results.Problem("User provisioning failed.", statusCode: 500);
		}

		var rolesStr = string.Join(",", roles);
		Log.IssuingRoles(logger, rolesStr, provisionContext.ExternalUserId, payload.CorrelationId, settings.Source);

		// 7. Build and return response
		var body = new OidcProvisionResponse {
			Allowed = true,
			Roles = [.. roles],
			CorrelationId = payload.CorrelationId
		};

		return Results.Json(body, OidcJsonContext.Default.OidcProvisionResponse);
	}

	private static partial class Log {
		[LoggerMessage(Level = LogLevel.Warning, Message = "Failed to deserialize Oidc provisioning request body for instance '{Source}'.")]
		internal static partial void DeserializationFailed(ILogger logger, Exception? ex, string source);

		[LoggerMessage(Level = LogLevel.Warning, Message = "Missing externalUserId in Oidc provisioning request for instance '{Source}'.")]
		internal static partial void MissingExternalUserId(ILogger logger, string source);

		[LoggerMessage(Level = LogLevel.Warning, Message = "App '{AppId}' is not in the allowed list for instance '{Source}'.")]
		internal static partial void AppNotAllowed(ILogger logger, string appId, string source);

		[LoggerMessage(Level = LogLevel.Information, Message = "User '{UserId}' was denied by provisioner for instance '{Source}'. Blocking token issuance.")]
		internal static partial void UserDenied(ILogger logger, string userId, string source);

		[LoggerMessage(Level = LogLevel.Warning, Message = "Provisioner returned Allowed with no roles for user '{UserId}' on instance '{Source}'. Blocking token issuance.")]
		internal static partial void ProvisionerAllowedWithNoRoles(ILogger logger, string userId, string source);

		[LoggerMessage(Level = LogLevel.Error, Message = "Provisioner failed for user '{UserId}' on instance '{Source}'. Blocking token issuance.")]
		internal static partial void ProvisionerFailed(ILogger logger, Exception? ex, string userId, string source);

		[LoggerMessage(Level = LogLevel.Information, Message = "Issuing roles '{Roles}' for user '{UserId}' (correlation: {CorrelationId}, instance: {Source}).")]
		internal static partial void IssuingRoles(ILogger logger, string roles, string userId, string correlationId, string source);
	}
}
