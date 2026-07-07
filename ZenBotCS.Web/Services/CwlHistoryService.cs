using Microsoft.EntityFrameworkCore;
using ZenBotCS.Entities;
using ZenBotCS.Entities.Models;
using ZenBotCS.Entities.Models.Cwl;

namespace ZenBotCS.Web.Services;

/// <summary>
/// Reads a clan's CWL performance history from the <see cref="CwlHistory"/> cache, lazily filling it
/// from ClashKing's war history (<c>/war/{tag}/previous</c>) on first request or when stale. Finished
/// CWLs are immutable so they're computed once and served from the DB thereafter; the most recent
/// instance is refreshed while it may still be in progress. Per-operation DbContext (Blazor Server).
/// </summary>
public class CwlHistoryService(
    IDbContextFactory<BotDataContext> dbFactory,
    ClashKingClient clashKing,
    CocCacheCwlService cocCache,
    ILogger<CwlHistoryService> logger)
{
    // A CWL whose first war started within this many days is treated as "current": it comes from the
    // CoC cache (complete + live), and is excluded from the ClashKing /previous fill (which delivers
    // in-progress rounds unreliably). Matches CocCacheCwlService's window.
    private static readonly TimeSpan CurrentWindow = TimeSpan.FromDays(9);

    public record CwlInstanceInfo(string Season, DateTime StartTime);

    /// <summary>Available CWL instances for a clan (newest first), filling/refreshing the cache as needed.</summary>
    public async Task<List<CwlInstanceInfo>> GetInstancesAsync(string clanTag, CancellationToken ct = default)
    {
        await EnsureFreshAsync(clanTag, ct);

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.CwlHistories
            .Where(h => h.ClanTag == clanTag)
            .OrderByDescending(h => h.StartTime)
            .Select(h => new CwlInstanceInfo(h.Season, h.StartTime))
            .ToListAsync(ct);
    }

    /// <summary>The computed performance for one CWL instance, or null if it isn't available.</summary>
    public async Task<CwlSeasonPerformance?> GetPerformanceAsync(
        string clanTag, string season, DateTime startTime, CancellationToken ct = default)
    {
        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            var cached = await db.CwlHistories
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.ClanTag == clanTag && h.Season == season && h.StartTime == startTime, ct);
            if (cached?.Performance is not null)
                return cached.Performance;
        }

        // Not cached yet (e.g. an old season never viewed) — backfill and try again.
        await RefreshHistoricalAsync(clanTag, ct);
        await RefreshCurrentAsync(clanTag, ct);

        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            var row = await db.CwlHistories
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.ClanTag == clanTag && h.Season == season && h.StartTime == startTime, ct);
            return row?.Performance;
        }
    }

    private async Task EnsureFreshAsync(string clanTag, CancellationToken ct)
    {
        // The current CWL is live and cheap (one local cache query) — always refresh it. It must NOT
        // be gated by the historical cache, or a bot/earlier fill that skips the current window would
        // leave the in-progress CWL permanently missing.
        await RefreshCurrentAsync(clanTag, ct);

        // Historical CWLs are immutable — backfill from ClashKing only when we have none cached yet
        // (the bot's daily job normally keeps these populated).
        var currentCutoff = DateTime.UtcNow - CurrentWindow;
        bool hasFinished;
        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            hasFinished = await db.CwlHistories.AnyAsync(
                h => h.ClanTag == clanTag && h.StartTime < currentCutoff, ct);
        }

        if (!hasFinished)
            await RefreshHistoricalAsync(clanTag, ct);
    }

    // Finished CWLs from ClashKing /war/previous — immutable, inserted once, never overwritten.
    // Anything inside the current window is skipped (owned by the CoC-cache path).
    private async Task RefreshHistoricalAsync(string clanTag, CancellationToken ct)
    {
        var history = await clashKing.GetClanWarHistoryAsync(clanTag, limit: 300, ct: ct);
        if (history is null)
        {
            logger.LogWarning("CWL refresh: no war history returned for {tag}", clanTag);
            return;
        }

        var currentCutoff = DateTime.UtcNow - CurrentWindow;
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        foreach (var instanceWars in CwlPerformanceCalculator.GroupIntoCwlInstances(history))
        {
            if (CwlPerformanceCalculator.GroupStart(instanceWars) >= currentCutoff)
                continue;

            var performance = CwlPerformanceCalculator.Compute(clanTag, instanceWars, _ => false);
            if (performance.Players.Count == 0)
                continue;

            var exists = await db.CwlHistories.AnyAsync(
                h => h.ClanTag == clanTag && h.Season == performance.Season && h.StartTime == performance.StartTime, ct);
            if (!exists)
            {
                db.CwlHistories.Add(new CwlHistory
                {
                    ClanTag = clanTag,
                    Season = performance.Season,
                    StartTime = performance.StartTime,
                    Performance = performance,
                    UpdatedAt = DateTime.UtcNow,
                });
            }
        }

        await db.SaveChangesAsync(ct);
    }

    // The current, in-progress CWL from the CoC cache (complete + live) — always overwritten so
    // progressive results and late attacks are reflected, with live bonus flags from the signups.
    private async Task RefreshCurrentAsync(string clanTag, CancellationToken ct)
    {
        var currentWars = await cocCache.GetCurrentCwlWarsAsync(clanTag, ct: ct);
        if (currentWars.Count == 0)
            return;

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var bonusTags = await db.CwlSignups
            .Where(s => s.ClanTag == clanTag && s.Bonus && !s.Archieved)
            .Select(s => s.PlayerTag)
            .ToHashSetAsync(ct);

        foreach (var instanceWars in CwlPerformanceCalculator.GroupIntoCwlInstances(currentWars))
        {
            var performance = CwlPerformanceCalculator.Compute(clanTag, instanceWars, tag => bonusTags.Contains(tag));
            if (performance.Players.Count == 0)
                continue;

            var existing = await db.CwlHistories.FirstOrDefaultAsync(
                h => h.ClanTag == clanTag && h.Season == performance.Season && h.StartTime == performance.StartTime, ct);
            if (existing is null)
            {
                db.CwlHistories.Add(new CwlHistory
                {
                    ClanTag = clanTag,
                    Season = performance.Season,
                    StartTime = performance.StartTime,
                    Performance = performance,
                    UpdatedAt = DateTime.UtcNow,
                });
            }
            else
            {
                existing.Performance = performance;
                existing.UpdatedAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
