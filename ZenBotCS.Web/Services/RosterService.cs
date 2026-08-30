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
public class RosterService(IDbContextFactory<BotDataContext> dbFactory, CocApiClient cocApi, ClashKingClient clashKing, ILogger<RosterService> logger)
{
    private readonly IDbContextFactory<BotDataContext> _dbFactory = dbFactory;
    private readonly CocApiClient _cocApi = cocApi;
    private readonly ClashKingClient _clashKing = clashKing;
    private readonly ILogger<RosterService> _logger = logger;

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

    /// <summary>
    /// All managed clans worth listing — War, FWA and Event types only (Partner/Other excluded) — in
    /// the DB-defined <see cref="ClanSettings.Order"/>, each tagged with its type and current signup count.
    /// </summary>
    public async Task<List<ClanListItem>> GetClansByTypeAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var clans = await db.ClanSettings
            .Where(cs => cs.ClanType == ClanType.War || cs.ClanType == ClanType.FWA || cs.ClanType == ClanType.Event)
            .OrderBy(cs => cs.Order)
            .Select(cs => new { cs.ClanTag, cs.ClanType, cs.ChampStyleCwlRoster })
            .ToListAsync(ct);

        var counts = await db.CwlSignups
            .Where(s => !s.Archieved)
            .GroupBy(s => s.ClanTag)
            .Select(g => new { ClanTag = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ClanTag, x => x.Count, ct);

        return clans
            .Select(c => new ClanListItem(c.ClanTag, c.ClanType, c.ChampStyleCwlRoster, counts.GetValueOrDefault(c.ClanTag)))
            .ToList();
    }

    /// <summary>
    /// Active (non-archived, non-hidden) signups in one of the clan's rosters, ordered like the sheet
    /// (TH, then name). <paramref name="subRosterId"/> null means the clan's main roster.
    /// </summary>
    public async Task<List<CwlSignup>> GetRosterAsync(string clanTag, int? subRosterId = null, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.CwlSignups
            .Where(s => s.ClanTag == clanTag && s.SubRosterId == subRosterId && !s.Archieved && !s.Hidden)
            .OrderBy(s => s.PlayerThLevel)
            .ThenBy(s => s.PlayerName)
            .ToListAsync(ct);
    }

    /// <summary>Hidden (but not archived) signups in one roster, for the "show hidden" restore section.</summary>
    public async Task<List<CwlSignup>> GetHiddenAsync(string clanTag, int? subRosterId = null, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.CwlSignups
            .Where(s => s.ClanTag == clanTag && s.SubRosterId == subRosterId && !s.Archieved && s.Hidden)
            .OrderBy(s => s.PlayerThLevel)
            .ThenBy(s => s.PlayerName)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Move signups into one of their clan's rosters (null = the main roster) in a single round-trip.
    /// Only moves within the clan, so <see cref="CwlSignup.ClanTag"/> is untouched — a cross-clan move
    /// is <see cref="MoveSignupAsync"/>, which clears the roster instead.
    /// </summary>
    public async Task MoveToSubRosterAsync(IReadOnlyCollection<int> signupIds, int? subRosterId, CancellationToken ct = default)
    {
        if (signupIds.Count == 0)
            return;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var signups = await db.CwlSignups.Where(s => signupIds.Contains(s.Id)).ToListAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var signup in signups)
        {
            signup.SubRosterId = subRosterId;
            signup.UpdatedAt = now;
        }
        await db.SaveChangesAsync(ct);
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
    public async Task<AddResult> AddSignupAsync(string clanTag, string? rawTag, WarPreference warPreference,
        bool bonus, int? subRosterId = null, CancellationToken ct = default)
    {
        var tag = NormalizeTag(rawTag);
        if (string.IsNullOrEmpty(tag))
            return AddResult.Fail("Please enter a player tag.");

        // Player name/TH from the official CoC API (authoritative + current); the Discord link still
        // comes from ClashKing since the official API doesn't have it.
        var player = await _cocApi.GetPlayerAsync(tag, ct);
        if (player is null)
            return AddResult.Fail($"Couldn't find a player with tag {tag}.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // ClashKing's link endpoint goes down often enough to block signups outright, so the bot's
        // DiscordLinks table -- its rolling copy of that API -- is the backup whenever the API has
        // no answer. The copy can be stale (a player who unlinked upstream still resolves to their
        // old user), which beats refusing the signup. A fresh answer is mirrored back into it.
        var discordId = await _clashKing.GetDiscordUserIdAsync(tag, ct);
        if (discordId is null)
        {
            discordId = await db.DiscordLinks
                .Where(dl => dl.PlayerTag == tag)
                .Select(dl => (ulong?)dl.DiscordId)
                .FirstOrDefaultAsync(ct);

            if (discordId is not null)
                _logger.LogInformation("ClashKing had no discord link for {tag}, used the bot's link table", tag);
        }
        else
        {
            db.AddOrUpdateDiscordLink(new DiscordLink { PlayerTag = tag, DiscordId = discordId.Value });
        }

        if (discordId is null)
            return AddResult.Fail($"{player.Value.Name} isn't linked to a Discord account.");

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
            SubRosterId = subRosterId, // lands in whichever roster the leader had open
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
        // A subroster belongs to one owner clan, so a signup leaving that clan can't stay in it —
        // drop the player into the destination clan's main roster.
        signup.SubRosterId = null;
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
    // O/0 mix-up (Clash tags never contain the letter O). Null/blank input yields "" (the autocomplete
    // binds its text to null when the field is empty), so the caller shows "enter a tag" not a crash.
    private static string NormalizeTag(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;
        var t = raw.Trim().ToUpperInvariant().Replace("O", "0");
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

public record ClanListItem(string ClanTag, ClanType ClanType, bool ChampStyle, int SignupCount);
