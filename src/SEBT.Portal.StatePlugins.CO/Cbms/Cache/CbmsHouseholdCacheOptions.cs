namespace SEBT.Portal.StatePlugins.CO.Cbms.Cache;

internal sealed class CbmsHouseholdCacheOptions
{
    public int SoftExpirationMinutes { get; set; } = 15;
    public int HardExpirationMinutes { get; set; } = 240;
    public int NegativeCacheSeconds { get; set; } = 60;
    public int BackgroundRefreshTimeoutSeconds { get; set; } = 60;

    public TimeSpan SoftExpiration => TimeSpan.FromMinutes(SoftExpirationMinutes);
    public TimeSpan HardExpiration => TimeSpan.FromMinutes(HardExpirationMinutes);
    public TimeSpan NegativeCacheExpiration => TimeSpan.FromSeconds(NegativeCacheSeconds);
    public TimeSpan BackgroundRefreshTimeout => TimeSpan.FromSeconds(BackgroundRefreshTimeoutSeconds);

    public IEnumerable<string> Validate()
    {
        if (SoftExpirationMinutes <= 0) yield return "SoftExpirationMinutes must be > 0";
        if (HardExpirationMinutes <= 0) yield return "HardExpirationMinutes must be > 0";
        if (SoftExpirationMinutes >= HardExpirationMinutes) yield return "SoftExpirationMinutes must be < HardExpirationMinutes";
        if (NegativeCacheSeconds < 0) yield return "NegativeCacheSeconds must be >= 0";
        if (BackgroundRefreshTimeoutSeconds <= 0) yield return "BackgroundRefreshTimeoutSeconds must be > 0";
    }
}
