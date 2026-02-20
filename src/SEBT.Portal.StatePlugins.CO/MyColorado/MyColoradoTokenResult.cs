namespace SEBT.Portal.StatePlugins.CO.MyColorado;

/// <summary>
/// Result of a successful code-for-tokens exchange with MyColorado.
/// </summary>
/// <param name="AccessToken">OAuth 2.0 access token (e.g. for userinfo or APIs).</param>
/// <param name="IdToken">OpenID Connect ID token (JWT).</param>
/// <param name="IdTokenClaims">Decoded id_token claims (e.g. sub, nonce, name).</param>
public record MyColoradoTokenResult(
    string AccessToken,
    string IdToken,
    IReadOnlyDictionary<string, object> IdTokenClaims);
