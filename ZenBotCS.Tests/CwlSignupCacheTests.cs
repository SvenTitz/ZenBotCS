using Microsoft.Extensions.Caching.Memory;
using ZenBotCS.Entities.Models;
using ZenBotCS.Helper;

namespace ZenBotCS.Tests;

public class CwlSignupCacheTests
{
    private static CwlSignupCache CreateCache() => new(new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public void Get_ReturnsSignup_AfterSet()
    {
        var cache = CreateCache();
        var signup = new CwlSignup { DiscordId = 1, PlayerTag = "#ABC", PlayerName = "Bob" };

        cache.Set(123UL, signup);

        Assert.Same(signup, cache.Get(123UL));
    }

    [Fact]
    public void Get_ReturnsNull_WhenMissing()
    {
        var cache = CreateCache();

        Assert.Null(cache.Get(999UL));
    }

    [Fact]
    public void TryUpdate_ReturnsFalse_WhenMissing()
    {
        var cache = CreateCache();

        var updated = cache.TryUpdate(999UL, s => s.ClanTag = "#X");

        Assert.False(updated);
    }

    [Fact]
    public void TryUpdate_AppliesMutationAndPersists_WhenPresent()
    {
        var cache = CreateCache();
        cache.Set(1UL, new CwlSignup { DiscordId = 1, PlayerTag = "#ABC" });

        var updated = cache.TryUpdate(1UL, s => s.ClanTag = "#CLAN");

        Assert.True(updated);
        Assert.Equal("#CLAN", cache.Get(1UL)!.ClanTag);
    }

    [Fact]
    public void Set_Overwrites_ExistingEntry()
    {
        var cache = CreateCache();
        var first = new CwlSignup { DiscordId = 1, PlayerTag = "#A" };
        var second = new CwlSignup { DiscordId = 2, PlayerTag = "#B" };

        cache.Set(1UL, first);
        cache.Set(1UL, second);

        Assert.Same(second, cache.Get(1UL));
    }
}
