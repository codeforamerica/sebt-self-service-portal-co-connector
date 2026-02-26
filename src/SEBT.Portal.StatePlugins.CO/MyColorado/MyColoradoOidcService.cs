using System.Collections.Generic;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.JsonWebTokens;

namespace SEBT.Portal.StatePlugins.CO.MyColorado;

/// <summary>
/// Validates id_token JWTs from MyColorado (PingOne) using the IdP's JWKS.
/// Frontend does the Authorization Code + PKCE flow and sends the id_token to the backend; backend only validates.
/// </summary>
public class MyColoradoOidcService
{
    private readonly MyColoradoOidcOptions _options;
    private readonly HttpClient _httpClient;
    private readonly IConfigurationManager<OpenIdConnectConfiguration> _configManager;

    public MyColoradoOidcService(MyColoradoOidcOptions options, HttpClient httpClient)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

        _configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            _options.DiscoveryEndpoint,
            new OpenIdConnectConfigurationRetriever(),
            _httpClient);
    }

    /// <summary>
    /// Validates an ID token (JWT) issued by MyColorado using the IdP's JWKS.
    /// Use this when the frontend has completed the Authorization Code + PKCE flow and sends the id_token to the backend.
    /// </summary>
    /// <param name="idToken">The raw id_token JWT from the frontend.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Decoded claims and the same id_token (no access token in this flow).</returns>
    public async Task<MyColoradoTokenResult> ValidateIdTokenAsync(
        string idToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idToken))
            throw new ArgumentException("ID token is required.", nameof(idToken));

        var config = await _configManager.GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
        var claims = await ValidateIdTokenInternalAsync(idToken, config, cancellationToken).ConfigureAwait(false);
        return new MyColoradoTokenResult(
            AccessToken: string.Empty,
            IdToken: idToken,
            IdTokenClaims: claims);
    }

    private async Task<IReadOnlyDictionary<string, object>> ValidateIdTokenInternalAsync(
        string idToken,
        OpenIdConnectConfiguration config,
        CancellationToken cancellationToken)
    {
        var validationParameters = new TokenValidationParameters
        {
            ValidIssuer = config.Issuer,
            ValidAudience = _options.ClientId,
            IssuerSigningKeys = config.SigningKeys,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };

        var handler = new JsonWebTokenHandler();
        var result = await handler.ValidateTokenAsync(idToken, validationParameters).ConfigureAwait(false);
        if (result.Exception != null)
            throw result.Exception;

        var claims = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in result.Claims)
            claims[kv.Key] = kv.Value ?? (object)string.Empty;
        return claims;
    }
}
