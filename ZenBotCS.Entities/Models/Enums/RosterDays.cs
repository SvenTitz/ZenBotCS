namespace ZenBotCS.Entities.Models.Enums;

/// <summary>
/// Which CWL days a player is in the roster lineup. Mirrors <see cref="OptOutDays"/>'s bit layout
/// (Day1=1 … Day7=64) on purpose, so the two can be converted with a bitmask — see
/// <c>CwlSignup.EffectiveRosterDays</c>. A set flag means "playing that day".
/// </summary>
[Flags]
public enum RosterDays
{
    None = 0,
    Day1 = 1,
    Day2 = 2,
    Day3 = 4,
    Day4 = 8,
    Day5 = 16,
    Day6 = 32,
    Day7 = 64,
}
