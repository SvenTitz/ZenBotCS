using ZenBotCS.Entities.Models.ClashKingApi;
using ZenBotCS.Entities.Models.Cwl;

namespace ZenBotCS.Tests;

public class CwlInstanceGroupingTests
{
    private static WarData CwlWar(int year, int month, int day, string tag = "#WAR") =>
        new() { WarTag = tag, StartTime = $"{year:D4}{month:D2}{day:D2}T195213.000Z" };

    private static WarData RegularWar(int year, int month, int day) =>
        new() { StartTime = $"{year:D4}{month:D2}{day:D2}T195213.000Z" };

    private static DateTime StartOf(List<WarData> instance) => CwlPerformanceCalculator.GroupStart(instance);

    [Fact]
    public void GroupIntoCwlInstances_KeepsOneMonthsSevenRoundsTogether()
    {
        var wars = Enumerable.Range(2, 7).Select(d => CwlWar(2026, 9, d)).ToList();

        var instances = CwlPerformanceCalculator.GroupIntoCwlInstances(wars);

        var instance = Assert.Single(instances);
        Assert.Equal(7, instance.Count);
    }

    [Fact]
    public void GroupIntoCwlInstances_DoesNotSplitWhenRoundsAreMissing()
    {
        // The rounds a partial fetch leaves behind: first two and last, six days apart.
        var wars = new List<WarData> { CwlWar(2026, 9, 2), CwlWar(2026, 9, 3), CwlWar(2026, 9, 8) };

        var instances = CwlPerformanceCalculator.GroupIntoCwlInstances(wars);

        var instance = Assert.Single(instances);
        Assert.Equal(3, instance.Count);
        Assert.Equal(new DateTime(2026, 9, 2, 19, 52, 13, DateTimeKind.Utc), StartOf(instance));
    }

    [Fact]
    public void GroupIntoCwlInstances_SeparatesTwoCwlsInTheSameMonth()
    {
        var wars = Enumerable.Range(2, 7).Select(d => CwlWar(2026, 6, d))
            .Concat(Enumerable.Range(17, 7).Select(d => CwlWar(2026, 6, d)))
            .ToList();

        var instances = CwlPerformanceCalculator.GroupIntoCwlInstances(wars);

        Assert.Equal(2, instances.Count);
        Assert.Equal(17, StartOf(instances[0]).Day); // newest first
        Assert.Equal(2, StartOf(instances[1]).Day);
        Assert.All(instances, i => Assert.Equal(7, i.Count));
    }

    [Fact]
    public void GroupIntoCwlInstances_SeparatesConsecutiveMonths()
    {
        var wars = Enumerable.Range(2, 7).Select(d => CwlWar(2026, 8, d))
            .Concat(Enumerable.Range(2, 7).Select(d => CwlWar(2026, 9, d)))
            .ToList();

        var instances = CwlPerformanceCalculator.GroupIntoCwlInstances(wars);

        Assert.Equal(2, instances.Count);
        Assert.Equal(9, StartOf(instances[0]).Month);
        Assert.Equal(8, StartOf(instances[1]).Month);
    }

    [Fact]
    public void GroupIntoCwlInstances_IgnoresRegularWars()
    {
        var wars = new List<WarData> { CwlWar(2026, 9, 2), RegularWar(2026, 9, 3), RegularWar(2026, 9, 12) };

        var instances = CwlPerformanceCalculator.GroupIntoCwlInstances(wars);

        Assert.Single(instances);
        Assert.Single(instances[0]);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(13, false)]
    [InlineData(14, true)]
    [InlineData(22, true)]
    public void InstanceKey_SplitsTheMonthInHalves(int day, bool secondOfMonth)
    {
        var key = CwlPerformanceCalculator.InstanceKey(new DateTime(2026, 6, day, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new CwlPerformanceCalculator.CwlInstanceKey(2026, 6, secondOfMonth), key);
    }

    [Theory]
    [InlineData(9, 1)]
    [InlineData(13, 1)]
    [InlineData(14, 14)]
    [InlineData(30, 14)]
    public void InstanceSlotStart_IsTheStartOfTheSlotTheMomentFallsIn(int day, int expectedDay)
    {
        var start = CwlPerformanceCalculator.InstanceSlotStart(new DateTime(2026, 9, day, 6, 30, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2026, 9, expectedDay, 0, 0, 0, DateTimeKind.Utc), start);
    }

    [Fact]
    public void RoundsWithData_CountsOnlyRoundsAnyoneWasRosteredFor()
    {
        var performance = new CwlSeasonPerformance
        {
            Players =
            [
                new CwlPlayerPerformance { PlayerTag = "#A", Days = Cells(0, 1) },
                new CwlPlayerPerformance { PlayerTag = "#B", Days = Cells(1, 4) },
            ],
        };

        Assert.Equal(3, CwlPerformanceCalculator.RoundsWithData(performance));
        Assert.Equal(0, CwlPerformanceCalculator.RoundsWithData(new CwlSeasonPerformance()));
        Assert.Equal(0, CwlPerformanceCalculator.RoundsWithData(null));
    }

    private static CwlAttackCell?[] Cells(params int[] rounds)
    {
        var days = new CwlAttackCell?[7];
        foreach (var round in rounds)
            days[round] = new CwlAttackCell { Stars = 3 };
        return days;
    }
}
