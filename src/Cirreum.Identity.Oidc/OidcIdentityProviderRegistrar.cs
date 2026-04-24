namespace Cirreum.Identity;

using Cirreum.Identity.Configuration;
using Cirreum.Identity.Oidc;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Registrar for Oidc identity provider instances. Wires up a webhook-style pre-token
/// provisioning endpoint compatible with Descope, Auth0, and any OIDC IdP that can POST
/// a custom-claims payload to an HTTP endpoint.
/// </summary>
/// <remarks>
/// <para>
/// For each enabled instance in configuration, this registrar:
/// </para>
/// <list type="number">
///   <item><description>Registers a keyed-singleton <see cref="OidcSharedSecretValidator"/> for the instance (services phase).</description></item>
///   <item><description>Maps an anonymous POST endpoint at <c>settings.Route</c> that validates the shared secret, deserializes the payload, resolves the keyed <see cref="Provisioning.IUserProvisioner"/> for <c>settings.Source</c>, and translates the result into the IdP's expected response shape (endpoints phase).</description></item>
/// </list>
/// <para>
/// The keyed <see cref="Provisioning.IUserProvisioner"/> that fulfils each instance is
/// registered separately by the app through the Runtime Extensions layer
/// (<c>builder.AddIdentity().AddProvisioner&lt;T&gt;("instance_key")</c>).
/// </para>
/// </remarks>
public sealed class OidcIdentityProviderRegistrar
	: IdentityProviderRegistrar<OidcIdentityProviderSettings, OidcIdentityProviderInstanceSettings> {

	/// <inheritdoc/>
	public override string ProviderName => "Oidc";

	/// <inheritdoc/>
	public override void ValidateSettings(OidcIdentityProviderInstanceSettings settings) {
		if (string.IsNullOrWhiteSpace(settings.SharedSecret)) {
			throw new InvalidOperationException(
				$"Oidc provider instance '{settings.Source}' requires a SharedSecret. " +
				$"Configure it at Cirreum:Identity:Providers:Oidc:Instances:{settings.Source}:SharedSecret " +
				$"(prefer a secret store — Key Vault, user-secrets, etc. — never commit to source control).");
		}
	}

	/// <inheritdoc/>
	protected override void RegisterProvisioner(
		string key,
		OidcIdentityProviderInstanceSettings settings,
		IServiceCollection services,
		IConfiguration configuration) {

		// Per-instance validator — the expected secret is immutable for the instance's
		// lifetime, so a singleton avoids per-request UTF-8 encoding overhead.
		services.AddKeyedSingleton(key, (sp, _) =>
			new OidcSharedSecretValidator(
				settings,
				sp.GetRequiredService<ILogger<OidcSharedSecretValidator>>()));
	}

	/// <inheritdoc/>
	protected override void MapProvisioner(
		string key,
		OidcIdentityProviderInstanceSettings settings,
		IEndpointRouteBuilder endpoints) {

		endpoints.MapPost(settings.Route, async (HttpContext ctx, CancellationToken ct) => {
			var sp = ctx.RequestServices;
			var validator = sp.GetRequiredKeyedService<OidcSharedSecretValidator>(key);
			var logger = sp.GetRequiredService<ILogger<OidcConnectorHandler>>();
			var handler = new OidcConnectorHandler(settings, validator, sp, logger);
			return await handler.HandleAsync(ctx.Request, ct);
		})
		.AllowAnonymous()
		.ExcludeFromDescription();
	}
}
