using Newtonsoft.Json;

namespace ZenBotCS.Entities.Models.Cwl;

/// <summary>
/// One player's result on a single CWL war day — the sheet's ⭐ / % / TH triple. A day the
/// player was rostered but didn't attack is a <see cref="IsMissed"/> cell (0⭐/0%); a day they
/// weren't in the war at all is represented by a null cell in <see cref="CwlPlayerPerformance.Days"/>.
/// </summary>
public class CwlAttackCell
{
    public int Stars { get; set; }

    public int DestructionPercentage { get; set; }

    /// <summary>The defender's actual town hall level.</summary>
    public int DefenderTownHall { get; set; }

    /// <summary>
    /// How much lower the defender's map slot "should" have been than their real TH — i.e. how
    /// rushed they are. Ported from <c>CwlDataMemberAttack.GetTownhallLevelDifferenceFromMap</c>.
    /// </summary>
    public int DefenderRushScore { get; set; }

    public bool IsMissed { get; set; }

    /// <summary>The rush-adjusted TH the sheet's "reach" is computed against.</summary>
    [JsonIgnore]
    public int EffectiveDefenderTownHall => DefenderTownHall - DefenderRushScore;

    /// <summary>Whether the defender was rushed (drives the web badge + the image marker).</summary>
    [JsonIgnore]
    public bool IsRushedDefender => DefenderRushScore > 0;
}
