using ZenBotCS.Entities.Models;
using ZenBotCS.Services.Background;

namespace ZenBotCS.Tests;

public class ReminderTargetTests
{
    private const string Zen = "#ZEN";
    private const string WarLovers = "#WARLOVERS";
    private const string Pizza = "#PIZZA";

    private static SubRoster Sub(string clanTag, string gameClanTag, string name, int order = 0) =>
        new() { ClanTag = clanTag, GameClanTag = gameClanTag, Name = name, Order = order };

    [Fact]
    public void ExpandTargets_ReturnsTheClanItself_WhenItHasNoSubRosters()
    {
        var targets = CwlRosterReminderService.ExpandTargets(Zen, []);

        var target = Assert.Single(targets);
        Assert.Equal(Zen, target.GameClanTag);
        Assert.Null(target.RosterName); // the main roster isn't named in the embed title
    }

    [Fact]
    public void ExpandTargets_AddsEachSubRostersHostClan()
    {
        var subRosters = new[] { Sub(Zen, WarLovers, "B Roster") };

        var targets = CwlRosterReminderService.ExpandTargets(Zen, subRosters);

        Assert.Equal([Zen, WarLovers], targets.Select(t => t.GameClanTag));
        Assert.Equal([null, "B Roster"], targets.Select(t => t.RosterName));
    }

    [Fact]
    public void ExpandTargets_KeepsTheMainRosterFirst_ThenOrdersSubRosters()
    {
        var subRosters = new[]
        {
            Sub(Zen, Pizza, "C Roster", order: 2),
            Sub(Zen, WarLovers, "B Roster", order: 1),
        };

        var targets = CwlRosterReminderService.ExpandTargets(Zen, subRosters);

        Assert.Equal([Zen, WarLovers, Pizza], targets.Select(t => t.GameClanTag));
    }

    [Fact]
    public void ExpandTargets_IgnoresSubRostersOwnedByOtherClans()
    {
        var subRosters = new[] { Sub("#OTHER", WarLovers, "Their B Roster") };

        var targets = CwlRosterReminderService.ExpandTargets(Zen, subRosters);

        Assert.Equal([Zen], targets.Select(t => t.GameClanTag));
    }

    // Each target must be a distinct clan tag: the reminder dedup keys on it, so a duplicate would
    // make two rosters suppress each other and only one reminder would ever post.
    [Fact]
    public void ExpandTargets_YieldsDistinctGameClanTags()
    {
        var subRosters = new[]
        {
            Sub(Zen, WarLovers, "B Roster", order: 1),
            Sub(Zen, Pizza, "C Roster", order: 2),
        };

        var targets = CwlRosterReminderService.ExpandTargets(Zen, subRosters);

        Assert.Equal(targets.Count, targets.Select(t => t.GameClanTag).Distinct().Count());
    }
}
