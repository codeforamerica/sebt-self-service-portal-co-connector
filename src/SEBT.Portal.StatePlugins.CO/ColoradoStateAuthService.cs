using System.Composition;
using SEBT.Portal.StatesPlugins.Interfaces;
using SEBT.Portal.StatesPlugins.Interfaces.Models;

namespace SEBT.Portal.StatePlugins.CO;

/// <summary>
/// Returns the state auth context (MyColorado IdP tokens) for the current request session
/// by reading from the portal's <see cref="IStateAuthStore"/> using the session id from the cookie.
/// </summary>
[Export(typeof(IStatePlugin))]
[Export(typeof(IStateAuthService))]
[ExportMetadata("StateCode", "CO")]
public class ColoradoStateAuthService : IStateAuthService
{
    private readonly IStateAuthStore _store;
    private readonly IStateAuthSessionAccessor _sessionAccessor;

    [ImportingConstructor]
    public ColoradoStateAuthService(
        [Import] IStateAuthStore store,
        [Import] IStateAuthSessionAccessor sessionAccessor)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _sessionAccessor = sessionAccessor ?? throw new ArgumentNullException(nameof(sessionAccessor));
    }

    /// <inheritdoc />
    public async Task<StateAuthContext?> GetStateAuthAsync(CancellationToken cancellationToken = default)
    {
        var sessionId = _sessionAccessor.GetCurrentSessionId();
        if (string.IsNullOrEmpty(sessionId))
            return null;

        return await _store.GetAsync(sessionId, cancellationToken).ConfigureAwait(false);
    }
}
