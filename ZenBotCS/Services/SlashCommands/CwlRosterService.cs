using System.Text;
using CocApi.Rest.Models;
using Discord;
using ZenBotCS.Clients;
using ZenBotCS.Entities;
using ZenBotCS.Entities.Models;
using ZenBotCS.Entities.Models.Enums;
using ZenBotCS.Extensions;
using ZenBotCS.Helper;
using ZenBotCS.Models;

namespace ZenBotCS.Services.SlashCommands
{
    public class CwlRosterService(
        CustomClansClient _clansClient,
        BotDataContext _botDb,
        GspreadService _gspreadService,
        EmbedHelper _embedHelper,
        ClashKingApiService _clashKingApiService)
    {
        public async Task<(Embed, MessageComponent?)> SignupRoster(string clanTag, bool forceNew)
        {
            try
            {
                var clan = await _clansClient.GetOrFetchClanAsync(clanTag);

                var pinnedRoster = _botDb.PinnedRosters.FirstOrDefault(pr => pr.ClanTag == clanTag);
                if (pinnedRoster is not null
                    && !string.IsNullOrEmpty(pinnedRoster.SpreadsheetId)
                    && !forceNew)
                {
                    var embed = new EmbedBuilder()
                        .WithTitle($"{clan.Name} Cwl Roster")
                        .WithDescription($"The link below is the pinned roster for {clan.Name}." +
                            $"\nIf you want to generate a new roster, please make sure to set the optional parameter `ForceNew` to true." +
                            $"\nYou can also reset the pin by calling `/cwl signup pin-roster` with an empty url.")
                        .WithColor(Color.DarkPurple)
                        .Build();

                    var urlButton = new ButtonBuilder()
                        .WithLabel("Pinned Roster")
                        .WithUrl(_gspreadService.GetUrl(pinnedRoster))
                        .WithStyle(ButtonStyle.Link);

                    var components = new ComponentBuilder()
                        .WithButton(urlButton)
                        .Build();

                    return (embed, components);
                }
                else
                {
                    var signups = _botDb.CwlSignups.Where(s => s.ClanTag == clanTag && !s.Archieved).ToList();

                    var clanSettings = _botDb.ClanSettings.FirstOrDefault(cs => cs.ClanTag == clanTag);
                    object?[][] data;
                    string url;
                    if (clanSettings?.ChampStyleCwlRoster ?? false)
                    {
                        data = await FormatDataForRosterSpreadsheetChampStyle(signups);
                        url = await _gspreadService.WriteCwlRosterData(data, clan, clanSettings, true);
                    }
                    else
                    {
                        data = FormatDataForRosterSpreadsheet(signups);
                        url = await _gspreadService.WriteCwlRosterData(data, clan, clanSettings, false);
                    }

                    var embed = new EmbedBuilder()
                            .WithTitle($"{clan.Name} Cwl Roster")
                            .WithDescription($"Please make sure to pin this roster with `/cwl signup pin-roster` if this is the final roster you start working on.")
                            .WithColor(Color.DarkPurple)
                            .Build();

                    var urlButton = new ButtonBuilder()
                        .WithLabel("Roster")
                        .WithUrl(url)
                        .WithStyle(ButtonStyle.Link);

                    var components = new ComponentBuilder()
                        .WithButton(urlButton)
                        .Build();

                    return (embed, components);
                }
            }
            catch (Exception ex)
            {
                return (_embedHelper.ErrorEmbed("Error", ex.Message), null);
            }
        }

        public async Task<Embed> SingupRosterPin(string clanTag, string rosterUrl)
        {
            try
            {
                var clan = await _clansClient.GetOrFetchClanAsync(clanTag);

                if (string.IsNullOrEmpty(rosterUrl))
                {
                    return SignupRosterPinRemove(clan);
                }

                (var spreadsheetId, var gid) = _gspreadService.ExtractSpreadsheetInfo(rosterUrl);
                if (spreadsheetId is null || gid is null)
                {
                    throw new ArgumentException(@"Invalid spreadsheet url. Please make sure it follow the pattern `https://docs.google.com/spreadsheets/d/<sheetId>#gid=<gid>`");
                }

                var pinnedRoster = _botDb.PinnedRosters.FirstOrDefault(pr => pr.ClanTag == clanTag);
                if (pinnedRoster == null)
                {
                    pinnedRoster = new PinnedRoster { ClanTag = clanTag };
                    _botDb.PinnedRosters.Add(pinnedRoster);
                }
                pinnedRoster.SpreadsheetId = spreadsheetId;
                pinnedRoster.Gid = gid;
                _botDb.SaveChanges();

                return new EmbedBuilder()
                    .WithColor(Color.Green)
                    .WithDescription($"Pinned [Roster]({rosterUrl}) for {clan.Name}.")
                    .Build();
            }
            catch (Exception ex)
            {
                return _embedHelper.ErrorEmbed("Error", ex.Message);
            }
        }

        private Embed SignupRosterPinRemove(Clan clan)
        {
            var pinnedRoster = _botDb.PinnedRosters.FirstOrDefault(pr => pr.ClanTag == clan.Tag);
            if (pinnedRoster is not null)
            {
                _botDb.PinnedRosters.Remove(pinnedRoster);
                _botDb.SaveChanges();
            }

            return new EmbedBuilder()
                    .WithColor(Color.Green)
                    .WithDescription($"Reset pinned roster for {clan.Name}.")
                    .Build();
        }

        private object?[][] FormatDataForRosterSpreadsheet(List<CwlSignup> signups)
        {
            var data = new List<List<object?>>();

            signups = signups.OrderBy(s => s.PlayerThLevel).ThenBy(s => s.PlayerName).ToList();
            foreach (var signup in signups)
            {
                data.Add(
                [
                    signup.PlayerName,
                    signup.PlayerTag,
                    signup.PlayerThLevel,
                    signup.OptOutDays.HasFlag(OptOutDays.Day1) ? "" : 1,
                    signup.OptOutDays.HasFlag(OptOutDays.Day2) ? "" : 1,
                    signup.OptOutDays.HasFlag(OptOutDays.Day3) ? "" : 1,
                    signup.OptOutDays.HasFlag(OptOutDays.Day4) ? "" : 1,
                    signup.OptOutDays.HasFlag(OptOutDays.Day5) ? "" : 1,
                    signup.OptOutDays.HasFlag(OptOutDays.Day6) ? "" : 1,
                    signup.OptOutDays.HasFlag(OptOutDays.Day7) ? "" : 1,
                    null,
                    signup.Bonus ? "Yes" : "No",
                    signup.WarPreference.ToString(),
                ]);

            }

            return data.Select(d => d.ToArray()).ToArray();
        }

        private async Task<object?[][]> FormatDataForRosterSpreadsheetChampStyle(List<CwlSignup> signups)
        {
            var data = new List<List<object?>>();

            var hitrates1Month = await GetLastMonthHitrates(signups.Select(s => s.PlayerTag).ToList());
            var hitrates3Months = await GetThreeMonthsHitrates(signups.Select(s => s.PlayerTag).ToList());

            signups = [.. signups.OrderBy(s => s.PlayerThLevel).ThenBy(s => s.PlayerName)];
            foreach (var signup in signups)
            {
                var hitrate1Month = hitrates1Month.FirstOrDefault(hr => hr?.PlayerTag == signup.PlayerTag);
                var htirate3Months = hitrates3Months.FirstOrDefault(hr => hr?.PlayerTag == signup.PlayerTag);
                data.Add(
                [
                    signup.PlayerName,
                    signup.PlayerTag,
                    signup.PlayerThLevel,
                    signup.MaxDefeneses ? "Yes" : "No",
                    hitrate1Month?.GetSuccessRate(),
                    hitrate1Month?.AttackCount,
                    hitrate1Month?.SuccessCount,
                    htirate3Months?.GetSuccessRate(),
                    signup.OptOutDays.HasFlag(OptOutDays.Day1) ? "" : 1,
                    signup.OptOutDays.HasFlag(OptOutDays.Day2) ? "" : 1,
                    signup.OptOutDays.HasFlag(OptOutDays.Day3) ? "" : 1,
                    signup.OptOutDays.HasFlag(OptOutDays.Day4) ? "" : 1,
                    signup.OptOutDays.HasFlag(OptOutDays.Day5) ? "" : 1,
                    signup.OptOutDays.HasFlag(OptOutDays.Day6) ? "" : 1,
                    signup.OptOutDays.HasFlag(OptOutDays.Day7) ? "" : 1,
                    null,
                    signup.Bonus ? "Yes" : "No",
                    signup.WarPreference.ToString(),
                ]);

            }

            return data.Select(d => d.ToArray()).ToArray();
        }

        private async Task<List<AttackSuccessModel?>> GetLastMonthHitrates(List<string> playerTags)
        {
            List<AttackSuccessModel?> resList = [];
            foreach (var playerTag in playerTags)
            {
                resList.Add(await GetHitrate(playerTag, 31));
            }
            return resList;
        }

        private async Task<List<AttackSuccessModel?>> GetThreeMonthsHitrates(List<string> playerTags)
        {
            List<AttackSuccessModel?> resList = [];
            foreach (var playerTag in playerTags)
            {
                resList.Add(await GetHitrate(playerTag, 90));
            }
            return resList;
        }

        private async Task<AttackSuccessModel?> GetHitrate(string playerTag, int numberDays)
        {
            var warAttacks = await _clashKingApiService.GetOrFetchPlayerWarhitsAsync(playerTag);

            if (warAttacks is null)
                return null;

            var attacks = warAttacks.Items
                .Where(w =>
                {
                    var endTime = DateTime.ParseExact(w.WarData.EndTime, "yyyyMMddTHHmmss.fffZ", null, System.Globalization.DateTimeStyles.RoundtripKind);
                    TimeSpan difference = DateTime.UtcNow - endTime;
                    return difference.TotalDays <= numberDays;
                })
                .SelectMany(i => i.Attacks.Where(a => a.Defender.TownhallLevel >= i.MemberData.TownhallLevel));

            return new AttackSuccessModel
            (
                playerName: warAttacks.Items.FirstOrDefault()?.MemberData.Name ?? "",
                playerTag: playerTag,
                playerTh: warAttacks.Items.FirstOrDefault()?.MemberData.TownhallLevel ?? 0,
                attackCount: attacks.Count(),
                successCount: attacks.Count(a => a.Stars >= 3)
            );
        }

        public async Task<Embed> RosterMissingDay(string clanTag)
        {
            try
            {
                var clan = await _clansClient.GetOrFetchClanAsync(clanTag);

                // Find the CWL war currently in preparation (the upcoming day).
                var group = await _clansClient.GetOrFetchLeagueGroupOrDefaultAsync(clanTag);
                if (group is null)
                    return _embedHelper.ErrorEmbed("Error", "This clan does not seem to be in an active CWL.");

                var wars = (await _clansClient.GetOrFetchLeagueWarsAsync(group))
                    .Where(w => w.Clans.ContainsKey(clanTag))
                    .OrderBy(w => w.StartTime)
                    .ToList();

                var prepIndex = wars.FindIndex(w => w.State == WarState.Preparation);
                if (prepIndex < 0)
                    return _embedHelper.ErrorEmbed("Error", "There is no CWL war currently in preparation for this clan.");

                var dayNumber = prepIndex + 1;
                var prepWar = wars[prepIndex];
                var warClan = prepWar.Clan.Tag == clanTag ? prepWar.Clan : prepWar.Opponent;
                var opponent = prepWar.Clan.Tag == clanTag ? prepWar.Opponent : prepWar.Clan;

                // Read the matching day column from the pinned roster.
                var pinned = _botDb.PinnedRosters.FirstOrDefault(p => p.ClanTag == clanTag);
                if (pinned is null || string.IsNullOrEmpty(pinned.SpreadsheetId))
                    return _embedHelper.ErrorEmbed("Error", "No pinned roster url for that clan.");

                var champStyle = _botDb.ClanSettings.FirstOrDefault(cs => cs.ClanTag == clanTag)?.ChampStyleCwlRoster ?? false;
                // Normal roster day columns are D-J (index 3-9); champ-style are I-O (index 8-14).
                var dayColumnIndex = (champStyle ? 8 : 3) + (dayNumber - 1);

                var sheetEntries = await _gspreadService.GetRosterDayOptIns(_gspreadService.GetUrl(pinned), dayColumnIndex);

                // Reconcile the sheet plan against the in-game lineup.
                var lineup = warClan.Members.Select(m => (m.Tag, m.Name));
                var (toOptIn, toOptOut) = ComputeDayRosterDiff(sheetEntries, lineup);

                var description = new StringBuilder();
                description.AppendLine($"**Opponent:** {opponent.Name}");
                description.AppendLine();

                if (toOptIn.Count == 0 && toOptOut.Count == 0)
                {
                    description.AppendLine($"✅ The in-game lineup already matches the roster for Day {dayNumber}.");
                }
                else
                {
                    description.AppendLine($"**✅ Opt in ({toOptIn.Count})** — in the roster for Day {dayNumber} but not in the war:");
                    description.AppendLine(toOptIn.Count == 0 ? "- none" : string.Join("\n", toOptIn.Select(p => $"- {p.Name} ({p.Tag})")));
                    description.AppendLine();
                    description.AppendLine($"**🚫 Opt out ({toOptOut.Count})** — in the war but not in the roster for Day {dayNumber}:");
                    description.AppendLine(toOptOut.Count == 0 ? "- none" : string.Join("\n", toOptOut.Select(p => $"- {p.Name} ({p.Tag})")));
                }

                return new EmbedBuilder()
                    .WithTitle($"Day {dayNumber} Roster Check - {clan.Name}")
                    .WithDescription(description.ToString())
                    .WithColor(Color.DarkPurple)
                    .Build();
            }
            catch (Exception ex)
            {
                return _embedHelper.ErrorEmbed("Error", ex.Message);
            }
        }

        // Reconciles the roster's day column (who should play) against the in-game war lineup.
        // Opt in = opted in on the sheet but not in the lineup; opt out = in the lineup but not opted in.
        internal static (List<(string Tag, string Name)> ToOptIn, List<(string Tag, string Name)> ToOptOut)
            ComputeDayRosterDiff(
                IEnumerable<(string Tag, string Name, bool OptedIn)> sheet,
                IEnumerable<(string Tag, string Name)> lineup)
        {
            var lineupList = lineup.ToList();
            var lineupTags = lineupList.Select(m => m.Tag).ToHashSet();
            var optedInTags = sheet.Where(e => e.OptedIn).Select(e => e.Tag).ToHashSet();

            var toOptIn = sheet
                .Where(e => e.OptedIn && !lineupTags.Contains(e.Tag))
                .Select(e => (e.Tag, e.Name))
                .ToList();

            var toOptOut = lineupList
                .Where(m => !optedInTags.Contains(m.Tag))
                .ToList();

            return (toOptIn, toOptOut);
        }
    }
}
