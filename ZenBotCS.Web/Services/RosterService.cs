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
public class RosterService(IDbContextFactory<BotDataContext> dbFactory, ClashKingClient clashKing)
{
    private readonly IDbContextFactory<BotDataContext> _dbFactory = dbFactory;
    private readonly ClashKingClient _clashKing = clashKing;

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

    /// <summary>Active (non-archived, non-hidden) signups for a clan, ordered like the sheet (TH, then name).</summary>
    public async Task<List<CwlSignup>> GetRosterAsync(string clanTag, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.CwlSignups
            .Where(s => s.ClanTag == clanTag && !s.Archieved && !s.Hidden)
            .OrderBy(s => s.PlayerThLevel)
            .ThenBy(s => s.PlayerName)
            .ToListAsync(ct);
    }

    /// <summary>Hidden (but not archived) signups for a clan, for the "show hidden" restore section.</summary>
    public async Task<List<CwlSignup>> GetHiddenAsync(string clanTag, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.CwlSignups
            .Where(s => s.ClanTag == clanTag && !s.Archieved && s.Hidden)
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

    /// <summary>Persist a whole set of lineups at once (used by the solver) in a single round-trip.</summary>
    public async Task SetRosterDaysBulkAsync(IReadOnlyDictionary<int, RosterDays> values, CancellationToken ct = default)
    {
        if (values.Count == 0)
            return;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var ids = values.Keys.ToList();
        var signups = await db.CwlSignups.Where(s => ids.Contains(s.Id)).ToListAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var s in signups)
            if (values.TryGetValue(s.Id, out var v))
            {
                s.RosterDays = v;
                s.UpdatedAt = now;
            }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Add a signup, mirroring the bot's <c>/cwl signup add</c>: look the player up on ClashKing for
    /// name/TH, require a linked Discord account, and reject a duplicate active signup. Returns a
    /// user-facing result — <see cref="AddResult.Ok"/> false carries the message to show.
    /// </summary>
    public async Task<AddResult> AddSignupAsync(string clanTag, string rawTag, WarPreference warPreference,
        bool bonus, CancellationToken ct = default)
    {
        var tag = NormalizeTag(rawTag);
        if (string.IsNullOrEmpty(tag))
            return AddResult.Fail("Please enter a player tag.");

        var player = await _clashKing.GetPlayerAsync(tag, ct);
        if (player is null)
            return AddResult.Fail($"Couldn't find a player with tag {tag}.");

        var discordId = await _clashKing.GetDiscordUserIdAsync(tag, ct);
        if (discordId is null)
            return AddResult.Fail($"{player.Value.Name} isn't linked to a Discord account.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        if (await db.CwlSignups.AnyAsync(s => s.PlayerTag == tag && !s.Archieved, ct))
            return AddResult.Fail($"{player.Value.Name} is already signed up.");

        db.CwlSignups.Add(new CwlSignup
        {
            PlayerTag = tag,
            PlayerName = player.Value.Name,
            PlayerThLevel = player.Value.TownHall,
            ClanTag = clanTag,
            DiscordId = discordId.Value,
            OptOutDays = OptOutDays.None,
            WarPreference = warPreference,
            Bonus = bonus,
        });
        await db.SaveChangesAsync(ct);
        return AddResult.Success($"Added {player.Value.Name} (TH{player.Value.TownHall}).", player.Value.Name);
    }

    /// <summary>Hard-delete a signup (mirrors the bot's <c>/cwl signup delete</c>).</summary>
    public async Task DeleteSignupAsync(int signupId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var signup = await db.CwlSignups.FirstOrDefaultAsync(s => s.Id == signupId, ct);
        if (signup is null)
            return;
        db.CwlSignups.Remove(signup);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Move a signup to another clan (mirrors the bot's <c>/cwl signup move</c>).</summary>
    public async Task MoveSignupAsync(int signupId, string newClanTag, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var signup = await db.CwlSignups.FirstOrDefaultAsync(s => s.Id == signupId, ct)
            ?? throw new InvalidOperationException($"Signup {signupId} not found.");
        signup.ClanTag = newClanTag;
        signup.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Hide or unhide a signup (kept in the DB; see <see cref="CwlSignup.Hidden"/>).</summary>
    public async Task SetHiddenAsync(int signupId, bool hidden, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var signup = await db.CwlSignups.FirstOrDefaultAsync(s => s.Id == signupId, ct)
            ?? throw new InvalidOperationException($"Signup {signupId} not found.");
        signup.Hidden = hidden;
        signup.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    // Tidy a user-entered tag: trim, uppercase, ensure a single leading '#', and fix the common
    // O/0 mix-up (Clash tags never contain the letter O).
    private static string NormalizeTag(string raw)
    {
        var t = raw.Trim().ToUpperInvariant().Replace("O", "0");
        if (t.Length == 0)
            return string.Empty;
        return t.StartsWith('#') ? t : "#" + t;
    }
}

/// <summary>Outcome of <see cref="RosterService.AddSignupAsync"/>: whether it worked, a message to
/// show, and (on success) the resolved player name for logging.</summary>
public record AddResult(bool Ok, string Message, string? PlayerName = null)
{
    public static AddResult Success(string message, string? playerName = null) => new(true, message, playerName);
    public static AddResult Fail(string message) => new(false, message);
}

/// <summary>A clan offered for CWL signup. ClanName is not stored in the bot DB (it comes from CocApi);
/// Phase 1 shows the tag. Enriching with the cached clan name is a follow-up.</summary>
public record ClanSummary(string ClanTag, bool ChampStyle, int SignupCount);
