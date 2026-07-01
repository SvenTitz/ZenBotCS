using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ZenBotCS.Entities;
using ZenBotCS.Entities.Models.Enums;

namespace ZenBotCS.Services;

/// <summary>
/// Single source of truth for "the CWL roster". Reads either from the DB (the web roster site —
/// the default) or, as a backup, from the pinned Google Sheet. Controlled by the "RosterSource"
/// config key: "Database" (default) or "Spreadsheet". Routing the bot's reminder, roles, and
/// missing-check through here keeps them all agreeing on where the roster comes from.
/// </summary>
public class CwlRosterSource(
    BotDataContext _botDb,
    GspreadService _gspreadService,
    IConfiguration _config,
    ILogger<CwlRosterSource> _logger)
{
    private bool UseSpreadsheet =>
        string.Equals(_config["RosterSource"], "Spreadsheet", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Player tags on the roster. Returns null only in spreadsheet mode when the clan has no pinned
    /// roster (so callers can keep their existing "no pinned roster" message). In DB mode it returns
    /// the clan's active signups (possibly empty).
    /// </summary>
    public async Task<List<string>?> GetRosterPlayerTagsAsync(string clanTag)
    {
        if (UseSpreadsheet)
        {
            var url = GetPinnedUrl(clanTag);
            return url is null ? null : await _gspreadService.GetPlayerTags(url);
        }

        return _botDb.CwlSignups
            .Where(s => s.ClanTag == clanTag && !s.Archieved)
            .Select(s => s.PlayerTag)
            .ToList();
    }

    /// <summary>
    /// Per-player opt-in for the given 0-based CWL day (war 1 = index 0). Returns null only in
    /// spreadsheet mode when the clan has no pinned roster.
    /// </summary>
    public async Task<List<(string Tag, string Name, bool OptedIn)>?> GetDayOptInsAsync(string clanTag, int dayIndex)
    {
        if (UseSpreadsheet)
        {
            var url = GetPinnedUrl(clanTag);
            if (url is null)
                return null;

            var champStyle = _botDb.ClanSettings.FirstOrDefault(cs => cs.ClanTag == clanTag)?.ChampStyleCwlRoster ?? false;
            // Normal roster day columns are D-J (index 3-9); champ-style are I-O (index 8-14).
            var dayColumnIndex = (champStyle ? 8 : 3) + dayIndex;
            return await _gspreadService.GetRosterDayOptIns(url, dayColumnIndex);
        }

        var dayFlag = (RosterDays)(1 << dayIndex);
        // Hidden signups are treated as opted-out of every day here, so they drop out of the pre-war
        // reminder and the missing-day check. (Role assignment uses GetRosterPlayerTagsAsync, which does
        // not filter Hidden, so hidden players still get their CWL roles.)
        return _botDb.CwlSignups
            .Where(s => s.ClanTag == clanTag && !s.Archieved && !s.Hidden)
            .AsEnumerable() // EffectiveRosterDays is a computed (not mapped) member — evaluate client-side
            .Select(s => (s.PlayerTag, s.PlayerName, s.EffectiveRosterDays.HasFlag(dayFlag)))
            .ToList();
    }

    private string? GetPinnedUrl(string clanTag)
    {
        var pinned = _botDb.PinnedRosters.FirstOrDefault(p => p.ClanTag == clanTag);
        if (pinned is null || string.IsNullOrEmpty(pinned.SpreadsheetId))
        {
            _logger.LogWarning("No pinned roster for clan {clan} while in spreadsheet roster mode.", clanTag);
            return null;
        }
        return _gspreadService.GetUrl(pinned);
    }
}
