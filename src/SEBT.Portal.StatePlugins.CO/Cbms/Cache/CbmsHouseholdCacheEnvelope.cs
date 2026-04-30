using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;

namespace SEBT.Portal.StatePlugins.CO.Cbms.Cache;

internal sealed record CbmsHouseholdCacheEnvelope(
    GetAccountDetailsResponse Response,
    DateTimeOffset SoftExpiryUtc,
    DateTimeOffset HardExpiryUtc,
    DateTimeOffset CachedAtUtc);
