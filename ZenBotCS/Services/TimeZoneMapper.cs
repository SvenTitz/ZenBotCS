using ZenBotCS.Models.Enums;

namespace ZenBotCS.Services;

public static class TimeZoneMapper
{
    private static readonly Dictionary<TimeZoneEnum, (string WindowsId, string IanaId)> TimeZoneMap = new()
{
    { TimeZoneEnum.UTC,  ("UTC", "Etc/UTC") },
    { TimeZoneEnum.CET,  ("Central European Standard Time", "Europe/Berlin") },
    { TimeZoneEnum.EST,  ("Eastern Standard Time", "America/New_York") },
    { TimeZoneEnum.PST,  ("Pacific Standard Time", "America/Los_Angeles") },
    { TimeZoneEnum.MST,  ("Mountain Standard Time", "America/Denver") },
    { TimeZoneEnum.CST,  ("Central Standard Time", "America/Chicago") },
    { TimeZoneEnum.IST,  ("India Standard Time", "Asia/Kolkata") },
    { TimeZoneEnum.JST,  ("Tokyo Standard Time", "Asia/Tokyo") },
    { TimeZoneEnum.AEST, ("AUS Eastern Standard Time", "Australia/Sydney") },
    { TimeZoneEnum.GMT,  ("GMT Standard Time", "Europe/London") },
};

    public static TimeZoneInfo GetTimeZoneInfo(TimeZoneEnum zone)
    {
        var (windowsId, ianaId) = TimeZoneMap[zone];

        // Detect platform
        bool isWindows = OperatingSystem.IsWindows();

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(isWindows ? windowsId : ianaId);
        }
        catch (TimeZoneNotFoundException)
        {
            throw new ArgumentException($"Time zone not found: {(isWindows ? windowsId : ianaId)}");
        }
    }
}
