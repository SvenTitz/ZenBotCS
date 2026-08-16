using CocApi.Rest.Models;
using ZenBotCS.Services.Background;

namespace ZenBotCS.Tests;

public class SpinReminderDecisionTests
{
    // Sunday 2026-08-16, the 19:00 spin.
    private static readonly DateTime Spin = new(2026, 8, 16, 19, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Due = Spin.AddMinutes(30);

    private static SpinReminderDecision Decide(DateTime nowUtc, WarState state, DateTime? warEndUtc = null)
        => WarSpinReminderService.Decide(nowUtc, Spin, state, warEndUtc);

    [Fact]
    public void BeforeTheCheckDelay_Waits()
    {
        Assert.Equal(SpinReminderDecision.Wait, Decide(Spin.AddMinutes(29), WarState.WarEnded, Spin.AddHours(-21)));
    }

    [Fact]
    public void PreviousWarStillInTheSlot_Pings()
    {
        // The war before this spin ended the previous evening and nobody has hit search since.
        Assert.Equal(SpinReminderDecision.Ping, Decide(Due, WarState.WarEnded, Spin.AddHours(-21)));
    }

    [Fact]
    public void Matched_IsSilent()
    {
        Assert.Equal(SpinReminderDecision.MarkSilent, Decide(Due, WarState.Preparation, Spin.AddDays(1)));
    }

    [Fact]
    public void NotInWar_IsSilent()
    {
        // Could be idle, could be mid-search — indistinguishable, so never ping.
        Assert.Equal(SpinReminderDecision.MarkSilent, Decide(Due, WarState.NotInWar));
    }

    [Fact]
    public void OverrunningWar_WaitsForItToEnd()
    {
        var warEnd = Spin.AddHours(2);

        Assert.Equal(SpinReminderDecision.Wait, Decide(Due, WarState.InWar, warEnd));
    }

    [Fact]
    public void OverrunningWar_PingsHalfAnHourAfterItEnds()
    {
        var warEnd = Spin.AddHours(2);

        Assert.Equal(SpinReminderDecision.Wait, Decide(warEnd.AddMinutes(29), WarState.WarEnded, warEnd));
        Assert.Equal(SpinReminderDecision.Ping, Decide(warEnd.AddMinutes(30), WarState.WarEnded, warEnd));
    }

    [Fact]
    public void WarEndingBeyondTheDeferralCap_IsSilent()
    {
        // Ends 6.5h after the due moment: the rotation has drifted too far to be worth a reminder.
        var warEnd = Due.AddHours(6).AddMinutes(1);

        Assert.Equal(SpinReminderDecision.MarkSilent, Decide(Due, WarState.InWar, warEnd));
        Assert.Equal(SpinReminderDecision.MarkSilent, Decide(warEnd.AddHours(1), WarState.WarEnded, warEnd));
    }

    [Fact]
    public void WarEndingExactlyAtTheDeferralCap_StillWaits()
    {
        var warEnd = Due.AddHours(6).AddMinutes(-30);

        Assert.Equal(SpinReminderDecision.Wait, Decide(Due, WarState.InWar, warEnd));
    }

    [Fact]
    public void WithinTheGracePeriod_StillPings()
    {
        Assert.Equal(SpinReminderDecision.Ping, Decide(Due.AddHours(2), WarState.WarEnded, Spin.AddHours(-21)));
    }

    [Fact]
    public void AfterTheGracePeriod_IsSilent()
    {
        // The bot was down over the due moment; a ping this late is just noise.
        Assert.Equal(SpinReminderDecision.MarkSilent, Decide(Due.AddHours(2).AddMinutes(1), WarState.WarEnded, Spin.AddHours(-21)));
    }

    [Fact]
    public void GracePeriodRunsFromTheDeferredDue_NotTheScheduledOne()
    {
        // War overran to 3h past the spin, so the ping window is 21:30-23:30 rather than 19:30-21:30.
        var warEnd = Spin.AddHours(3);

        Assert.Equal(SpinReminderDecision.Ping, Decide(Spin.AddHours(5), WarState.WarEnded, warEnd));
    }

    [Fact]
    public void InWarWithAnAlreadyPassedEndTime_IsSilent()
    {
        // Stale/contradictory data: the clan is in a war either way, so it didn't need to spin.
        Assert.Equal(SpinReminderDecision.MarkSilent, Decide(Due, WarState.InWar, Spin.AddHours(-1)));
    }

    [Theory]
    [InlineData(-61, false)]  // more than an hour out
    [InlineData(-60, true)]   // exactly the lead time
    [InlineData(-1, true)]
    [InlineData(0, false)]    // the spin has started; too late to be useful
    [InlineData(30, false)]
    public void MandoWarningWindow(int minutesFromSpin, bool expected)
    {
        Assert.Equal(expected, WarSpinReminderService.ShouldPostMandoWarning(Spin.AddMinutes(minutesFromSpin), Spin));
    }
}
