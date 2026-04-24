namespace Cirreum.Identity.Oidc;

using System.Text.Json.Serialization;
using Cirreum.Identity.Oidc.Models;

[JsonSerializable(typeof(OidcProvisionRequest))]
[JsonSerializable(typeof(OidcProvisionResponse))]
internal sealed partial class OidcJsonContext : JsonSerializerContext {
}
