using Microsoft.EntityFrameworkCore;
using ZenBotCS.Entities;
using ZenBotCS.Entities.Models;
using ZenBotCS.Entities.Models.Enums;

namespace ZenBotCS.Web.Services;

/// <summary>
/// Read-only queries over the bot database for the roster site (Phase 1).
/// Uses a short-lived context per call via <see cref="IDbContextFactory{TContext}"/> — see Program.cs
/// for why a scoped context is unsafe in Blazor Server.
/// </summary>
public class RosterService(IDbContextFactory<BotDataContext> dbFactory)
{
    private readonly IDbContextFactory<BotDataContext> _dbFactory = dbFactory;

    /// <summary>Clans that have CWL signup enabled, with their current active signup count.</summary>
    public async Task<List<ClanSummary>> GetSignupClansAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var clans = await db.ClanSettings
            .Where(cs => cs.EnableCwlSignup)
            .OrderBy(cs => cs.Order)
            .Select(cs => new { cs.ClanTag, cs.ChampStyleCwlRoster })
            .ToListAsync(ct);

        var counts = await db.CwlSignups
            .Where(s => !s.Archieved)
            .GroupBy(s => s.ClanTag)
            .Select(g => new { ClanTag = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ClanTag, x => x.Count, ct);

        return clans
            .Select(c => new ClanSummary(c.ClanTag, c.ChampStyleCwlRoster, counts.GetValueOrDefault(c.ClanTag)))
            .ToList();
    }

    /// <summary>Active (non-archived) signups for a clan, ordered like the roster sheet (TH, then name).</summary>
    public async Task<List<CwlSignup>> GetRosterAsync(string clanTag, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.CwlSignups
            .Where(s => s.ClanTag == clanTag && !s.Archieved)
            .OrderBy(s => s.PlayerThLevel)
            .ThenBy(s => s.PlayerName)
            .ToListAsync(ct);
    }

    public async Task<bool> IsChampStyleAsync(string clanTag, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.ClanSettings
            .Where(cs => cs.ClanTag == clanTag)
            .Select(cs => cs.ChampStyleCwlRoster)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>Persist the leader-edited day lineup for a single signup (absolute value).</summary>
    public async Task SetRosterDaysAsync(int signupId, RosterDays value, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var signup = await db.CwlSignups.FirstOrDefaultAsync(s => s.Id == signupId, ct)
            ?? throw new InvalidOperationException($"Signup {signupId} not found.");
        signup.RosterDays = value;
        signup.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>A clan offered for CWL signup. ClanName is not stored in the bot DB (it comes from CocApi);
/// Phase 1 shows the tag. Enriching with the cached clan name is a follow-up.</summary>
public record ClanSummary(string ClanTag, bool ChampStyle, int SignupCount);
