namespace SEBT.Portal.StatePlugins.CO.MyColorado;

/// <summary>
/// Data stored server-side for the duration of the OIDC authorization flow (state -> callback).
/// </summary>
/// <param name="CodeVerifier">PKCE code_verifier used when exchanging the authorization code.</param>
/// <param name="Nonce">Nonce sent in the authorization request; must match id_token claim.</param>
/// <param name="RedirectUri">Redirect URI used for this login attempt (must match token request).</param>
public record PendingLoginData(string CodeVerifier, string Nonce, string RedirectUri);
