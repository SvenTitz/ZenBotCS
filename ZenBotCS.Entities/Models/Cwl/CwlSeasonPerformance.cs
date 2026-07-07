namespace ZenBotCS.Entities.Models.Cwl;

/// <summary>
/// The computed CWL performance for one clan in one CWL instance — the whole sheet tab, minus
/// presentation. This is what gets serialised into <see cref="CwlHistory.Performance"/> and what
/// both the web table and the summary image render from. Leaderboards are derived at render time
/// by sorting <see cref="Players"/>; only the per-day totals are kept here.
/// </summary>
public class CwlSeasonPerformance
{
    public string ClanTag { get; set; } = string.Empty;

    public string ClanName { get; set; } = string.Empty;

    /// <summary>The CWL season in <c>yyyy-MM</c> form.</summary>
    public string Season { get; set; } = string.Empty;

    /// <summary>The first war's start time — disambiguates two CWLs in the same month.</summary>
    public DateTime StartTime { get; set; }

    public List<CwlPlayerPerformance> Players { get; set; } = [];

    /// <summary>Total stars scored by the clan on each of the seven days.</summary>
    public int[] DailyTotalStars { get; set; } = new int[7];

    /// <summary>Total destruction percentage scored by the clan on each of the seven days.</summary>
    public int[] DailyTotalDestruction { get; set; } = new int[7];
}
