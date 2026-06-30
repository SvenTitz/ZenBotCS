using System.Numerics;
using ZenBotCS.Entities.Models;
using ZenBotCS.Entities.Models.Enums;

namespace ZenBotCS.Web.Services;

/// <summary>
/// Auto-solves a CWL roster: resets every player to their default availability, then trims
/// over-subscribed days down to the target war size by benching players in a fixed priority order.
///
/// The passes escalate in pain — first nudge Alternates down a day, then 8-star players, then Always —
/// and within each pass the lowest town hall is benched first. A player is only benched on a day while
/// their rostered-day count is still ABOVE that pass's floor, so e.g. the first pass trims Alternates
/// down to (but never below) 5 days. FWA players are never touched.
/// </summary>
public static class RosterSolver
{
    private const int AllDaysMask = 0b111_1111; // Day1..Day7

    private static readonly RosterDays[] Days =
    [
        RosterDays.Day1, RosterDays.Day2, RosterDays.Day3, RosterDays.Day4,
        RosterDays.Day5, RosterDays.Day6, RosterDays.Day7,
    ];

    // Removal passes in priority order: (does this preference match?, the day-count floor we won't go below).
    // Each pass is applied across ALL over-capacity days before the next pass runs (see Solve), so the
    // lowest-town-hall-first rule holds within a pass. Mirrors the hand-written priority list:
    // alternate→5, 8star→5, alternate→4, 8star→4, always→5, alternate→3, always→4, alternate→0,
    // then 8star+always→0 as the last resort.
    private static readonly (Func<WarPreference, bool> Match, int Floor)[] Passes =
    [
        (p => p == WarPreference.Alternate, 6),
        (p => p == WarPreference.Alternate, 5),
        (p => p == WarPreference.EightStars, 6),
        (p => p == WarPreference.EightStars, 5),
        (p => p == WarPreference.Alternate, 4),
        (p => p == WarPreference.EightStars, 4),
        (p => p == WarPreference.Always, 6),
        (p => p == WarPreference.Always, 5),
        (p => p == WarPreference.Alternate, 3),
        (p => p == WarPreference.Always, 4),
        (p => p == WarPreference.Alternate, 2),
        (p => p == WarPreference.Alternate, 1),
        (p => p == WarPreference.Alternate, 0),
        (p => p is WarPreference.EightStars or WarPreference.Always, 3),
        (p => p is WarPreference.EightStars or WarPreference.Always, 2),
        (p => p is WarPreference.EightStars or WarPreference.Always, 1),
        (p => p is WarPreference.EightStars or WarPreference.Always, 0),
    ];

    /// <summary>
    /// Solves the lineup in place: each signup's <see cref="CwlSignup.RosterDays"/> is set to the
    /// resulting lineup (non-null for every player afterwards). A day may stay over target if no
    /// eligible player can be benched (e.g. it's all protected players) — that's best-effort.
    /// </summary>
    public static void Solve(IReadOnlyList<CwlSignup> signups, int target)
    {
        // 1. Reset to default: play every day you didn't opt out of (opted-out days show as "red").
        foreach (var s in signups)
            s.RosterDays = (RosterDays)(~(int)s.OptOutDays & AllDaysMask);

        // 2. Apply each pass across ALL over-capacity days before escalating to the next pass. This
        //    keeps "lowest town hall first" a per-pass rule: every day gets pass 1 before any day sees
        //    pass 2, so a low-TH Always is never benched ahead of a higher-TH Alternate.
        foreach (var (match, floor) in Passes)
            foreach (var day in Days)
                while (Count(signups, day) > target)
                {
                    var victim = signups
                        .Where(s => s.RosterDays!.Value.HasFlag(day)
                                 && match(s.WarPreference)
                                 && DayCount(s) > floor)
                        .OrderBy(s => s.PlayerThLevel)              // lowest town hall first
                        .ThenByDescending(DayCount)                 // then whoever is rostered most days
                        .ThenBy(s => s.PlayerName, StringComparer.Ordinal) // stable tie-break
                        .FirstOrDefault();

                    if (victim is null)
                        break; // nobody left to bench for this pass on this day — move on

                    victim.RosterDays = victim.RosterDays!.Value & ~day; // bench this player this day
                }
    }

    private static int Count(IReadOnlyList<CwlSignup> signups, RosterDays day) =>
        signups.Count(s => s.RosterDays!.Value.HasFlag(day));

    // Number of days this player is currently rostered.
    private static int DayCount(CwlSignup s) => BitOperations.PopCount((uint)(int)s.RosterDays!.Value);
}
