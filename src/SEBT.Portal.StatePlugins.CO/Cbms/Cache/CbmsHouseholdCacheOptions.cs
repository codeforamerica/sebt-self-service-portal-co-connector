namespace SEBT.Portal.StatePlugins.CO.Cbms.Cache;

internal sealed class CbmsHouseholdCacheOptions
{
    public int SoftExpirationMinutes { get; set; } = 15;
    public int HardExpirationMinutes { get; set; } = 240;
    public int NegativeCacheSeconds { get; set; } = 60;
    public int BackgroundRefreshTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// L1 (in-process) cache TTL. Bounds the staleness window when a write-through on
    /// one pod is not visible to other pods' L1 caches — after this elapses, other pods
    /// fall through to L2 (Redis) and pick up the fresh value. Should be shorter than
    /// <see cref="SoftExpirationMinutes"/> so SWR semantics still apply on warm L1 entries.
    /// </summary>
    public int LocalCacheExpirationSeconds { get; set; } = 60;

    public TimeSpan SoftExpiration => TimeSpan.FromMinutes(SoftExpirationMinutes);
    public TimeSpan HardExpiration => TimeSpan.FromMinutes(HardExpirationMinutes);
    public TimeSpan NegativeCacheExpiration => TimeSpan.FromSeconds(NegativeCacheSeconds);
    public TimeSpan BackgroundRefreshTimeout => TimeSpan.FromSeconds(BackgroundRefreshTimeoutSeconds);
    public TimeSpan LocalCacheExpiration => TimeSpan.FromSeconds(LocalCacheExpirationSeconds);

    public IEnumerable<string> Validate()
    {
        if (SoftExpirationMinutes <= 0) yield return "SoftExpirationMinutes must be > 0";
        if (HardExpirationMinutes <= 0) yield return "HardExpirationMinutes must be > 0";
        if (SoftExpirationMinutes >= HardExpirationMinutes) yield return "SoftExpirationMinutes must be < HardExpirationMinutes";
        if (NegativeCacheSeconds < 0) yield return "NegativeCacheSeconds must be >= 0";
        if (BackgroundRefreshTimeoutSeconds <= 0) yield return "BackgroundRefreshTimeoutSeconds must be > 0";
        if (LocalCacheExpirationSeconds <= 0) yield return "LocalCacheExpirationSeconds must be > 0";
    }
}
