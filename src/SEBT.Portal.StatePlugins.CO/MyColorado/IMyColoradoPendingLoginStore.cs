namespace SEBT.Portal.StatePlugins.CO.MyColorado;

/// <summary>
/// Stores pending OIDC login data (state -> code_verifier, nonce, redirect_uri) until the callback.
/// The host (e.g. portal) should implement this with distributed cache or similar.
/// </summary>
public interface IMyColoradoPendingLoginStore
{
    /// <summary>
    /// Save pending login data for the given state. Overwrites any existing entry.
    /// </summary>
    Task SavePendingLoginAsync(string state, PendingLoginData data, TimeSpan expiration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieve and remove pending login data for the given state. Returns null if not found or expired.
    /// </summary>
    Task<PendingLoginData?> GetAndRemovePendingLoginAsync(string state, CancellationToken cancellationToken = default);
}
