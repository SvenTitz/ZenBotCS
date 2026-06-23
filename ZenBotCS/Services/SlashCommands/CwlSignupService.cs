using System.Text;
using CocApi.Cache;
using CocApi.Rest.Models;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using ZenBotCS.Clients;
using ZenBotCS.Entities;
using ZenBotCS.Entities.Models;
using ZenBotCS.Entities.Models.Enums;
using ZenBotCS.Extensions;
using ZenBotCS.Helper;
using WarPreference = ZenBotCS.Entities.Models.Enums.WarPreference;

namespace ZenBotCS.Services.SlashCommands
{
    public class CwlSignupService(
        CustomClansClient _clansClient,
        BotDataContext _botDb,
        EmbedHelper _embedHelper,
        GspreadService _gspreadService,
        PlayersClient _playersClient,
        PlayerService _playerService,
        ClashKingApiClient _clashKingApiClient)
    {
        public async Task<Embed> SignupSummaryAllClans()
        {
            var description = new StringBuilder();

            var clans = await _clansClient.GetCachedClansAsync();

            foreach (var clan in clans)
            {
                var clanSignups = _botDb.CwlSignups.Where(s => s.ClanTag == clan.Tag && !s.Archieved);

                if (!clanSignups.Any())
                    continue;

                description.AppendLine($"**{clan.Name}** ({clan.Tag}):");

                var countSignups = clanSignups.Count();
                var prefCountStrings = (from WarPreference pref in Enum.GetValues(typeof(WarPreference))
                                        let count = clanSignups.Count(s => s.WarPreference == pref)
                                        where count > 0
                                        select $"{pref}: {count}").ToList();

                description.AppendLine($"> Signups: {countSignups}");
                description.Append("> ");
                description.Append(string.Join(", ", prefCountStrings) + "\n\n");
            }

            return new EmbedBuilder()
                .WithTitle("CWL Signup Summary")
                .WithDescription(description.ToString())
                .WithColor(Color.DarkPurple)
                .Build();
        }

        public async Task<Embed> SignupSummaryClan(string clantag)
        {
            var description = new StringBuilder();

            var clan = await _clansClient.GetOrFetchClanAsync(clantag);

            var signups = _botDb.CwlSignups.Where(s => s.ClanTag == clantag && !s.Archieved);
            var singnupNames = signups.Select(s => s.PlayerName).ToList();
            singnupNames = singnupNames.Order().ToList();
            description.Append("Signups: ");
            description.AppendLine(string.Join(", ", singnupNames));
            description.AppendLine();

            description.AppendLine($"Sum: {signups.Count()}");

            var prefCountStrings = (from WarPreference pref in Enum.GetValues(typeof(WarPreference))
                                    let count = signups.Count(s => s.WarPreference == pref)
                                    where count > 0
                                    select $"{pref}: {count}").ToList();
            description.AppendLine(string.Join(", ", prefCountStrings));

            return new EmbedBuilder()
                .WithTitle($"**{clan.Name}** CWL Signup Summary")
                .WithDescription(description.ToString())
                .WithColor(Color.DarkPurple)
                .Build();
        }

        public async Task<Embed> SignupCheck(string? playerTag, SocketUser? user)
        {
            if (playerTag is null && user is null)
            {
                return _embedHelper.ErrorEmbed("Error", "You need to provide either a User or Playertag.");
            }

            try
            {
                var players = await _playerService.GetPlayersFromTagAndUser(playerTag, user);
                var playerTags = players.Select(x => x.Tag).ToList();

                var clans = await _clansClient.GetCachedClansAsync();

                var signups = _botDb.CwlSignups.Where(s => !s.Archieved && playerTags.Contains(s.PlayerTag)).ToList();
                var embedBuilder = new EmbedBuilder();
                foreach (var signup in signups)
                {
                    var signupClan = clans.FirstOrDefault(c => c.Tag == signup.ClanTag);
                    var timestamp = (long)(signup.UpdatedAt.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                    var fieldBuilder = new EmbedFieldBuilder()
                        .WithName($"**{signup.PlayerName}{_embedHelper.ToSuperscript(signup.PlayerThLevel)}** in **{signupClan?.Name}**")
                        .WithValue($"OptOut Days: {GetOptOutDaysString(signup.OptOutDays)}\n" +
                        $"War Preference: {signup.WarPreference}, Bonus?: {signup.Bonus}\n" +
                        $"Timestamp: <t:{timestamp}:f>")
                        .WithIsInline(false);
                    embedBuilder.AddField(fieldBuilder);
                }

                return embedBuilder
                    .WithTitle("Cwl Signup Check")
                    .WithColor(Color.DarkPurple)
                    .Build();
            }
            catch (Exception ex)
            {
                return _embedHelper.ErrorEmbed("Error", ex.Message);
            }
        }

        public async Task SignupDelete(string playerTag)
        {
            var signups = _botDb.CwlSignups.Where(s => s.PlayerTag == playerTag && !s.Archieved);
            if (signups.Any())
            {
                _botDb.CwlSignups.RemoveRange(signups);
                await _botDb.SaveChangesAsync();
            }
        }

        public async Task<Embed> SingupMissing(string clantag)
        {
            var spreadsheetId = _botDb.PinnedRosters.FirstOrDefault(x => x.ClanTag == clantag)?.SpreadsheetId;
            if (spreadsheetId == null)
            {
                return _embedHelper.ErrorEmbed("Error", "No pinned roster url for that clan.");
            }

            var spreadsheetUrl = _gspreadService.GetUrl(_botDb.PinnedRosters.FirstOrDefault(x => x.ClanTag == clantag)!);
            var rosterPlayerTags = await _gspreadService.GetPlayerTags(spreadsheetUrl);
            var clan = await _clansClient.GetOrFetchClanAsync(clantag);
            var clanPlayerTags = clan.Members.Select(m => m.Tag);

            var missingPlayerTags = rosterPlayerTags.Where(tag => !clanPlayerTags.Contains(tag));
            var missingPlayersTasks = missingPlayerTags.Select(async tag => await _playersClient.GetOrFetchPlayerAsync(tag));
            var missingPlayers = await Task.WhenAll(missingPlayersTasks);
            var extraPlayers = clan.Members.Where(m => !rosterPlayerTags.Contains(m.Tag));

            var description = new StringBuilder();
            description.AppendLine("**Missing Players from Roster:**");
            foreach (var player in missingPlayers)
            {
                description.AppendLine($"- **{player.Name}{_embedHelper.ToSuperscript(player.TownHallLevel)}** ({player.Tag})");
            }

            description.AppendLine("\n\n**Exrta Players not in Roster:**");
            foreach (var player in extraPlayers)
            {
                description.AppendLine($"- **{player.Name}{_embedHelper.ToSuperscript(player.TownHallLevel ?? 0)}** ({player.Tag})");
            }

            return new EmbedBuilder()
                .WithTitle($"CWL Roster Check {clan.Name}")
                .WithDescription(description.ToString())
                .WithColor(Color.DarkPurple)
                .Build();
        }


        public int GetCurrentSignupCount()
        {
            return _botDb.CwlSignups.Where(s => !s.Archieved).Count();
        }

        public async Task SignupsReset()
        {
            await _botDb.CwlSignups.Where(s => !s.Archieved).ForEachAsync(s => s.Archieved = true);
            _botDb.PinnedRosters.RemoveRange(_botDb.PinnedRosters);
            await _botDb.SaveChangesAsync();
        }

        public (Embed, MessageComponent) SignupArchiveConfirmDialog()
        {
            var embed = new EmbedBuilder()
                .WithTitle("Are you sure?")
                .WithDescription("This will archive all current CWL signups. They will no longer be shown to the user or used for creating the roster. This should usually be done once, right before the signups for a new season start.")
                .WithColor(Color.DarkPurple)
                .Build();

            var confirmButton = new ButtonBuilder()
                .WithLabel("Confirm")
                .WithCustomId("button_cwl_signup_reset_confirm")
                .WithStyle(ButtonStyle.Success);

            var cancelButton = new ButtonBuilder()
                .WithLabel("Cancel")
                .WithCustomId("button_cwl_signup_reset_cancel")
                .WithStyle(ButtonStyle.Danger);

            var comps = new ComponentBuilder()
                .WithButton(confirmButton)
                .WithButton(cancelButton)
                .Build();

            return (embed, comps);
        }

        public async Task<Embed> SignupDump(bool includeArchive)
        {
            try
            {
                var data = await ConvertDbSignupsToObjectArray(includeArchive);

                var url = _gspreadService.CreateDbDumpSpreadsheet("CwlSignup Dump", data!);

                return new EmbedBuilder()
                .WithDescription(url)
                .WithColor(Color.DarkPurple)
                .Build();
            }
            catch (Exception ex)
            {
                return _embedHelper.ErrorEmbed("Error", ex.Message);
            }
        }

        public async Task<Embed> SignupAdd(string playerTag, string clanTag, bool? bonus, WarPreference? warPreference)
        {
            Player player;
            Clan clan;
            try
            {
                player = await _playersClient.GetOrFetchPlayerAsync(playerTag);
                clan = await _clansClient.GetOrFetchClanAsync(clanTag);
            }
            catch (Exception e)
            {
                return _embedHelper.ErrorEmbed("Error", e.Message);
            }
            if (clan is null)
            {
                return _embedHelper.ErrorEmbed("Error", $"Invalid Clan Tag `{clanTag}`.");
            }

            if (player is null)
            {
                return _embedHelper.ErrorEmbed("Error", $"Invalid Player Tag `{playerTag}`");
            }

            var discordUserId = await _clashKingApiClient.PostDiscordLinksAsync(playerTag);
            if (discordUserId is null)
            {
                return _embedHelper.ErrorEmbed("Error", $"{player.Name} not linked to a Discord user.");
            }

            var existingSignup = _botDb.CwlSignups.FirstOrDefault(s => s.PlayerTag == playerTag && !s.Archieved);
            if (existingSignup is not null)
            {
                return _embedHelper.ErrorEmbed("Error", $"{player.Name} is already signed up in {clan.Name}");
            }

            var signup = new CwlSignup()
            {
                PlayerTag = player.Tag,
                PlayerName = player.Name,
                PlayerThLevel = player.TownHallLevel,
                ClanTag = clan.Tag,
                DiscordId = discordUserId.Value,
                OptOutDays = 0,
                WarPreference = warPreference ?? WarPreference.Alternate,
                Bonus = bonus ?? false
            };
            _botDb.CwlSignups.Add(signup);
            _botDb.SaveChanges();

            return new EmbedBuilder()
                .WithTitle("Cwl Signup Added")
                .WithDescription($"Signed up {player.Name} ({player.Tag}) in {clan.Name}")
                .WithColor(Color.Purple)
                .Build();
        }

        private string GetOptOutDaysString(OptOutDays optOutDays)
        {
            if (optOutDays is OptOutDays.None)
                return "None";

            List<string> dayStrings = [];
            foreach (OptOutDays day in Enum.GetValues(typeof(OptOutDays)))
            {
                if (day is OptOutDays.None)
                    continue;

                if (optOutDays.HasFlag(day))
                {
                    dayStrings.Add(day.ToString() ?? "");
                }
            }
            return string.Join(", ", dayStrings);
        }

        private async Task<object?[][]> ConvertDbSignupsToObjectArray(bool includeArchived)
        {
            // Retrieve column headers from DbSet
            var columnHeaders = _botDb.Model.FindEntityType(typeof(CwlSignup))?
                .GetProperties()
                .Select(p => p.Name)
                .ToArray();

            if (columnHeaders is null)
                return [];

            // Retrieve data from DbSet
            var signups = _botDb.CwlSignups.AsQueryable();
            if (!includeArchived)
            {
                signups = signups.Where(s => !s.Archieved);
            }
            var data = await signups.ToArrayAsync();

            // Populate object[][] with column headers in the first row and data in subsequent rows
            var result = new object?[data.Length + 1][];
            result[0] = columnHeaders;
            for (int i = 0; i < data.Length; i++)
            {
                result[i + 1] = columnHeaders.Select(header => typeof(CwlSignup).GetProperty(header)?.GetValue(data[i])).ToArray();
            }

            return result;
        }
    }
}
