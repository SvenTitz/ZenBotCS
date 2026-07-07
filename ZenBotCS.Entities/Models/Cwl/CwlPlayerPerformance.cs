using Newtonsoft.Json;

namespace ZenBotCS.Entities.Models.Cwl;

/// <summary>
/// A player's full CWL performance for one season: the seven day cells plus the sheet's
/// aggregate columns. Aggregates are stored (not just computed) so a snapshot preserves exactly
/// what was captured, but they are also re-derivable from <see cref="Days"/> via
/// <c>CwlPerformanceCalculator</c>.
/// </summary>
public class CwlPlayerPerformance
{
    public string PlayerTag { get; set; } = string.Empty;

    public string PlayerName { get; set; } = string.Empty;

    /// <summary>The player's own town hall level (the sheet's "TH" column, used for reach).</summary>
    public int TownHallLevel { get; set; }

    public bool Bonus { get; set; }

    /// <summary>One entry per CWL round (index 0..6); null where the player wasn't in that war.</summary>
    public CwlAttackCell?[] Days { get; set; } = new CwlAttackCell?[7];

    /// <summary>Attacks used (days with a non-missed result).</summary>
    public int Hits { get; set; }

    /// <summary>Σ(effective defender TH over hits) − ownTH × Hits.</summary>
    public int ReachPlusMinus { get; set; }

    public double AverageStars { get; set; }

    public double AverageDestruction { get; set; }

    /// <summary>AverageStars + ReachPlusMinus ÷ Hits ÷ 3.</summary>
    public double Score { get; set; }
}
