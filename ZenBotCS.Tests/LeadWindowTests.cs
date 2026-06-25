using ZenBotCS.Services.Background;

namespace ZenBotCS.Tests;

public class LeadWindowTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void WithinWindow_IsTrue()
    {
        // war starts in 3 hours, lead window is 4 hours
        Assert.True(CwlRosterReminderService.IsWithinLeadWindow(Now, Now.AddHours(3), 4));
    }

    [Fact]
    public void BeforeWindowOpens_IsFalse()
    {
        // war starts in 6 hours, lead window is 4 hours
        Assert.False(CwlRosterReminderService.IsWithinLeadWindow(Now, Now.AddHours(6), 4));
    }

    [Fact]
    public void AfterStart_IsFalse()
    {
        // war already started an hour ago
        Assert.False(CwlRosterReminderService.IsWithinLeadWindow(Now, Now.AddHours(-1), 4));
    }

    [Fact]
    public void ExactlyAtStart_IsFalse()
    {
        Assert.False(CwlRosterReminderService.IsWithinLeadWindow(Now, Now, 4));
    }

    [Fact]
    public void ExactlyAtLeadBoundary_IsTrue()
    {
        Assert.True(CwlRosterReminderService.IsWithinLeadWindow(Now, Now.AddHours(4), 4));
    }
}
