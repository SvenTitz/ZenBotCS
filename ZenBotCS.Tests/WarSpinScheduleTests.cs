using ZenBotCS.Helper;

namespace ZenBotCS.Tests;

public class WarSpinScheduleTests
{
    private static DateTime Utc(int year, int month, int day, int hour = 0, int minute = 0)
        => new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    [Fact]
    public void Upcoming_ReturnsTheThreeWeeklySlotsInOrder()
    {
        // Friday 2026-08-14, so the next three are Sun / Tue / Thu.
        var spins = WarSpinSchedule.Upcoming(Utc(2026, 8, 14, 12, 0)).Take(3).ToList();

        Assert.Equal(Utc(2026, 8, 16, 19, 0), spins[0].StartUtc);
        Assert.Equal(Utc(2026, 8, 18, 21, 0), spins[1].StartUtc);
        Assert.Equal(Utc(2026, 8, 20, 23, 0), spins[2].StartUtc);
    }

    [Fact]
    public void Upcoming_SpinTimesAreUtc()
    {
        var spin = WarSpinSchedule.Upcoming(Utc(2026, 8, 14, 12, 0)).First();

        Assert.Equal(DateTimeKind.Utc, spin.StartUtc.Kind);
    }

    [Fact]
    public void Upcoming_ExcludesASlotThatHasJustPassed()
    {
        // Sunday 2026-08-16 at 19:30 — that day's 19:00 spin is gone.
        var next = WarSpinSchedule.Upcoming(Utc(2026, 8, 16, 19, 30)).First();

        Assert.Equal(Utc(2026, 8, 18, 21, 0), next.StartUtc);
    }

    [Fact]
    public void Upcoming_SkipsTheCwlWindowAtTheStartOfTheMonth()
    {
        // 2026-09-01 is a Tuesday; days 1-9 are CWL, so the first spin is the
        // Thursday on the 10th.
        var next = WarSpinSchedule.Upcoming(Utc(2026, 9, 1, 0, 0)).First();

        Assert.Equal(Utc(2026, 9, 10, 23, 0), next.StartUtc);
    }

    [Fact]
    public void Upcoming_RollsOverIntoTheNextMonth()
    {
        // Thursday 2026-08-27 at 23:30, past that day's spin. The Sunday (30th)
        // and Tuesday (Sep 1st) slots fall in the next month's CWL window, so the
        // next spin is Sep 10th.
        var spins = WarSpinSchedule.Upcoming(Utc(2026, 8, 27, 23, 30)).Take(2).ToList();

        Assert.Equal(Utc(2026, 8, 30, 19, 0), spins[0].StartUtc);
        Assert.Equal(Utc(2026, 9, 10, 23, 0), spins[1].StartUtc);
    }

    [Theory]
    [InlineData(2026, 9, 10)]  // first window, 10-16
    [InlineData(2026, 9, 24)]  // second window, 24-30
    public void IsMandatory_TrueForThursdaysInBothWindows(int year, int month, int day)
    {
        Assert.True(WarSpinSchedule.IsMandatory(Utc(year, month, day, 23, 0)));
    }

    [Theory]
    [InlineData(2026, 9, 17)]  // Thursday, but between the windows
    [InlineData(2026, 10, 1)]  // Thursday, but inside the CWL window
    [InlineData(2026, 9, 13)]  // Sunday inside a mando window
    public void IsMandatory_FalseOtherwise(int year, int month, int day)
    {
        Assert.False(WarSpinSchedule.IsMandatory(Utc(year, month, day, 23, 0)));
    }

    [Fact]
    public void Upcoming_MandoIsAnchoredOnTheSpinTimeNotTheDate()
    {
        // Thursday 2026-09-10 at 23:30: that day's mando spin has started, so the
        // next one is the 24th rather than today.
        var mando = WarSpinSchedule.Upcoming(Utc(2026, 9, 10, 23, 30)).First(s => s.IsMandatory);

        Assert.Equal(Utc(2026, 9, 24, 23, 0), mando.StartUtc);
    }

    [Fact]
    public void Upcoming_AlwaysFindsTwoMandosAhead()
    {
        var mandos = WarSpinSchedule.Upcoming(Utc(2026, 8, 27, 23, 30)).Where(s => s.IsMandatory).Take(2).ToList();

        Assert.Equal(2, mandos.Count);
    }

    [Fact]
    public void MostRecent_ReturnsTheSlotThatHasJustPassed()
    {
        var spin = WarSpinSchedule.MostRecent(Utc(2026, 8, 16, 19, 30), TimeSpan.FromHours(12));

        Assert.NotNull(spin);
        Assert.Equal(Utc(2026, 8, 16, 19, 0), spin.Value.StartUtc);
    }

    [Fact]
    public void MostRecent_IgnoresASlotOlderThanTheLookback()
    {
        // Monday 09:00 is 14 hours after Sunday's 19:00 spin.
        var spin = WarSpinSchedule.MostRecent(Utc(2026, 8, 17, 9, 0), TimeSpan.FromHours(12));

        Assert.Null(spin);
    }

    [Fact]
    public void MostRecent_FindsASlotOnAPreviousDayWithinTheLookback()
    {
        var spin = WarSpinSchedule.MostRecent(Utc(2026, 8, 17, 6, 0), TimeSpan.FromHours(12));

        Assert.NotNull(spin);
        Assert.Equal(Utc(2026, 8, 16, 19, 0), spin.Value.StartUtc);
    }

    [Fact]
    public void MostRecent_ReturnsNullInsideTheCwlWindow()
    {
        var spin = WarSpinSchedule.MostRecent(Utc(2026, 9, 6, 20, 0), TimeSpan.FromHours(12));

        Assert.Null(spin);
    }
}
