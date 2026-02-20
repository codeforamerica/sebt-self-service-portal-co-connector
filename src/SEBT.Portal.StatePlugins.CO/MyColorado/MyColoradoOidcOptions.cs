namespace SEBT.Portal.StatePlugins.CO.MyColorado;

/// <summary>
/// Configuration for MyColorado (PingOne) OIDC authorization code + PKCE flow.
/// </summary>
public class MyColoradoOidcOptions
{
    /// <summary>
    /// OIDC discovery document URL (e.g. https://auth.pingone.com/.../as/.well-known/openid-configuration).
    /// </summary>
    public string DiscoveryEndpoint { get; set; } = "https://auth.pingone.com/e8e64475-39e1-43de-964b-3bc2e835a2f5/as/.well-known/openid-configuration";

    /// <summary>
    /// Issuer ID (authority). Must match the discovery document issuer.
    /// </summary>
    public string Authority { get; set; } = "https://auth.pingone.com/e8e64475-39e1-43de-964b-3bc2e835a2f5/as";

    /// <summary>
    /// OAuth client ID from PingOne/MyColorado.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// OAuth client secret.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Allowed redirect URIs. One of these must be used when starting the login flow.
    /// </summary>
    public IList<string> RedirectUris { get; set; } = new List<string>
    {
        "http://localhost:4200/callback",
        "http://localhost:8080/callback"
    };

    /// <summary>
    /// Scopes to request (e.g. "openid profile").
    /// </summary>
    public string Scopes { get; set; } = "openid";

    /// <summary>
    /// How long the pending login (state/code_verifier) is valid, in seconds.
    /// </summary>
    public int SessionExpirationSeconds { get; set; } = 600;
}
