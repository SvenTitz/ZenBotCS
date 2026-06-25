using ZenBotCS.Services.SlashCommands;

namespace ZenBotCS.Tests;

public class DayRosterDiffTests
{
    [Fact]
    public void OptedInButNotInLineup_IsOptIn()
    {
        var sheet = new[] { ("#A", "Alice", true), ("#B", "Bob", false) };
        var lineup = Array.Empty<(string, string)>();

        var (toOptIn, toOptOut) = CwlRosterService.ComputeDayRosterDiff(sheet, lineup);

        Assert.Equal(new[] { ("#A", "Alice") }, toOptIn);
        Assert.Empty(toOptOut);
    }

    [Fact]
    public void InLineupButNotOptedIn_IsOptOut()
    {
        var sheet = new[] { ("#A", "Alice", false) };
        var lineup = new[] { ("#A", "Alice") };

        var (toOptIn, toOptOut) = CwlRosterService.ComputeDayRosterDiff(sheet, lineup);

        Assert.Empty(toOptIn);
        Assert.Equal(new[] { ("#A", "Alice") }, toOptOut);
    }

    [Fact]
    public void InLineupButAbsentFromSheet_IsOptOut()
    {
        var sheet = Array.Empty<(string, string, bool)>();
        var lineup = new[] { ("#X", "Xander") };

        var (toOptIn, toOptOut) = CwlRosterService.ComputeDayRosterDiff(sheet, lineup);

        Assert.Empty(toOptIn);
        Assert.Equal(new[] { ("#X", "Xander") }, toOptOut);
    }

    [Fact]
    public void OptedInAndAlreadyInLineup_IsNeither()
    {
        var sheet = new[] { ("#A", "Alice", true) };
        var lineup = new[] { ("#A", "Alice") };

        var (toOptIn, toOptOut) = CwlRosterService.ComputeDayRosterDiff(sheet, lineup);

        Assert.Empty(toOptIn);
        Assert.Empty(toOptOut);
    }
}
