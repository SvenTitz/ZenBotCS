using System.Globalization;
using ZenBotCS.Entities.Models.ClashKingApi;

namespace ZenBotCS.Entities.Models.Cwl;

/// <summary>
/// Turns a clan's raw war history (from <c>/war/{tag}/previous</c>, where each war's <c>Clan</c> is
/// the queried clan) into computed CWL performance, reproducing the family's Google-sheet metrics.
/// Pure and stateless so both the website (lazy fill) and the bot (background snapshot) can share it.
/// </summary>
public static class CwlPerformanceCalculator
{
    // CoC timestamps look like "20260602T195213.000Z".
    private const string CocTimeFormat = "yyyyMMdd'T'HHmmss.fff'Z'";

    // A CWL runs one war per day for ~7 days; the next CWL is ~10 days later. Split into a new
    // instance only on a gap this large, so a missing/not-yet-ingested round in the middle doesn't
    // fracture one CWL into two, while two CWLs in the same month still separate cleanly.
    private static readonly TimeSpan InstanceGap = TimeSpan.FromDays(5);

    /// <summary>
    /// Split a clan's war history into individual CWL instances (each a run of ~7 consecutive
    /// tagged wars), newest first. Regular wars (no <see cref="WarData.WarTag"/>) are ignored.
    /// </summary>
    public static List<List<WarData>> GroupIntoCwlInstances(IEnumerable<WarData> wars)
    {
        var cwlWars = wars
            .Where(w => !string.IsNullOrEmpty(w.WarTag))
            .Select(w => (War: w, Start: ParseTime(w.StartTime)))
            .Where(x => x.Start != default)
            .OrderBy(x => x.Start)
            .ToList();

        var instances = new List<List<WarData>>();
        List<WarData>? current = null;
        DateTime prevStart = default;

        foreach (var (war, start) in cwlWars)
        {
            if (current is null || start - prevStart > InstanceGap)
            {
                current = [];
                instances.Add(current);
            }
            current.Add(war);
            prevStart = start;
        }

        instances.Reverse(); // newest instance first
        return instances;
    }

    /// <summary>The earliest war start in an instance (its identifying <c>StartTime</c>), or default if empty.</summary>
    public static DateTime GroupStart(IEnumerable<WarData> instanceWars)
    {
        return instanceWars
            .Select(w => ParseTime(w.StartTime))
            .Where(t => t != default)
            .DefaultIfEmpty(default)
            .Min();
    }

    /// <summary>
    /// Compute one CWL instance's performance for <paramref name="clanTag"/>. <paramref name="instanceWars"/>
    /// is the set of wars for a single CWL (see <see cref="GroupIntoCwlInstances"/>);
    /// <paramref name="hasBonus"/> reports whether a player tag was flagged for the roster bonus.
    /// </summary>
    public static CwlSeasonPerformance Compute(
        string clanTag,
        IReadOnlyList<WarData> instanceWars,
        Func<string, bool> hasBonus)
    {
        var rounds = instanceWars
            .Where(w => OurSide(w, clanTag) is not null)   // only wars the clan actually played
            .GroupBy(w => w.WarTag)                         // one entry per CWL round (dedupe both orientations)
            .Select(g => g.First())
            .Select(w => (War: w, Start: ParseTime(w.StartTime)))
            .OrderBy(x => x.Start)
            .Take(7)
            .Select(x => x.War)
            .ToList();

        var start = rounds.Count > 0 ? ParseTime(rounds[0].StartTime) : default;
        var result = new CwlSeasonPerformance
        {
            ClanTag = clanTag,
            ClanName = rounds.Select(w => OurSide(w, clanTag)?.Name).FirstOrDefault(n => !string.IsNullOrEmpty(n)) ?? string.Empty,
            Season = start == default ? string.Empty : start.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            StartTime = start,
        };

        var players = new Dictionary<string, CwlPlayerPerformance>();

        for (var round = 0; round < rounds.Count; round++)
        {
            var war = rounds[round];
            // Don't assume the queried clan is the `clan` side — ClashKing can return a war with it
            // as `opponent`. Picking the wrong side would aggregate the enemy roster (a different clan
            // each round), inflating the player list. Skip a war the clan isn't actually in.
            var ourClan = OurSide(war, clanTag);
            if (ourClan is null)
                continue;
            var opponent = ReferenceEquals(ourClan, war.Clan) ? war.Opponent : war.Clan;
            var thMinIndexMap = GetThMinIndexMap(opponent);

            foreach (var member in ourClan.Members)
            {
                var player = GetOrAdd(players, member);
                // Latest appearance wins for the display name (CoC names can change).
                player.PlayerName = member.Name;

                if (member.Attacks.Count > 0)
                {
                    var attack = member.Attacks[0];
                    var defender = opponent.Members.FirstOrDefault(m => m.Tag == attack.DefenderTag);
                    var defenderTh = defender?.TownhallLevel ?? 0;
                    var rush = defender is null ? 0 : GetRushScore(defenderTh, defender.MapPosition, thMinIndexMap);

                    player.Days[round] = new CwlAttackCell
                    {
                        Stars = attack.Stars,
                        DestructionPercentage = (int)Math.Round(attack.DestructionPercentage),
                        DefenderTownHall = defenderTh,
                        DefenderRushScore = rush,
                        IsMissed = false,
                    };
                }
                else
                {
                    player.Days[round] = new CwlAttackCell { IsMissed = true };
                }
            }
        }

        foreach (var player in players.Values)
        {
            FillAggregates(player);
            result.Players.Add(player);
        }

        for (var round = 0; round < 7; round++)
        {
            result.DailyTotalStars[round] = result.Players.Sum(p => p.Days[round]?.Stars ?? 0);
            result.DailyTotalDestruction[round] = result.Players.Sum(p => p.Days[round]?.DestructionPercentage ?? 0);
        }

        foreach (var player in result.Players)
            player.Bonus = hasBonus(player.PlayerTag);

        result.Players = result.Players
            .OrderByDescending(p => p.Score)
            .ThenByDescending(p => p.AverageStars)
            .ToList();

        return result;
    }

    // The side of the war that is the queried clan (by tag), or null if it isn't in this war.
    private static WarClan? OurSide(WarData war, string clanTag)
    {
        if (string.Equals(war.Clan.Tag, clanTag, StringComparison.OrdinalIgnoreCase))
            return war.Clan;
        if (string.Equals(war.Opponent.Tag, clanTag, StringComparison.OrdinalIgnoreCase))
            return war.Opponent;
        return null;
    }

    private static CwlPlayerPerformance GetOrAdd(Dictionary<string, CwlPlayerPerformance> players, WarMember member)
    {
        if (!players.TryGetValue(member.Tag, out var player))
        {
            player = new CwlPlayerPerformance
            {
                PlayerTag = member.Tag,
                PlayerName = member.Name,
                TownHallLevel = member.TownhallLevel,
            };
            players[member.Tag] = player;
        }
        return player;
    }

    // Reproduces the sheet: a "hit" is any round the player was in the lineup (attacked or missed);
    // rounds the player wasn't rostered stay null and don't count. Averages divide by hits; reach is
    // Σ(effective defender TH) − ownTH × hits; score = avg stars + reach ÷ hits ÷ 3.
    private static void FillAggregates(CwlPlayerPerformance player)
    {
        var cells = player.Days.Where(c => c is not null).Select(c => c!).ToList();
        player.Hits = cells.Count;
        if (player.Hits == 0)
            return;

        var sumStars = cells.Sum(c => c.Stars);
        var sumDestruction = cells.Sum(c => c.DestructionPercentage);
        var sumEffectiveTh = cells.Sum(c => c.EffectiveDefenderTownHall);

        player.ReachPlusMinus = sumEffectiveTh - player.TownHallLevel * player.Hits;
        player.AverageStars = (double)sumStars / player.Hits;
        player.AverageDestruction = (double)sumDestruction / player.Hits;
        player.Score = player.AverageStars + (double)player.ReachPlusMinus / player.Hits / 3;
    }

    // For each opponent TH, the highest map slot (smallest map position) any defender of that TH holds.
    private static Dictionary<int, int> GetThMinIndexMap(WarClan opponent)
    {
        return opponent.Members
            .GroupBy(m => m.TownhallLevel)
            .ToDictionary(g => g.Key, g => g.Min(m => m.MapPosition));
    }

    // How rushed the defender is: their real TH minus the lowest TH that "belongs" at their map slot.
    // Ported from CwlDataMemberAttack.GetTownhallLevelDifferenceFromMap.
    private static int GetRushScore(int defenderTownHall, int defenderMapPosition, Dictionary<int, int> thMinIndexMap)
    {
        var lowerTh = thMinIndexMap
            .Where(kv => kv.Value <= defenderMapPosition)
            .Select(kv => kv.Key)
            .DefaultIfEmpty(defenderTownHall)
            .Min();

        return defenderTownHall - lowerTh;
    }

    private static DateTime ParseTime(string raw)
    {
        return DateTime.TryParseExact(raw, CocTimeFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt)
            ? dt
            : default;
    }
}
