using SEBT.Portal.StatePlugins.CO.Cbms.Cache;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;

namespace SEBT.Portal.StatePlugins.CO.Tests.Cbms.Cache;

public class CbmsHouseholdCacheEnvelopeTests
{
    [Fact]
    public void Envelope_carries_response_and_expiries()
    {
        var response = new GetAccountDetailsResponse();
        var now = DateTimeOffset.UtcNow;
        var envelope = new CbmsHouseholdCacheEnvelope(
            Response: response,
            SoftExpiryUtc: now.AddMinutes(15),
            HardExpiryUtc: now.AddHours(4),
            CachedAtUtc: now);

        Assert.Same(response, envelope.Response);
        Assert.Equal(now.AddMinutes(15), envelope.SoftExpiryUtc);
        Assert.Equal(now.AddHours(4), envelope.HardExpiryUtc);
        Assert.Equal(now, envelope.CachedAtUtc);
    }

    [Fact]
    public void Envelope_is_record_with_value_equality()
    {
        var response = new GetAccountDetailsResponse();
        var now = DateTimeOffset.UtcNow;
        var a = new CbmsHouseholdCacheEnvelope(response, now.AddMinutes(15), now.AddHours(4), now);
        var b = new CbmsHouseholdCacheEnvelope(response, now.AddMinutes(15), now.AddHours(4), now);

        Assert.Equal(a, b);
    }
}
