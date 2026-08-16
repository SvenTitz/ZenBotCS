using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ZenBotCS.Entities;
using ZenBotCS.Entities.Models;

namespace ZenBotCS.Web.Services;

/// <summary>
/// Reads and writes a clan's <see cref="ClanSettings"/> row for the settings page. Mirrors the bot's
/// <c>/clan settings edit</c> / <c>reset</c> (ClanService), but the whole row is edited at once here rather
/// than field-by-field. Uses a short-lived context per call (see Program.cs for why scoped is unsafe).
/// </summary>
public partial class ClanSettingsService(IDbContextFactory<BotDataContext> dbFactory)
{
    // Same format the bot enforces: #RRGGBB or #RRGGBBAA.
    [GeneratedRegex("^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{8})$")]
    private static partial Regex ColorHexRegex();

    /// <summary>The clan's saved settings, or a fresh default (not yet persisted) if it has none.</summary>
    public async Task<ClanSettings> GetAsync(string clanTag, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var existing = await db.ClanSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cs => cs.ClanTag == clanTag, ct);

        return existing ?? new ClanSettings { ClanTag = clanTag };
    }

    /// <summary>True once this clan has a persisted settings row (as opposed to the on-the-fly default).</summary>
    public async Task<bool> ExistsAsync(string clanTag, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.ClanSettings.AnyAsync(cs => cs.ClanTag == clanTag, ct);
    }

    /// <summary>Validates the (optional) colour hex against the same rule the bot uses. Blank is allowed.</summary>
    public static bool IsValidColorHex(string? colorHex)
        => string.IsNullOrWhiteSpace(colorHex) || ColorHexRegex().IsMatch(colorHex);

    /// <summary>Upsert the whole settings row from the edited values. Throws on an invalid colour hex.</summary>
    public async Task SaveAsync(ClanSettings edited, CancellationToken ct = default)
    {
        if (!IsValidColorHex(edited.ColorHex))
            throw new ArgumentException("Colour must be in the format #RRGGBB or #RRGGBBAA.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var row = await db.ClanSettings.FirstOrDefaultAsync(cs => cs.ClanTag == edited.ClanTag, ct);
        if (row is null)
        {
            row = new ClanSettings { ClanTag = edited.ClanTag };
            db.ClanSettings.Add(row);
        }

        row.ClanType = edited.ClanType;
        row.Order = edited.Order;
        row.MemberRoleId = edited.MemberRoleId;
        row.ElderRoleId = edited.ElderRoleId;
        row.LeaderRoleId = edited.LeaderRoleId;
        row.CwlRoleId = edited.CwlRoleId;
        row.ColorHex = string.IsNullOrWhiteSpace(edited.ColorHex) ? null : edited.ColorHex;
        row.EnableCwlSignup = edited.EnableCwlSignup;
        row.ChampStyleCwlRoster = edited.ChampStyleCwlRoster;
        row.CcGoldDump = edited.CcGoldDump;
        row.LeadershipChannelId = edited.LeadershipChannelId;
        row.CwlRosterReminderEnabled = edited.CwlRosterReminderEnabled;
        row.CwlRosterReminderPingRoleId = edited.CwlRosterReminderPingRoleId;
        row.CwlRosterReminderLeadHours = edited.CwlRosterReminderLeadHours;
        row.WarSpinReminderEnabled = edited.WarSpinReminderEnabled;

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Delete the clan's settings row (mirrors the bot's <c>/clan settings reset</c>). No-op if absent.</summary>
    public async Task<bool> ResetAsync(string clanTag, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var row = await db.ClanSettings.FirstOrDefaultAsync(cs => cs.ClanTag == clanTag, ct);
        if (row is null)
            return false;

        db.ClanSettings.Remove(row);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
