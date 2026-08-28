using ZenBotCS.Entities;
using ZenBotCS.Entities.Models;
using ZenBotCS.Entities.Models.Enums;

namespace ZenBotCS.Tests;

public class SubRosterRulesTests
{
    private const string Zen = "#ZEN";
    private const string WarLovers = "#WARLOVERS";

    private static SubRoster Sub(string clanTag, string gameClanTag, string name) =>
        new() { ClanTag = clanTag, GameClanTag = gameClanTag, Name = name };

    [Fact]
    public void ValidateNew_Succeeds_ForAFreeEventClan()
    {
        var result = SubRosterRules.ValidateNew(Zen, WarLovers, "B Roster", ClanType.Event, []);

        Assert.True(result.Ok);
        Assert.Null(result.Error);
    }

    [Fact]
    public void ValidateNew_AllowsPartnerClansToo()
    {
        var result = SubRosterRules.ValidateNew(Zen, WarLovers, "B Roster", ClanType.Partner, []);

        Assert.True(result.Ok);
    }

    [Theory]
    [InlineData(ClanType.War)]
    [InlineData(ClanType.FWA)]
    [InlineData(ClanType.Other)]
    public void ValidateNew_RejectsClansThatRunTheirOwnCwl(ClanType type)
    {
        var result = SubRosterRules.ValidateNew(Zen, WarLovers, "B Roster", type, []);

        Assert.False(result.Ok);
        Assert.Contains("event and partner", result.Error);
    }

    [Fact]
    public void ValidateNew_RejectsAnUnmanagedClan()
    {
        var result = SubRosterRules.ValidateNew(Zen, WarLovers, "B Roster", hostClanType: null, []);

        Assert.False(result.Ok);
    }

    [Fact]
    public void ValidateNew_RejectsTheOwnerClanItself()
    {
        var result = SubRosterRules.ValidateNew(Zen, Zen, "B Roster", ClanType.Event, []);

        Assert.False(result.Ok);
        Assert.Contains("main roster", result.Error);
    }

    // The unique index on GameClanTag enforces this in the DB; catching it here is what turns it into
    // a readable message instead of a raw constraint violation.
    [Fact]
    public void ValidateNew_RejectsAClanThatAlreadyHostsARoster()
    {
        var existing = new[] { Sub("#OTHER", WarLovers, "Their B Roster") };

        var result = SubRosterRules.ValidateNew(Zen, WarLovers, "B Roster", ClanType.Event, existing);

        Assert.False(result.Ok);
        Assert.Contains("Their B Roster", result.Error);
    }

    [Fact]
    public void ValidateNew_MatchesHostClanTagsCaseInsensitively()
    {
        var existing = new[] { Sub(Zen, WarLovers, "B Roster") };

        var result = SubRosterRules.ValidateNew(Zen, "#warlovers", "C Roster", ClanType.Event, existing);

        Assert.False(result.Ok);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateNew_RequiresAName(string name)
    {
        var result = SubRosterRules.ValidateNew(Zen, WarLovers, name, ClanType.Event, []);

        Assert.False(result.Ok);
        Assert.Contains("name", result.Error);
    }

    [Fact]
    public void ValidateNew_RequiresAHostClan()
    {
        var result = SubRosterRules.ValidateNew(Zen, "", "B Roster", ClanType.Event, []);

        Assert.False(result.Ok);
    }

    [Theory]
    [InlineData(ClanType.Event, true)]
    [InlineData(ClanType.Partner, true)]
    [InlineData(ClanType.War, false)]
    [InlineData(ClanType.FWA, false)]
    [InlineData(ClanType.Other, false)]
    public void CanHost_AllowsOnlyEventAndPartnerClans(ClanType type, bool expected)
        => Assert.Equal(expected, SubRosterRules.CanHost(type));
}
