namespace SEBT.Portal.StatePlugins.CO.MyColorado;

/// <summary>
/// Configuration for MyColorado (PingOne) OIDC id_token validation.
/// Frontend does the Authorization Code + PKCE flow; backend only validates JWTs using discovery/JWKS.
/// </summary>
public class MyColoradoOidcOptions
{
    /// <summary>
    /// OIDC discovery document URL (e.g. https://auth.pingone.com/.../as/.well-known/openid-configuration).
    /// Used to fetch JWKS for signature validation.
    /// </summary>
    public string DiscoveryEndpoint { get; set; } = "https://auth.pingone.com/e8e64475-39e1-43de-964b-3bc2e835a2f5/as/.well-known/openid-configuration";

    /// <summary>
    /// Issuer ID (authority). Must match the discovery document issuer.
    /// </summary>
    public string Authority { get; set; } = "https://auth.pingone.com/e8e64475-39e1-43de-964b-3bc2e835a2f5/as";

    /// <summary>
    /// OAuth client ID from PingOne/MyColorado. Used as the expected audience when validating the id_token.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;
}
