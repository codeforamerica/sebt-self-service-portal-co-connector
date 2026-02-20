using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.JsonWebTokens;

namespace SEBT.Portal.StatePlugins.CO.MyColorado;

/// <summary>
/// Performs the MyColorado (PingOne) OIDC authorization code + PKCE flow:
/// 1) Prepare authorization URL and store pending login (state/code_verifier/nonce).
/// 2) Exchange authorization code for tokens and validate the ID token.
/// </summary>
public class MyColoradoOidcService
{
    private readonly MyColoradoOidcOptions _options;
    private readonly IMyColoradoPendingLoginStore _pendingLoginStore;
    private readonly HttpClient _httpClient;
    private readonly IConfigurationManager<OpenIdConnectConfiguration> _configManager;

    public MyColoradoOidcService(
        MyColoradoOidcOptions options,
        IMyColoradoPendingLoginStore pendingLoginStore,
        HttpClient httpClient)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _pendingLoginStore = pendingLoginStore ?? throw new ArgumentNullException(nameof(pendingLoginStore));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

        _configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            _options.DiscoveryEndpoint,
            new OpenIdConnectConfigurationRetriever(),
            _httpClient);
    }

    /// <summary>
    /// Prepares the MyColorado authorization URL with PKCE, state, and nonce.
    /// Stores the pending login server-side so the callback can exchange the code.
    /// </summary>
    /// <param name="redirectUri">Must be one of the configured RedirectUris.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The URL to redirect the user to, and the state (e.g. for cookie).</returns>
    public async Task<(string AuthorizationUrl, string State)> PrepareAuthorizationAsync(
        string redirectUri,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(redirectUri))
            throw new ArgumentException("Redirect URI is required.", nameof(redirectUri));

        var allowed = _options.RedirectUris?.Contains(redirectUri, StringComparer.OrdinalIgnoreCase) ?? false;
        if (!allowed)
            throw new ArgumentException($"Redirect URI is not allowed: {redirectUri}", nameof(redirectUri));

        var config = await _configManager.GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
        var authEndpoint = config.AuthorizationEndpoint
            ?? throw new InvalidOperationException("Discovery document has no authorization_endpoint.");

        var (codeVerifier, codeChallenge) = GeneratePkcePair();
        var state = GenerateRandomUrlSafe(32);
        var nonce = GenerateRandomUrlSafe(32);

        var expiration = TimeSpan.FromSeconds(_options.SessionExpirationSeconds);
        await _pendingLoginStore.SavePendingLoginAsync(
            state,
            new PendingLoginData(codeVerifier, nonce, redirectUri),
            expiration,
            cancellationToken).ConfigureAwait(false);

        var query = new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = _options.Scopes,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
            ["state"] = state,
            ["nonce"] = nonce
        };
        var queryString = string.Join("&", query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        var authorizationUrl = $"{authEndpoint}?{queryString}";

        return (authorizationUrl, state);
    }

    /// <summary>
    /// Exchanges the authorization code for tokens and validates the ID token.
    /// </summary>
    /// <param name="code">The authorization code from the callback query.</param>
    /// <param name="state">The state from the callback query (used to retrieve code_verifier).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Access token, ID token, and decoded ID token claims.</returns>
    public async Task<MyColoradoTokenResult> ExchangeCodeForTokensAsync(
        string code,
        string state,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(code))
            throw new ArgumentException("Code is required.", nameof(code));
        if (string.IsNullOrEmpty(state))
            throw new ArgumentException("State is required.", nameof(state));

        var pending = await _pendingLoginStore.GetAndRemovePendingLoginAsync(state, cancellationToken).ConfigureAwait(false);
        if (pending == null)
            throw new InvalidOperationException("Invalid or expired state. Please start the login flow again.");

        var config = await _configManager.GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
        var tokenEndpoint = config.TokenEndpoint
            ?? throw new InvalidOperationException("Discovery document has no token_endpoint.");

        var credentials = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
        var requestContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = pending.RedirectUri,
            ["code_verifier"] = pending.CodeVerifier
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint);
        request.Headers.TryAddWithoutValidation("Authorization", "Basic " + credentials);
        request.Content = requestContent;

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var tokenResponse = System.Text.Json.JsonSerializer.Deserialize<TokenResponse>(json)
            ?? throw new InvalidOperationException("Invalid token response.");

        if (string.IsNullOrEmpty(tokenResponse.AccessToken) || string.IsNullOrEmpty(tokenResponse.IdToken))
            throw new InvalidOperationException("Token response missing access_token or id_token.");

        var claims = await ValidateIdTokenAsync(
            tokenResponse.IdToken,
            pending.Nonce,
            config,
            cancellationToken).ConfigureAwait(false);

        return new MyColoradoTokenResult(
            tokenResponse.AccessToken,
            tokenResponse.IdToken,
            claims);
    }

    private async Task<IReadOnlyDictionary<string, object>> ValidateIdTokenAsync(
        string idToken,
        string expectedNonce,
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

        if (result.Claims.TryGetValue("nonce", out var nonceObj))
        {
            var nonce = nonceObj?.ToString();
            if (!string.Equals(nonce, expectedNonce, StringComparison.Ordinal))
                throw new InvalidOperationException("ID token nonce does not match.");
        }
        else
            throw new InvalidOperationException("ID token missing nonce claim.");

        var claims = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in result.Claims)
            claims[kv.Key] = kv.Value ?? (object)string.Empty;
        return claims;
    }

    private static (string CodeVerifier, string CodeChallenge) GeneratePkcePair(int verifierLength = 64)
    {
        if (verifierLength < 43 || verifierLength > 128)
            throw new ArgumentOutOfRangeException(nameof(verifierLength), "PKCE code_verifier must be 43–128 characters.");
        var verifier = GenerateRandomUrlSafe(verifierLength);
        var digest = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(verifier));
        var challenge = Base64UrlEncoder.Encode(digest);
        return (verifier, challenge);
    }

    private static string GenerateRandomUrlSafe(int byteCount)
    {
        var bytes = new byte[byteCount];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncoder.Encode(bytes);
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }
        [JsonPropertyName("id_token")]
        public string? IdToken { get; set; }
    }
}
