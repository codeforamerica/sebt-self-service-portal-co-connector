using System.Composition;
using Microsoft.Extensions.Configuration;
using SEBT.Portal.StatesPlugins.Interfaces;
using SEBT.Portal.StatesPlugins.Interfaces.Models;
using SEBT.Portal.StatePlugins.CO.MyColorado;

namespace SEBT.Portal.StatePlugins.CO;

/// <summary>
/// Exposes MyColorado OIDC to the portal via <see cref="IStateOidcLoginService"/>.
/// Frontend does Authorization Code + PKCE and POSTs id_token; backend calls ValidateIdTokenAsync.
/// </summary>
[Export(typeof(IStatePlugin))]
[Export(typeof(IStateOidcLoginService))]
[ExportMetadata("StateCode", "CO")]
public class ColoradoOidcLoginService : IStateOidcLoginService
{
    public string StateCode => "CO";

    private readonly MyColoradoOidcService _oidcService;

    [ImportingConstructor]
    public ColoradoOidcLoginService([Import] IConfiguration configuration)
    {
        var options = configuration
            .GetSection("MyColorado")
            .Get<MyColoradoOidcOptions>() ?? new MyColoradoOidcOptions();
        var httpClient = new HttpClient();
        _oidcService = new MyColoradoOidcService(options, httpClient);
    }

    /// <inheritdoc />
    public Task<(string AuthorizationUrl, string State)> PrepareAuthorizationAsync(
        string redirectUri,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            "Use the frontend OIDC flow (Authorization Code + PKCE) and send the id_token to the backend; then call ValidateIdTokenAsync.");
    }

    /// <inheritdoc />
    public Task<StateAuthContext> ExchangeCodeForTokensAsync(
        string code,
        string state,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            "Use the frontend OIDC flow and send the id_token to the backend; then call ValidateIdTokenAsync.");
    }

    /// <summary>
    /// Validates an id_token JWT from the frontend (frontend-as-client flow).
    /// Call this when the frontend has completed the OIDC Authorization Code + PKCE flow and sends the id_token to the backend.
    /// </summary>
    public async Task<StateAuthContext> ValidateIdTokenAsync(
        string idToken,
        CancellationToken cancellationToken = default)
    {
        var result = await _oidcService.ValidateIdTokenAsync(idToken, cancellationToken).ConfigureAwait(false);
        return new StateAuthContext(
            result.IdToken,
            result.AccessToken,
            result.IdTokenClaims);
    }
}
