using Microsoft.EntityFrameworkCore;

namespace ZenBotCS.Entities.Models.Cwl;

/// <summary>
/// Writes a computed CWL instance into <see cref="CwlHistory"/>. Shared by the website's lazy fill
/// and the bot's daily snapshot so both agree on what identifies a CWL row: the calendar slot
/// (<see cref="CwlPerformanceCalculator.InstanceKey"/>), not the start time of whichever war
/// happened to be visible. Keying on the observed first war let every partial fetch — a CWL that
/// had slid halfway out of the cache window, a round ClashKing hadn't ingested — insert a second
/// row for a CWL that already had one, which is how one season ended up listed three times.
/// </summary>
public static class CwlHistoryStore
{
    /// <summary>
    /// Store <paramref name="performance"/> as the clan's row for its CWL slot: inserting it,
    /// updating the existing row, or folding stray fragment rows for the same CWL back into one.
    /// A snapshot with fewer rounds than the stored one never replaces it, so a partial refresh
    /// can only ever add to what's there.
    /// </summary>
    public static async Task UpsertAsync(
        BotDataContext db, string clanTag, CwlSeasonPerformance performance, CancellationToken ct = default)
    {
        var key = CwlPerformanceCalculator.InstanceKey(performance.StartTime);

        // Both halves of a month share one Season string, so narrow in SQL and match the slot here.
        var rows = (await db.CwlHistories
                .Where(h => h.ClanTag == clanTag && h.Season == performance.Season)
                .ToListAsync(ct))
            .Where(h => CwlPerformanceCalculator.InstanceKey(h.StartTime) == key)
            .OrderBy(h => h.StartTime)
            .ToList();

        if (rows.Count == 0)
        {
            db.CwlHistories.Add(new CwlHistory
            {
                ClanTag = clanTag,
                Season = performance.Season,
                StartTime = performance.StartTime,
                Performance = performance,
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(ct);
            return;
        }

        // The bonus can only be read while the signups are live, so it has to survive every later
        // recompute (the historical path can't derive it from war data and passes it as false).
        var bonusTags = rows
            .SelectMany(r => r.Performance?.Players ?? [])
            .Concat(performance.Players)
            .Where(p => p.Bonus)
            .Select(p => p.PlayerTag)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // One slot, one row: everything else here is a fragment left by a partial fetch. Keep the
        // fullest of them (not simply the earliest — its rounds may be the only copy we have).
        var row = rows
            .OrderByDescending(r => CwlPerformanceCalculator.RoundsWithData(r.Performance))
            .ThenBy(r => r.StartTime)
            .First();

        if (rows.Count > 1)
        {
            // Drop the fragments in their own round-trip: the survivor may be about to take a start
            // time one of them still holds, and the unique index doesn't wait for the delete.
            db.CwlHistories.RemoveRange(rows.Where(r => r != row));
            await db.SaveChangesAsync(ct);
        }

        var keep = CwlPerformanceCalculator.RoundsWithData(performance) >= CwlPerformanceCalculator.RoundsWithData(row.Performance)
            ? performance
            : row.Performance!;

        var earliest = rows[0].StartTime <= performance.StartTime ? rows[0].StartTime : performance.StartTime;
        keep.StartTime = earliest;
        foreach (var player in keep.Players)
            player.Bonus = player.Bonus || bonusTags.Contains(player.PlayerTag);

        row.Performance = keep;
        row.StartTime = earliest;
        row.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
    }
}
