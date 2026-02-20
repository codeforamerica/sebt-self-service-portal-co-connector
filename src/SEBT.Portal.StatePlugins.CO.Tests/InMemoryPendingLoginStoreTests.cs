using SEBT.Portal.StatePlugins.CO.MyColorado;

namespace SEBT.Portal.StatePlugins.CO.Tests;

public class InMemoryPendingLoginStoreTests
{
    [Fact]
    public async Task SavePendingLoginAsync_removes_expired_entries_before_adding_new()
    {
        var store = new InMemoryPendingLoginStore();
        var data = new PendingLoginData("verifier", "nonce", "http://localhost:8080/callback");

        // Save an entry that is already expired (negative TTL)
        await store.SavePendingLoginAsync("state1", data, TimeSpan.FromSeconds(-1));
        Assert.Equal(1, store.Count);

        // Saving a new entry triggers cleanup; the expired entry should be removed and only the new one kept
        await store.SavePendingLoginAsync("state2", data, TimeSpan.FromMinutes(10));

        Assert.Equal(1, store.Count);

        var expiredRetrieved = await store.GetAndRemovePendingLoginAsync("state1");
        Assert.Null(expiredRetrieved);

        // Valid state2 is present
        var retrieved = await store.GetAndRemovePendingLoginAsync("state2");
        Assert.NotNull(retrieved);
        Assert.Equal("http://localhost:8080/callback", retrieved.RedirectUri);
    }
}
