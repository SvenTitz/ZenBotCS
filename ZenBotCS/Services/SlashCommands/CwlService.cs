using CocApi.Cache;
using CocApi.Rest.Models;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using System.Text;
using ZenBotCS.Clients;
using ZenBotCS.Entities;
using ZenBotCS.Entities.Models;
using ZenBotCS.Entities.Models.Enums;
using ZenBotCS.Helper;
using ZenBotCS.Models;
using WarPreference = ZenBotCS.Entities.Models.Enums.WarPreference;

namespace ZenBotCS.Services.SlashCommands
{
    public class CwlService(
        CustomClansClient _clansClient,
        PlayersClient _playersClient,
        BotDataContext _botDb,
        EmbedHelper _embedHelper,
        GspreadService _gspreadService,
        IMemoryCache _cache,
        PlayerService _playerService,
        IConfiguration _config)
    {
        private static readonly string[] _cwlDataHeaders = ["Stars", "% Dest", "TH", "+/-", "Defence"];
        private static readonly string[] _cwlEmptyAttack = ["", "", "", "", "-"];
        private static readonly string[] _cwlMissedAttack = ["0", "0", "", "", "-"];
        private static readonly EmbedFieldBuilder _cwlDataInstructionField = new EmbedFieldBuilder()
            .WithName("Instructions:")
            .WithValue("1. Open the spreadsheet above and copy all lines containing player data (everything except the first two).\n" +
                        "2. Open the Family CWL Data Spreadsheet and select the first cell for player data for the respective clan.\n" +
                        "3. Paste values only. This can either be done with Ctrl+Shift+V or Rightclick -> Paste Special -> Value only.\n\n" +
                        "(on some browsers you might need the [Google Docs Offline](https://chrome.google.com/webstore/detail/google-docs-offline/ghbmnnjooekpmoecnnnilnnbdlolhkhi/related) extension to copy/paste from one sheet to another)");


        public (Embed[], MessageComponent) SignupPost()
        {
            var button = new ButtonBuilder()
                .WithLabel("Sign Up")
                .WithCustomId("button_cwl_signup_create")
                .WithStyle(ButtonStyle.Success);
            var component = new ComponentBuilder()
                .WithButton(button)
                .Build();


            Embed[] embeds = [
                new EmbedBuilder()
                    .WithImageUrl("https://cdn.discordapp.com/attachments/1126771582396805221/1219055335818530846/Zen-CWL-Signups.jpg?ex=6609e8c1&is=65f773c1&hm=3a5d778920b18cbb9f3be99d60d9da5e699fcb35d9455ece7b100601617b449e&")
                    .Build(),
                new EmbedBuilder()
                    .WithTitle("CWL Signups")
                    .WithDescription("Click the button below to sign up for the next upcoming CWL.          ")
                    .Build(),
            ];

            return (embeds, component);
        }

        public async Task<(string, MessageComponent)> CreateCwlSignupAccountSelection(SocketUser user)
        {

            var playerTags = _botDb.DiscordLinks.Where(dl => dl.DiscordId == user.Id).Select(dl => dl.PlayerTag);

            if (!playerTags.Any())
            {
                var errorMessage = "No clash accounts linked to your user. Please link them with ClashPerks oder ClashKing.";
                return await Task.FromResult((errorMessage, new ComponentBuilder().Build()));
            }

            var players = (await _playersClient.GetCachedPlayersAsync(playerTags)).Select(cp => cp.Content);
            players = players.OrderByDescending(p => p?.TownHallLevel).ThenBy(p => p?.Name);

            var menuBuilder = new SelectMenuBuilder()
                .WithPlaceholder("Select your account")
                .WithCustomId("menu_cwl_signup_account")
                .WithMinValues(1)
                .WithMaxValues(1);

            foreach (var player in players)
            {
                if (player is null)
                    continue;

                menuBuilder.AddOption(
                    player.Name,
                    player.Tag,
                    $"TH: {player.TownHallLevel}, Clan: {player.Clan?.Name}, Tag: {player.Tag}",
                    BotEmotes.GetThEmote(player.TownHallLevel));
            }

            var components = new ComponentBuilder()
                .WithSelectMenu(menuBuilder)
                .Build();

            var message = "Select your account you want to sign up.";
            return (message, components);
        }

        public bool CheckAlreadyRegistered(SocketMessageComponent interaction)
        {
            string playerTag = interaction.Data.Values.First();
            return _botDb.CwlSignups.Any(s => !s.Archieved && s.PlayerTag == playerTag);
        }

        public async Task<(string, MessageComponent)> CreateCwlSignupClanSelection()
        {
            var clans = await _clansClient.GetCachedClansAsync();
            clans = [.. clans.OrderBy(c => c.Name)];

            var menuBuilder = new SelectMenuBuilder()
                .WithPlaceholder("Select the clan")
                .WithCustomId("menu_cwl_signup_clan")
                .WithMinValues(1)
                .WithMaxValues(1);

            foreach (var clan in clans)
            {
                if (clan is null)
                    continue;

                menuBuilder.AddOption(clan.Name, clan.Tag);
            }

            var components = new ComponentBuilder()
                .WithSelectMenu(menuBuilder)
                .Build();

            var message = "Please select the clan you plan on participating CWL in.";
            return (message, components);
        }

        public (string, MessageComponent) CreateCwlSignupOptOutSelection()
        {
            var menuBuilder = new SelectMenuBuilder()
                .WithPlaceholder("Select Opt Out Days")
                .WithCustomId("menu_cwl_signup_optout")
                .WithMinValues(1)
                .WithMaxValues(7)
                .AddOption("I am able to participate on ALL days", ((int)OptOutDays.None).ToString())
                .AddOption("Day 1", ((int)OptOutDays.Day1).ToString())
                .AddOption("Day 2", ((int)OptOutDays.Day2).ToString())
                .AddOption("Day 3", ((int)OptOutDays.Day3).ToString())
                .AddOption("Day 4", ((int)OptOutDays.Day4).ToString())
                .AddOption("Day 5", ((int)OptOutDays.Day5).ToString())
                .AddOption("Day 6", ((int)OptOutDays.Day6).ToString())
                .AddOption("Day 7", ((int)OptOutDays.Day7).ToString());

            var components = new ComponentBuilder()
                .WithSelectMenu(menuBuilder)
                .Build();

            var message = "Please select the days on which you are **__NOT__** able to participate in CWL. \nKeep in mind that the first battle days usually stats on the second of each month.";
            return (message, components);
        }

        public (string, MessageComponent) CreateCwlSignupStyleSelection()
        {
            var menuBuilder = new SelectMenuBuilder()
                .WithPlaceholder("Select your preference")
                .WithCustomId("menu_cwl_signup_style")
                .WithMinValues(1)
                .WithMaxValues(1)
                .AddOption("FWA Clans - Click this option", ((int)WarPreference.FWA).ToString())
                .AddOption("Alternate - I don't mind sitting out", ((int)WarPreference.Alternate).ToString())
                .AddOption("I just want my 8 stars for max rewards", ((int)WarPreference.EightStars).ToString())
                .AddOption("I want to war as much as possible", ((int)WarPreference.Always).ToString());

            var components = new ComponentBuilder()
                .WithSelectMenu(menuBuilder)
                .Build();

            var message = "Please select your war preference.";
            return (message, components);
        }

        public (string, MessageComponent) CreateCwlSignupGeneralSelection()
        {
            var menuBuilder = new SelectMenuBuilder()
                .WithPlaceholder("Select your answer")
                .WithCustomId("menu_cwl_signup_general")
                .WithMinValues(1)
                .WithMaxValues(1)
                .AddOption("Yes", "true")
                .AddOption("No", "false");

            var components = new ComponentBuilder()
                .WithSelectMenu(menuBuilder)
                .Build();

            var message = "__Optional__: Do you want to help leadership during CWL with coordinating attacks or setting notes?";
            return (message, components);
        }

        public (string, MessageComponent) CreateCwlSignupBonusSelection()
        {
            var menuBuilder = new SelectMenuBuilder()
                .WithPlaceholder("Select your answer")
                .WithCustomId("menu_cwl_signup_bonus")
                .WithMinValues(1)
                .WithMaxValues(1)
                .AddOption("Yes", "true")
                .AddOption("No", "false");

            var components = new ComponentBuilder()
                .WithSelectMenu(menuBuilder)
                .Build();

            var message = "Would you like to be considered for bonus medals?";
            return (message, components);
        }

        public async Task HandleSignupError(SocketMessageComponent interaction)
        {
            var embed = _embedHelper.ErrorEmbed("Error", "Something went wrong during signup. Please try again.");
            await interaction.ModifyOriginalResponseAsync(x =>
            {
                x.Content = null;
                x.Components = null;
                x.Embed = embed;
            });

        }

        public async Task<bool> TryCacheSigupDetails(SocketMessageComponent interaction)
        {
            var messageId = interaction.Message.Id;
            string playerTag = interaction.Data.Values.First();
            var player = await _playersClient.GetOrFetchPlayerAsync(playerTag);

            if (player is null)
                return false;

            var data = new CwlSignup
            {
                DiscordId = interaction.User.Id,
                PlayerTag = player.Tag,
                PlayerName = player.Name,
                PlayerThLevel = player.TownHallLevel
            };

            _cache.Set(messageId, data, Options.MemoryCacheEntryOptions);
            return true;
        }

        public bool TryUpdateCachedSignupClan(SocketMessageComponent interaction)
        {
            var messageId = interaction.Message.Id;
            var clanTag = interaction.Data.Values.First();

            if (!_cache.TryGetValue(messageId, out CwlSignup? data)
                || data is null
                || string.IsNullOrEmpty(clanTag))
            {
                return false;
            }

            data.ClanTag = clanTag;
            _cache.Set(messageId, data, Options.MemoryCacheEntryOptions);
            return true;
        }

        public bool TryUpdateCachedSignupOptOuts(SocketMessageComponent interaction)
        {
            var messageId = interaction.Message.Id;
            var optOutDays = ConvertOptOutDataToEnum(interaction.Data.Values);

            if (!_cache.TryGetValue(messageId, out CwlSignup? data)
                || data is null)
            {
                return false;
            }

            data.OptOutDays = optOutDays;
            _cache.Set(messageId, data, Options.MemoryCacheEntryOptions);
            return true;
        }

        public bool TryUpdateCachedSignupStyle(SocketMessageComponent interaction)
        {
            var messageId = interaction.Message.Id;
            var styleIdString = interaction.Data.Values.First();

            if (!int.TryParse(styleIdString, out int styleId))
            {
                return false;
            }
            var style = (WarPreference)styleId;

            if (!_cache.TryGetValue(messageId, out CwlSignup? data)
            || data is null)
            {
                return false;
            }

            data.WarPreference = style;
            _cache.Set(messageId, data, Options.MemoryCacheEntryOptions);
            return true;
        }

        public bool TryUpdateCachedSignupBonus(SocketMessageComponent interaction)
        {
            var messageId = interaction.Message.Id;
            var bonus = interaction.Data.Values.First() == "true";

            if (!_cache.TryGetValue(messageId, out CwlSignup? data)
                || data is null)
            {
                return false;
            }

            data.Bonus = bonus;
            _cache.Set(messageId, data, Options.MemoryCacheEntryOptions);
            return true;
        }

        public bool TryUpdateCachedSignupGeneral(SocketMessageComponent interaction)
        {
            var messageId = interaction.Message.Id;
            var general = interaction.Data.Values.First() == "true";

            if (!_cache.TryGetValue(messageId, out CwlSignup? data)
                || data is null)
            {
                return false;
            }

            data.WarGeneral = general;
            _cache.Set(messageId, data, Options.MemoryCacheEntryOptions);
            return true;
        }

        public async Task<bool> SaveSignupToDb(SocketMessageComponent interaction)
        {
            var signup = GetSignup(interaction);

            if (signup is null)
                return false;

            _botDb.Add(signup);
            await _botDb.SaveChangesAsync();
            return true;
        }

        public CwlSignup? GetSignup(SocketMessageComponent interaction)
        {
            return _cache.Get(interaction.Message.Id) as CwlSignup;
        }

        public bool CheckValidOptOuts(SocketMessageComponent interaction)
        {
            var values = interaction.Data.Values;
            return values.Count == 1 || !values.Contains(((int)OptOutDays.None).ToString());
        }

        private OptOutDays ConvertOptOutDataToEnum(IEnumerable<string> values)
        {
            OptOutDays result = OptOutDays.None;
            foreach (string numString in values)
            {
                if (int.TryParse(numString, out int num))
                {
                    OptOutDays enumValue = (OptOutDays)num;
                    if (Enum.IsDefined(typeof(OptOutDays), enumValue))
                    {
                        result |= enumValue;
                    }
                }
            }
            return result;
        }

        public async Task<Embed> SignupRoster(string clanTag)
        {
            var clan = await _clansClient.GetOrFetchClanAsync(clanTag);
            var signups = _botDb.CwlSignups.Where(s => s.ClanTag == clanTag && !s.Archieved).ToList();
            var data = FormatDataForRosterSpreadsheet(signups);
            var url = await _gspreadService.WriteCwlRosterData(data, clan);

            return new EmbedBuilder()
                .WithDescription(url)
                .WithColor(Color.DarkPurple)
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

        public async Task<Embed> Data(string clantag, string? spreadsheetId)
        {
            try
            {
                var group = await _clansClient.GetOrFetchLeagueGroupOrDefaultAsync(clantag);
                var clan = group?.Clans.FirstOrDefault(c => c.Tag == clantag);

                if (group is null || clan is null)
                {
                    return _embedHelper.ErrorEmbed("Error", "Clan does not seem to be in an active CWL.");
                }

                var memberModels = await ExtractCwlDataMemberModelsAsync(group!, clan.Tag);

                var data = FormatDataForDataSpreadsheet(memberModels, clan);

                var url = _gspreadService.WriteCwlData(data, clan.Name, spreadsheetId);

                var urlField = new EmbedFieldBuilder()
                    .WithName("Sheet:")
                    .WithValue(url);

                return new EmbedBuilder()
                            .WithTitle($"__CWL Data {clan.Name}__")
                            .WithColor(Color.DarkPurple)
                            .AddField(urlField)
                            .AddField(_cwlDataInstructionField)
                            .Build();
            }
            catch (Exception ex)
            {
                return _embedHelper.ErrorEmbed("Error", ex.Message);
            }

        }

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
                        .WithName($"**{signup.PlayerName}** in **{signupClan?.Name}**")
                        .WithValue($"OptOut Days: {GetOptOutDaysString(signup.OptOutDays)}\n" +
                        $"War Preference: {signup.WarPreference}, Bonus?: {signup.Bonus}, WarGeneral?: {signup.WarGeneral}\n" +
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
            var signups = _botDb.CwlSignups.Where(s => s.PlayerTag == playerTag);
            if (signups.Any())
            {
                _botDb.CwlSignups.RemoveRange(signups);
                await _botDb.SaveChangesAsync();
            }
        }


        public async Task<string> RolesAsign(SocketInteractionContext context)
        {
            try
            {
                var groupedSignups = (await _botDb.CwlSignups.Where(s => !s.Archieved).ToListAsync()).GroupBy(s => s.DiscordId);
                var clanOptions = _config.GetRequiredSection(ClanOptionsList.String).Get<ClanOptionsList>()?.ClanOptions;

                ulong warGeneralRoleId = _config.GetValue<ulong>("WarGeneralRoleId");

                foreach (var userSignups in groupedSignups)
                {
                    var clanTags = userSignups.Select(us => us.ClanTag).ToHashSet();
                    HashSet<ulong> roleIds = [];
                    foreach (var clanTag in clanTags)
                    {
                        var clanOption = clanOptions?.FirstOrDefault(o => o.ClanTag == clanTag);

                        if (clanOption is not null
                            && clanOption.CwlRoleId > 0)
                        {
                            roleIds.Add(clanOption.CwlRoleId);
                        }
                    }
                    if (userSignups.Any(s => s.WarGeneral))
                    {
                        roleIds.Add(warGeneralRoleId);
                    }

                    if (roleIds.Count != 0)
                    {
                        var user = context.Guild.GetUser(userSignups.Key);
                        await user.AddRolesAsync(roleIds);
                    }
                }

                return $"Added {groupedSignups.SelectMany(s => s).Count()} roles to {groupedSignups.Count()} users.";

            }
            catch (Exception e)
            {
                return $"**Error**: {e.Message}";
            }
        }

        public async Task<string> RolesRemove(SocketInteractionContext context)
        {
            try
            {
                var clanOptions = _config.GetRequiredSection(ClanOptionsList.String).Get<ClanOptionsList>()?.ClanOptions;
                var roleIds = clanOptions!.Where(o => o.CwlRoleId > 0).Select(o => o.CwlRoleId).ToList();
                ulong warGeneralRoleId = _config.GetValue<ulong>("WarGeneralRoleId");
                roleIds.Add(warGeneralRoleId);

                foreach (var user in context.Guild.Users.Where(u => u.Roles.Any(r => roleIds.Contains(r.Id))))
                {
                    await user.RemoveRolesAsync(roleIds);
                }

                return "Done removing roles";
            }
            catch (Exception e)
            {
                return $"**Error**: {e.Message}";
            }
        }

        public int GetCurrentSignupCount()
        {
            return _botDb.CwlSignups.Count();
        }

        public async Task SignupsReset()
        {
            await _botDb.CwlSignups.Where(s => !s.Archieved).ForEachAsync(s => s.Archieved = true);
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


        private object[][] FormatDataForDataSpreadsheet(List<CwlDataMemberModel> memberModels, ClanWarLeagueClan clan)
        {
            var data = new List<List<object>>();
            var days = new List<object> { "", "" };
            var headers = new List<object> { "Players", "TH" };
            for (int i = 0; i < 7; i++)
            {
                days.AddRange(new[] { $"Day {i + 1}", "", "", "", "" });
                headers.AddRange(_cwlDataHeaders);
            }
            data.Add(days);
            data.Add(headers);

            foreach (var member in memberModels)
            {
                var memberRow = new List<object>
                {
                    member.Member.Name,
                    member.Member.TownhallLevel
                };
                for (int i = 0; i < 7; i++)
                {
                    if (member.Attacks[i] is null)
                    {
                        memberRow.AddRange(_cwlEmptyAttack);
                    }
                    else if (member.Attacks[i]!.isMissedAttack)
                    {
                        memberRow.AddRange(_cwlMissedAttack);
                    }
                    else
                    {
                        memberRow.AddRange(new[]
                            {
                                member.Attacks[i]!.Stars.ToString(),
                                member.Attacks[i]!.DestructionPercentage.ToString(),
                                member.Attacks[i]!.DefenderTownHall.ToString(),
                                (member.Attacks[i]!.DefenderTownHall - member.Attacks[i]!.AttackerTownHall).ToString(),
                                "-"
                            });
                    }
                }
                data.Add(memberRow);
            }

            return data.Select(d => d.ToArray()).ToArray();
        }

        private async Task<List<CwlDataMemberModel>> ExtractCwlDataMemberModelsAsync(ClanWarLeagueGroup group, string clantag)
        {
            var allWars = await _clansClient.GetOrFetchLeagueWarsAsync(group);
            var wars = allWars.Where(w => w.Clans.ContainsKey(clantag)).OrderBy(c => c.StartTime).ToList();

            List<CwlDataMemberModel> memberModels = new();
            foreach (var war in wars)
            {
                int index = wars.IndexOf(war);
                var clan = war.Clans[clantag];
                foreach (var member in clan.Members)
                {
                    var model = GetOrAddCwlDataMemberModel(memberModels, member);
                    if (member.Attacks is not null && member.Attacks.Count > 0)
                    {
                        model.Attacks[index] = new CwlDataMemberAttack(member.Attacks[0]);
                    }
                    else
                    {
                        model.Attacks[index] = new CwlDataMemberAttack { isMissedAttack = true };
                    }
                }
            }
            return memberModels;
        }

        private CwlDataMemberModel GetOrAddCwlDataMemberModel(List<CwlDataMemberModel> members, ClanWarMember member)
        {
            var model = members.FirstOrDefault(p => p.Member.Tag == member.Tag);
            if (model is null)
            {
                model = new CwlDataMemberModel(member);
                members.Add(model);
            }
            return model;
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
