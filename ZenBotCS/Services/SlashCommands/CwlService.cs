using CocApi.Cache;
using CocApi.Rest.Models;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using ZenBotCS.Clients;
using ZenBotCS.Entities;
using ZenBotCS.Entities.Models;
using ZenBotCS.Entities.Models.Enums;
using ZenBotCS.Helper;
using ZenBotCS.Models;
using ZenBotCS.Modules;
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
        IConfiguration _config,
        ILogger<CwlService> _logger)
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
            var signupButton = new ButtonBuilder()
                .WithLabel("Sign Up")
                .WithCustomId("button_cwl_signup_create")
                .WithStyle(ButtonStyle.Success);
            var closeButton = new ButtonBuilder()
                .WithLabel("Close Signup")
                .WithCustomId("button_cwl_signup_close")
                .WithStyle(ButtonStyle.Danger);
            var component = new ComponentBuilder()
                .WithButton(signupButton)
                .WithButton(closeButton)
                .Build();


            Embed[] embeds = [
                new EmbedBuilder()
                    .WithImageUrl("https://cdn.discordapp.com/attachments/1126771582396805221/1221539143473958912/Zen-CWL-Signups.jpg?ex=6612f1fa&is=66007cfa&hm=cac106677455f60967b6ef5ea4f4fa032d41d56400953f950b8e268352988653&")
                    .Build(),
                new EmbedBuilder()
                    .WithTitle("CWL Signups")
                    .WithDescription("Click the button below to sign up for the next upcoming CWL.\n\n" +
                        "- Your account needs to be linked to discord and it needs to be in a family clan.\n" +
                        "- There is no \"back\" button, but you can just dismiss the message and start over.\n" +
                        "- You can check your signup with </cwl signup check:1194250784695660605>\n" +
                        "- If you need help ping leadership.")
                    .WithImageUrl("https://cdn.discordapp.com/attachments/809874883768614922/1231630801792405704/Zen-CWL-Spacer.png?ex=6629d0d1&is=66287f51&hm=3372eb6161b41bb81bc6d89e02049e8e6ea1bc2126abb3f7bd8079306207b7c9&")
                    .Build(),
            ];

            return (embeds, component);
        }

        public (string?, Embed[], MessageComponent) HandleCwlSignupClose()
        {
            var signupButton = new ButtonBuilder()
                .WithLabel("Sign Up")
                .WithCustomId("button_cwl_signup_create")
                .WithStyle(ButtonStyle.Success)
                .WithDisabled(true);
            var reopenButton = new ButtonBuilder()
                .WithLabel("Reopen Signup")
                .WithCustomId("button_cwl_signup_reopen")
                .WithStyle(ButtonStyle.Danger);
            var component = new ComponentBuilder()
                .WithButton(signupButton)
                .WithButton(reopenButton)
                .Build();

            Embed[] embeds = [
                new EmbedBuilder()
                    .WithImageUrl("https://cdn.discordapp.com/attachments/1126771582396805221/1221539143473958912/Zen-CWL-Signups.jpg?ex=6612f1fa&is=66007cfa&hm=cac106677455f60967b6ef5ea4f4fa032d41d56400953f950b8e268352988653&")
                    .Build(),
                new EmbedBuilder()
                    .WithTitle("CWL Signups")
                    .WithDescription("~~Click the button below to sign up for the next upcoming CWL.~~\n\n**CWL Signup has been closed.**")
                    .Build(),
            ];

            return (null, embeds, component);
        }

        public (string?, Embed[], MessageComponent) HandleCwlSignupReopen()
        {
            (var embeds, var component) = SignupPost();
            return (null, embeds, component);
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

        public async Task<bool> CheckCorrectClan(SocketMessageComponent interaction)
        {
            try
            {
                var signup = GetSignupFromCache(interaction);
                var player = await _playersClient.GetOrFetchPlayerAsync(signup!.PlayerTag);
                var selectedClanTag = interaction.Data.Values.First();
                return player.Clan?.Tag == selectedClanTag;
            }
            catch
            {
                await HandleInteractionError(interaction);
                return true;
            }
        }

        public async Task<(string, MessageComponent)> CreateCwlSignupClanSelection()
        {
            var clans = await _clansClient.GetCachedClansAsync();
            var clanOptions = _config.GetRequiredSection(ClanOptionsList.String).Get<ClanOptionsList>()?.ClanOptions;
            clans = clans.Where(c => !clanOptions?.FirstOrDefault(o => o.ClanTag == c.Tag)?.DisableCwlSignup ?? false).ToList();

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

            var message = "Please select the family clan you are in.";
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


        public async Task<(string, MessageComponent)> CreateCwlSignupMove(string clantagFrom, string clantagTo, ulong messageId)
        {
            var clanFrom = await _clansClient.GetOrFetchClanAsync(clantagFrom);
            var clanTo = await _clansClient.GetOrFetchClanAsync(clantagTo);
            var signups = _botDb.CwlSignups.Where(s => s.ClanTag == clantagFrom && !s.Archieved);
            var players = (await _playersClient.GetCachedPlayersAsync(signups.Select(s => s.PlayerTag))).Select(cp => cp.Content).ToList();
            var sortedPlayers = players.Where(p => p is not null).Cast<Player>().OrderBy(p => p.Name).ToList();

            if (players is null || players.Count() == 0)
            {
                return ("Something went wrong. Please try again", new ComponentBuilder().Build());
            }

            var singupMoveContext = new SignupMoveContext
            {
                ClanFrom = clanFrom,
                ClanTo = clanTo,
                Players = sortedPlayers,
                PageCount = 0
            };

            _cache.Set(messageId, singupMoveContext, Options.MemoryCacheEntryOptions);

            return UpdateCwlSignupMoveSelectionNextPage(messageId);
        }

        public (string, MessageComponent) UpdateCwlSignupMoveSelectionNextPage(ulong messageId)
        {
            if (!_cache.TryGetValue(messageId, out SignupMoveContext? context) || context is null)
            {
                return ("Something went wrong. Please try again", new ComponentBuilder().Build());
            }

            context.PageCount++;

            var menuBuilder = new SelectMenuBuilder()
                .WithPlaceholder("Select the members to move")
                .WithCustomId("menu_cwl_signup_move")
                .AddOption("None from this page.", "none");

            foreach (var player in context!.Players.Skip((context.PageCount - 1) * 24).Take(24))
            {
                if (player is null)
                    continue;

                menuBuilder.AddOption(
                    player.Name,
                    player.Tag,
                    $"TH: {player.TownHallLevel}, Clan: {player.Clan?.Name}, Tag: {player.Tag}",
                    BotEmotes.GetThEmote(player.TownHallLevel));
            }

            menuBuilder
                .WithMinValues(1)
                .WithMaxValues(menuBuilder.Options.Count - 1);

            var components = new ComponentBuilder()
               .WithSelectMenu(menuBuilder)
               .Build();

            var maxPages = (int)Math.Ceiling(context.Players.Count() / 24d);
            var message = $"Please select the accounts you want to move from **{context.ClanFrom.Name}** to **{context.ClanTo.Name}**. \n\n" +
                $"Only 24 selections are allowed per page, so there might be multiple selections in a row.\n" +
                $"Currently on page {context.PageCount}/{maxPages}";

            if (context.SelectedPlayers.Count > 0)
            {
                message += "\n\nMembers selected so far: "
                    + string.Join(", ", context.SelectedPlayers.Select(p => p.Name))
                    + $"\nCount: {context.SelectedPlayers.Count}";
            }

            _cache.Set(messageId, context, Options.MemoryCacheEntryOptions);

            return (message, components);
        }

        public async Task<(string, MessageComponent)> HandleCwlSignupMoveSelection(SocketMessageComponent interaction)
        {
            var messageId = interaction.Message.Id;
            if (!_cache.TryGetValue(messageId, out SignupMoveContext? signupMoveContext) || signupMoveContext is null)
            {
                return ("Something went wrong. Please try again", new ComponentBuilder().Build());
            }

            var selectedValues = interaction.Data.Values;
            List<Player> selectedPlayers = [];
            if (selectedValues.Contains("none"))
            {
                if (selectedValues.Count > 1)
                {
                    return ("You dummy selected \"None from this page.\" in addition to other members.\nI would have expected that from Xero but not from you...\nAs a punishment you have to start over.", new ComponentBuilder().Build());
                }
            }
            else
            {
                selectedPlayers = (await _playersClient.GetCachedPlayersAsync(selectedValues)).Select(cp => cp.Content).Where(p => p is not null).Cast<Player>().ToList();
            }

            signupMoveContext.SelectedPlayers.AddRange(selectedPlayers);

            if (signupMoveContext.PageCount * 24 < signupMoveContext.Players.Count())
            {
                //post next page
                _cache.Set(messageId, signupMoveContext, Options.MemoryCacheEntryOptions);
                return UpdateCwlSignupMoveSelectionNextPage(messageId);
            }
            else
            {
                //do move
                return MoveCwlSignups(signupMoveContext);
            }
        }

        public (string, MessageComponent) MoveCwlSignups(SignupMoveContext signupMoveContext)
        {
            if (signupMoveContext.SelectedPlayers.Count <= 0)
            {
                return ("No members selected to move. I'm chillin", new ComponentBuilder().Build());
            }

            foreach (var player in signupMoveContext.SelectedPlayers)
            {
                var signup = _botDb.CwlSignups.FirstOrDefault(s => s.PlayerTag == player.Tag && !s.Archieved);
                if (signup is not null)
                    signup.ClanTag = signupMoveContext.ClanTo.Tag;
            }
            _botDb.SaveChanges();

            var message = $"Moved the following players from **{signupMoveContext.ClanFrom.Name}** ({signupMoveContext.ClanFrom.Tag}) to **{signupMoveContext.ClanTo.Name}** ({signupMoveContext.ClanTo.Tag})\n\n";
            message += string.Join(", ", signupMoveContext.SelectedPlayers.Select(p => p.Name));
            return (message, new ComponentBuilder().Build());
        }

        public async Task<(string, MessageComponent)> CreateClanConfirmationCheck(SocketMessageComponent interaction)
        {
            Player player = null!;
            Clan selectedClan = null!
                ;
            try
            {
                var signup = GetSignupFromCache(interaction);
                player = await _playersClient.GetOrFetchPlayerAsync(signup!.PlayerTag);
                var selectedClanTag = interaction.Data.Values.First();
                selectedClan = await _clansClient.GetOrFetchClanAsync(selectedClanTag);
            }
            catch
            {
                await HandleInteractionError(interaction);
            }


            var confirmButton = new ButtonBuilder()
                .WithLabel("Yes, this is the right Clan")
                .WithCustomId("button_cwl_signup_clan_confirm")
                .WithStyle(ButtonStyle.Success);

            var cancelButton = new ButtonBuilder()
                .WithLabel("No, I need to choose a different one")
                .WithCustomId("button_cwl_signup_clan_cancel")
                .WithStyle(ButtonStyle.Danger);

            var component = new ComponentBuilder()
                .WithButton(confirmButton)
                .WithButton(cancelButton)
                .Build();

            var message = $"Are you sure you want to sign up with **{player.Name}** ({player.Tag}, TH{player.TownHallLevel}) in **{selectedClan.Name}** ({selectedClan.Tag})? That account is currently in a different clan.";
            return (message, component);
        }

        public async Task HandleInteractionError(SocketMessageComponent interaction)
        {
            var embed = _embedHelper.ErrorEmbed("Error", "Something went wrong. Please try again.");
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
            var signup = GetSignupFromCache(interaction);

            if (signup is null)
                return false;

            _botDb.Add(signup);
            await _botDb.SaveChangesAsync();
            return true;
        }

        public CwlSignup? GetSignupFromCache(SocketMessageComponent interaction)
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

        public async Task<(Embed, MessageComponent?)> SignupRoster(string clanTag, bool forceNew)
        {
            try
            {
                var clan = await _clansClient.GetOrFetchClanAsync(clanTag);

                var pinnedRoster = _botDb.PinnedRosters.FirstOrDefault(pr => pr.ClanTag == clanTag);
                if (pinnedRoster is not null
                    && !string.IsNullOrEmpty(pinnedRoster.Url)
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
                        .WithUrl(pinnedRoster.Url)
                        .WithStyle(ButtonStyle.Link);

                    var components = new ComponentBuilder()
                        .WithButton(urlButton)
                        .Build();

                    return (embed, components);
                }
                else
                {
                    var signups = _botDb.CwlSignups.Where(s => s.ClanTag == clanTag && !s.Archieved).ToList();
                    var data = FormatDataForRosterSpreadsheet(signups);
                    var url = await _gspreadService.WriteCwlRosterData(data, clan);

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

                var pinnedRoster = _botDb.PinnedRosters.FirstOrDefault(pr => pr.ClanTag == clanTag);
                if (pinnedRoster == null)
                {
                    pinnedRoster = new PinnedRoster { ClanTag = clanTag };
                    _botDb.PinnedRosters.Add(pinnedRoster);
                }
                pinnedRoster.Url = rosterUrl;
                _botDb.SaveChanges();

                var builder = new EmbedBuilder()
                    .WithColor(Color.Green);

                if (string.IsNullOrEmpty(rosterUrl))
                {
                    builder.WithDescription($"Reset pinned roster for {clan.Name}.");
                }
                else
                {
                    builder.WithDescription($"Pinned [Roster]({rosterUrl}) for {clan.Name}.");
                }

                return builder.Build();
            }
            catch (Exception ex)
            {
                return _embedHelper.ErrorEmbed("Error", ex.Message);
            }
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
            var signups = _botDb.CwlSignups.Where(s => s.PlayerTag == playerTag && !s.Archieved);
            if (signups.Any())
            {
                _botDb.CwlSignups.RemoveRange(signups);
                await _botDb.SaveChangesAsync();
            }
        }


        public async Task<string> RolesAssign(SocketInteractionContext context)
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
            return _botDb.CwlSignups.Where(s => !s.Archieved).Count();
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

        public async Task<string> GetSignupSummaryMessage(SocketMessageComponent interaction)
        {
            var signup = GetSignupFromCache(interaction)!;
            var clan = await _clansClient.GetOrFetchClanAsync(signup.ClanTag);
            return $"Account: {signup.PlayerName} ({signup.PlayerTag})\n" +
                $"Clan: {clan.Name} ({clan.Tag})\n" +
                $"OptOutDays: {signup.OptOutDays}\n" +
                $"WarStyle: {signup.WarPreference}\n" +
                $"Bonuses: {signup.Bonus}\n" +
                $"WarGeneral: {signup.WarGeneral}";
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
