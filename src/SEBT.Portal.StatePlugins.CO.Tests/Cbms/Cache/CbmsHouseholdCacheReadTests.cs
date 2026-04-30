using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.StatePlugins.CO.Cbms.Cache;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;
using SEBT.Portal.StatesPlugins.Interfaces.Services;

namespace SEBT.Portal.StatePlugins.CO.Tests.Cbms.Cache;

public class CbmsHouseholdCacheReadTests
{
    private const string Phone = "3035550199";

    private static GetAccountDetailsResponse Populated() => new()
    {
        StdntEnrollDtls = new() { new GetAccountStudentDetail() }
    };

    private static GetAccountDetailsResponse Empty() => new()
    {
        StdntEnrollDtls = new()
    };

    private static (CbmsHouseholdCache sut, InMemoryHybridCache hybrid, FakeFetch fetch) Build(
        CbmsHouseholdCacheOptions? options = null)
    {
        var hybrid = new InMemoryHybridCache();
        var hasher = Substitute.For<IHMACSHA256Hasher>();
        hasher.Hash(Arg.Any<string>()).Returns(c => "hash:" + c.Arg<string>());
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(CancellationToken.None);
        var fetch = new FakeFetch();
        var sut = new CbmsHouseholdCache(
            hybrid, hasher, lifetime, NullLoggerFactory.Instance,
            Options.Create(options ?? new CbmsHouseholdCacheOptions()),
            fetch.Delegate);
        return (sut, hybrid, fetch);
    }

    [Fact]
    public async Task Cache_miss_calls_CBMS_then_caches()
    {
        var (sut, _, fetch) = Build();
        fetch.NextResponse = Populated();

        var result = await sut.GetAsync(Phone, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(1, fetch.CallCount);
    }

    [Fact]
    public async Task Cache_hit_does_not_call_CBMS_again()
    {
        var (sut, _, fetch) = Build();
        fetch.NextResponse = Populated();
        await sut.GetAsync(Phone, CancellationToken.None); // primes
        fetch.CallCount = 0;

        await sut.GetAsync(Phone, CancellationToken.None);

        Assert.Equal(0, fetch.CallCount);
    }

    [Fact]
    public async Task Cache_returns_null_when_CBMS_returns_empty_household()
    {
        var (sut, _, fetch) = Build();
        fetch.NextResponse = Empty();

        var result = await sut.GetAsync(Phone, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Cache_returns_null_when_CBMS_returns_null()
    {
        var (sut, _, fetch) = Build();
        fetch.NextResponse = null;

        var result = await sut.GetAsync(Phone, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Negative_cache_does_not_immediately_refetch_within_window()
    {
        var (sut, _, fetch) = Build(new CbmsHouseholdCacheOptions { NegativeCacheSeconds = 60 });
        fetch.NextResponse = Empty();
        await sut.GetAsync(Phone, CancellationToken.None);
        fetch.CallCount = 0;

        await sut.GetAsync(Phone, CancellationToken.None);

        Assert.Equal(0, fetch.CallCount);
    }
}
