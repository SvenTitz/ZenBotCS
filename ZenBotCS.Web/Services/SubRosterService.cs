using Microsoft.EntityFrameworkCore;
using ZenBotCS.Entities;
using ZenBotCS.Entities.Models;
using ZenBotCS.Entities.Models.Enums;

namespace ZenBotCS.Web.Services;

/// <summary>
/// Create, edit and delete the rosters a clan splits its signups into. The clan's main roster has no
/// row — see <see cref="SubRoster"/> — so everything here deals only with the extra ones.
/// Uses a short-lived context per call via <see cref="IDbContextFactory{TContext}"/>, like
/// <see cref="RosterService"/>.
/// </summary>
public class SubRosterService(IDbContextFactory<BotDataContext> dbFactory)
{
    private readonly IDbContextFactory<BotDataContext> _dbFactory = dbFactory;

    /// <summary>The clan's subrosters in tab order.</summary>
    public async Task<List<SubRoster>> GetForClanAsync(string clanTag, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.SubRosters
            .Where(sr => sr.ClanTag == clanTag)
            .OrderBy(sr => sr.Order)
            .ThenBy(sr => sr.Id)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Clans that could host a new roster for <paramref name="clanTag"/>: event and partner clans
    /// that no other roster has claimed. Ordered like the rest of the site, by ClanSettings.Order.
    /// </summary>
    public async Task<List<string>> GetAvailableHostClanTagsAsync(string clanTag, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var taken = await db.SubRosters.Select(sr => sr.GameClanTag).ToListAsync(ct);

        return await db.ClanSettings
            .Where(cs => cs.ClanType == ClanType.Event || cs.ClanType == ClanType.Partner)
            .Where(cs => cs.ClanTag != clanTag && !taken.Contains(cs.ClanTag))
            .OrderBy(cs => cs.Order)
            .Select(cs => cs.ClanTag)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Create a roster hosted in another clan, after checking it against <see cref="SubRosterRules"/>.
    /// Returns the reason on failure so the dialog can show it instead of a database error.
    /// </summary>
    public async Task<SubRosterResult> CreateAsync(string clanTag, string gameClanTag, string name,
        int targetSize, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var hostClanType = await db.ClanSettings
            .Where(cs => cs.ClanTag == gameClanTag)
            .Select(cs => (ClanType?)cs.ClanType)
            .FirstOrDefaultAsync(ct);

        var existing = await db.SubRosters.ToListAsync(ct);
        var check = SubRosterRules.ValidateNew(clanTag, gameClanTag, name, hostClanType, existing);
        if (!check.Ok)
            return SubRosterResult.Fail(check.Error!);

        var subRoster = new SubRoster
        {
            ClanTag = clanTag,
            GameClanTag = gameClanTag,
            Name = name.Trim(),
            TargetSize = targetSize,
            Order = existing.Where(sr => sr.ClanTag == clanTag).Select(sr => sr.Order).DefaultIfEmpty(0).Max() + 1,
        };
        db.SubRosters.Add(subRoster);
        await db.SaveChangesAsync(ct);
        return SubRosterResult.Success(subRoster);
    }

    public async Task RenameAsync(int subRosterId, string name, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var subRoster = await db.SubRosters.FirstOrDefaultAsync(sr => sr.Id == subRosterId, ct)
            ?? throw new InvalidOperationException($"Sub-roster {subRosterId} not found.");
        subRoster.Name = name.Trim();
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Persist a roster's war size. Null <paramref name="subRosterId"/> means the main roster,
    /// whose size lives on <see cref="ClanSettings.CwlRosterTargetSize"/>.</summary>
    public async Task SetTargetSizeAsync(string clanTag, int? subRosterId, int targetSize, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        if (subRosterId is null)
        {
            var settings = await db.ClanSettings.FirstOrDefaultAsync(cs => cs.ClanTag == clanTag, ct);
            if (settings is null)
                return; // clan isn't managed — nothing to store the size on
            settings.CwlRosterTargetSize = targetSize;
        }
        else
        {
            var subRoster = await db.SubRosters.FirstOrDefaultAsync(sr => sr.Id == subRosterId, ct)
                ?? throw new InvalidOperationException($"Sub-roster {subRosterId} not found.");
            subRoster.TargetSize = targetSize;
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>War size for a roster — the subroster's own, or the clan's for the main roster.</summary>
    public async Task<int> GetTargetSizeAsync(string clanTag, int? subRosterId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var size = subRosterId is null
            ? await db.ClanSettings.Where(cs => cs.ClanTag == clanTag)
                .Select(cs => (int?)cs.CwlRosterTargetSize).FirstOrDefaultAsync(ct)
            : await db.SubRosters.Where(sr => sr.Id == subRosterId)
                .Select(sr => (int?)sr.TargetSize).FirstOrDefaultAsync(ct);

        // Clans that predate the stored size, or a roster that just vanished, fall back to 15v15.
        return size is null or 0 ? 15 : size.Value;
    }

    /// <summary>
    /// Delete a roster. Its players return to the clan's main roster — the FK is SetNull, so their
    /// signups (and day lineups) survive untouched.
    /// </summary>
    public async Task DeleteAsync(int subRosterId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var subRoster = await db.SubRosters.FirstOrDefaultAsync(sr => sr.Id == subRosterId, ct);
        if (subRoster is null)
            return;
        db.SubRosters.Remove(subRoster);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>How many active signups sit in each of the clan's rosters, keyed by sub-roster id
    /// (null = the main roster). Drives the counts on the tab strip.</summary>
    public async Task<Dictionary<int?, int>> GetCountsAsync(string clanTag, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.CwlSignups
            .Where(s => s.ClanTag == clanTag && !s.Archieved && !s.Hidden)
            .GroupBy(s => s.SubRosterId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
    }
}

/// <summary>Outcome of creating a roster: the new row, or the reason it was rejected.</summary>
public record SubRosterResult(bool Ok, string? Error, SubRoster? SubRoster = null)
{
    public static SubRosterResult Success(SubRoster subRoster) => new(true, null, subRoster);
    public static SubRosterResult Fail(string error) => new(false, error);
}
