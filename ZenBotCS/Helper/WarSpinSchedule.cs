namespace ZenBotCS.Helper;

/// <summary>A scheduled war spin. <paramref name="StartUtc"/> is always <see cref="DateTimeKind.Utc"/>.</summary>
public readonly record struct WarSpin(DateTime StartUtc, bool IsMandatory);

/// <summary>
/// The family-wide war spin schedule: three fixed slots a week, in UTC. Pure and static so both
/// <c>/spintimes</c> and the spin reminder read the same calendar.
/// </summary>
public static class WarSpinSchedule
{
    private static readonly (DayOfWeek Day, int Hour, int Minute)[] Slots =
    [
        (DayOfWeek.Sunday, 19, 0),
        (DayOfWeek.Tuesday, 21, 0),
        (DayOfWeek.Thursday, 23, 0)
    ];

    // CWL occupies the start of the month, so no regular wars are spun on those days.
    private const int DaysSkippedAtStartOfMonth = 9;

    // Mandatory wars are the Thursday spins in these day-of-month ranges.
    private static readonly (int MinDay, int MaxDay)[] MandoDayRanges = [(10, 16), (24, 30)];

    // Two mando windows per month, so this always covers the next two of them.
    private const int LookaheadDays = 90;

    /// <summary>Every spin after <paramref name="nowUtc"/>, in chronological order.</summary>
    public static IEnumerable<WarSpin> Upcoming(DateTime nowUtc)
    {
        for (int i = 0; i <= LookaheadDays; i++)
        {
            var spin = SpinOn(nowUtc.Date.AddDays(i));
            if (spin is not null && spin.Value.StartUtc > nowUtc)
                yield return spin.Value;
        }
    }

    /// <summary>
    /// The most recent spin at or before <paramref name="nowUtc"/>, or null if none falls within
    /// <paramref name="lookback"/>.
    /// </summary>
    public static WarSpin? MostRecent(DateTime nowUtc, TimeSpan lookback)
    {
        var earliest = nowUtc - lookback;

        // At most one spin per day, so walking backwards the first hit is the latest one.
        for (var date = nowUtc.Date; date >= earliest.Date; date = date.AddDays(-1))
        {
            var spin = SpinOn(date);
            if (spin is not null && spin.Value.StartUtc <= nowUtc && spin.Value.StartUtc >= earliest)
                return spin.Value;
        }

        return null;
    }

    /// <summary>True when a spin at this time is a mandatory war.</summary>
    public static bool IsMandatory(DateTime spinUtc)
    {
        return spinUtc.DayOfWeek == DayOfWeek.Thursday
            && MandoDayRanges.Any(r => spinUtc.Day >= r.MinDay && spinUtc.Day <= r.MaxDay);
    }

    private static WarSpin? SpinOn(DateTime date)
    {
        if (date.Day <= DaysSkippedAtStartOfMonth)
            return null;

        foreach (var slot in Slots)
        {
            if (slot.Day != date.DayOfWeek)
                continue;

            var startUtc = new DateTime(date.Year, date.Month, date.Day, slot.Hour, slot.Minute, 0, DateTimeKind.Utc);
            return new WarSpin(startUtc, IsMandatory(startUtc));
        }

        return null;
    }
}
