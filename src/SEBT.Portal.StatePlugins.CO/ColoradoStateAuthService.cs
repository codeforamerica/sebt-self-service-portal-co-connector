using System.Security.Claims;
using System.Composition;
using SEBT.Portal.StatesPlugins.Interfaces;
using SEBT.Portal.StatesPlugins.Interfaces.Models;

namespace SEBT.Portal.StatePlugins.CO;

/// <summary>
/// Returns IdP-derived claims (e.g. MyColorado phone, name) from the current user's claims.
/// The portal JWT includes these at OIDC complete-login; this implementation reads from
/// the <see cref="ClaimsPrincipal"/> passed by the host.
/// </summary>
[Export(typeof(IStatePlugin))]
[Export(typeof(IStateAuthService))]
[ExportMetadata("StateCode", "CO")]
public class ColoradoStateAuthService : IStateAuthService
{
    /// <inheritdoc />
    public Task<IdpClaimsView?> GetIdpClaimsViewAsync(ClaimsPrincipal? user, CancellationToken cancellationToken = default)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return Task.FromResult<IdpClaimsView?>(null);

        static string? GetClaim(ClaimsPrincipal p, string type) =>
            p.Claims.FirstOrDefault(c => string.Equals(c.Type, type, StringComparison.OrdinalIgnoreCase))?.Value;

        var phone = GetClaim(user, "phone");
        var givenName = GetClaim(user, "givenName");
        var familyName = GetClaim(user, "familyName");
        var email = GetClaim(user, "email");
        var sub = GetClaim(user, "sub");

        // If we have no IdP-specific claims, return null so callers know this user didn't sign in via state IdP.
        if (string.IsNullOrEmpty(phone) && string.IsNullOrEmpty(givenName) && string.IsNullOrEmpty(familyName) && string.IsNullOrEmpty(email) && string.IsNullOrEmpty(sub))
            return Task.FromResult<IdpClaimsView?>(null);

        return Task.FromResult<IdpClaimsView?>(new IdpClaimsView(
            Phone: phone,
            GivenName: givenName,
            FamilyName: familyName,
            Email: email,
            Sub: sub));
    }
}
