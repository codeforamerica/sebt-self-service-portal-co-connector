using System.Text;
using System.Text.Json;
using Microsoft.Kiota.Abstractions.Authentication;

namespace SEBT.Portal.StatePlugins.CO.CbmsApi;

/// <summary>
/// <see cref="IAccessTokenProvider"/> that obtains OAuth 2.0 bearer tokens via the
/// client_credentials grant. Tokens are cached and refreshed 60 seconds before expiry
/// using a <see cref="SemaphoreSlim"/> for thread safety.
/// </summary>
public class ClientCredentialsTokenProvider : IAccessTokenProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _tokenEndpointUrl;

    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private string? _cachedToken;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;

    /// <summary>
    /// How many seconds before the token's actual expiry to trigger a refresh.
    /// </summary>
    private static readonly TimeSpan RefreshBuffer = TimeSpan.FromSeconds(60);

    public ClientCredentialsTokenProvider(
        HttpClient httpClient,
        string clientId,
        string clientSecret,
        string tokenEndpointUrl)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientSecret);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenEndpointUrl);

        _httpClient = httpClient;
        _clientId = clientId;
        _clientSecret = clientSecret;
        _tokenEndpointUrl = tokenEndpointUrl;
    }

    public AllowedHostsValidator AllowedHostsValidator { get; } = new();

    public async Task<string> GetAuthorizationTokenAsync(
        Uri uri,
        Dictionary<string, object>? additionalAuthenticationContext = null,
        CancellationToken cancellationToken = default)
    {
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiry)
        {
            return _cachedToken;
        }

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring the lock.
            if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiry)
            {
                return _cachedToken;
            }

            var (token, expiresIn) = await RequestTokenAsync(cancellationToken);

            _cachedToken = token;
            _tokenExpiry = DateTimeOffset.UtcNow.AddSeconds(expiresIn) - RefreshBuffer;

            return _cachedToken;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<(string Token, int ExpiresIn)> RequestTokenAsync(
        CancellationToken cancellationToken)
    {
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));

        using var request = new HttpRequestMessage(HttpMethod.Post, _tokenEndpointUrl);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Basic", credentials);
        request.Content = new FormUrlEncodedContent(
            new[] { new KeyValuePair<string, string>("grant_type", "client_credentials") });

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Token request failed {(int)response.StatusCode} {response.ReasonPhrase}: {body}");
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var root = json.RootElement;
        var accessToken = root.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Token response missing access_token.");
        var expiresIn = ParseExpiresIn(root.GetProperty("expires_in"));

        return (accessToken, expiresIn);
    }

    private static int ParseExpiresIn(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var n))
            return n;
        var s = element.GetString();
        return int.TryParse(s, out var parsed) ? parsed : 0;
    }
}
