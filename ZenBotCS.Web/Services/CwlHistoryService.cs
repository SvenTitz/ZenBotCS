using Microsoft.EntityFrameworkCore;
using ZenBotCS.Entities;
using ZenBotCS.Entities.Models;
using ZenBotCS.Entities.Models.Cwl;

namespace ZenBotCS.Web.Services;

/// <summary>
/// Reads a clan's CWL performance history from the <see cref="CwlHistory"/> cache, lazily filling it
/// from ClashKing's war history (<c>/v2/clan/{tag}/wars</c>) on first request or when stale. Finished
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
    // in-progress rounds unreliably).
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

    // Finished CWLs from ClashKing /v2/clan/{tag}/wars. Anything inside the current window is skipped
    // (owned by the CoC-cache path); the rest is upserted per CWL slot, which also repairs rows a
    // partial fetch fragmented earlier.
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

            await CwlHistoryStore.UpsertAsync(db, clanTag, performance, ct);
        }
    }

    // The current, in-progress CWL from the CoC cache (complete + live) — rewritten as rounds land so
    // progressive results and late attacks are reflected, with live bonus flags from the signups.
    private async Task RefreshCurrentAsync(string clanTag, CancellationToken ct)
    {
        var currentWars = await cocCache.GetCurrentCwlWarsAsync(clanTag, ct);
        if (currentWars.Count == 0)
            return;

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        // clanTag is the clan the wars were played in, which for a subroster's host clan is not the
        // clan its players signed up for — resolve the roster first or the bonus flags come back empty.
        var subRosterId = await db.SubRosters
            .Where(sr => sr.GameClanTag == clanTag)
            .Select(sr => (int?)sr.Id)
            .FirstOrDefaultAsync(ct);

        var bonusTags = await db.CwlSignups
            .Where(s => s.Bonus && !s.Archieved)
            .Where(s => subRosterId == null
                ? s.ClanTag == clanTag && s.SubRosterId == null
                : s.SubRosterId == subRosterId)
            .Select(s => s.PlayerTag)
            .ToHashSetAsync(ct);

        foreach (var instanceWars in CwlPerformanceCalculator.GroupIntoCwlInstances(currentWars))
        {
            var performance = CwlPerformanceCalculator.Compute(clanTag, instanceWars, tag => bonusTags.Contains(tag));
            if (performance.Players.Count == 0)
                continue;

            await CwlHistoryStore.UpsertAsync(db, clanTag, performance, ct);
        }
    }
}
