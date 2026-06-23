using System.Text;
using CocApi.Cache;
using CocApi.Rest.Models;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ZenBotCS.Clients;
using ZenBotCS.Entities;
using ZenBotCS.Entities.Models;
using ZenBotCS.Entities.Models.Enums;
using ZenBotCS.Extensions;
using ZenBotCS.Helper;
using ZenBotCS.Models;
using WarPreference = ZenBotCS.Entities.Models.Enums.WarPreference;

namespace ZenBotCS.Services.SlashCommands
{
    public class CwlSignupWizardService(
        CustomClansClient _clansClient,
        PlayersClient _playersClient,
        BotDataContext _botDb,
        EmbedHelper _embedHelper,
        IMemoryCache _cache,
        CwlSignupCache _signupCache,
        IConfiguration _config,
        ClashKingApiClient _clashKingApiClient,
        DiscordHelper _discordHelper)
    {
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
            var checkButton = new ButtonBuilder()
                .WithLabel("Check Your Signups")
                .WithCustomId("button_cwl_signup_check")
                .WithStyle(ButtonStyle.Primary);
            var helpButton = new ButtonBuilder()
                .WithLabel("Help")
                .WithCustomId("button_cwl_signup_help")
                .WithStyle(ButtonStyle.Primary);
            var component = new ComponentBuilder()
                .WithButton(signupButton)
                .WithButton(checkButton)
                .WithButton(helpButton)
                .WithButton(closeButton)
                .Build();


            Embed[] embeds = [
                new EmbedBuilder()
                    .WithImageUrl("https://cdn.discordapp.com/attachments/1126771582396805221/1221539143473958912/Zen-CWL-Signups.jpg?ex=6612f1fa&is=66007cfa&hm=cac106677455f60967b6ef5ea4f4fa032d41d56400953f950b8e268352988653&")
                    .Build(),
                new EmbedBuilder()
                    .WithTitle("CWL Signups")
                    .WithDescription("- Click the \"Sign Up\" button to sign up for the next upcoming CWL.\n" +
                                    "- Click the \"Help\" button for additional information.")
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
            var checkButton = new ButtonBuilder()
                .WithLabel("Check Your Signups")
                .WithCustomId("button_cwl_signup_check")
                .WithStyle(ButtonStyle.Primary);
            var helpButton = new ButtonBuilder()
                .WithLabel("Help")
                .WithCustomId("button_cwl_signup_help")
                .WithStyle(ButtonStyle.Primary);
            var component = new ComponentBuilder()
                .WithButton(signupButton)
                .WithButton(checkButton)
                .WithButton(helpButton)
                .WithButton(reopenButton)
                .Build();

            Embed[] embeds = [
                new EmbedBuilder()
                    .WithImageUrl("https://cdn.discordapp.com/attachments/1126771582396805221/1221539143473958912/Zen-CWL-Signups.jpg?ex=6612f1fa&is=66007cfa&hm=cac106677455f60967b6ef5ea4f4fa032d41d56400953f950b8e268352988653&")
                    .Build(),
                new EmbedBuilder()
                    .WithTitle("CWL Signups")
                    .WithDescription("~~- Click the \"Sign Up\" button to sign up for the next upcoming CWL.\n" +
                                    "- Click the \"Help\" button for additional information.~~\n" +
                                    "**CWL Signup has been closed.**")
                    .WithImageUrl("https://cdn.discordapp.com/attachments/809874883768614922/1231630801792405704/Zen-CWL-Spacer.png?ex=6629d0d1&is=66287f51&hm=3372eb6161b41bb81bc6d89e02049e8e6ea1bc2126abb3f7bd8079306207b7c9&")

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

            //var playerTags = _botDb.DiscordLinks.Where(dl => dl.DiscordId == user.Id).Select(dl => dl.PlayerTag);
            var playerTags = await _clashKingApiClient.PostDiscordLinksAsync(user.Id);

            if (!playerTags.Any())
            {
                var errorMessage = "No clash accounts linked to your user. Please link them with ClashPerks oder ClashKing.";
                return await Task.FromResult((errorMessage, new ComponentBuilder().Build()));
            }

            //var players = (await _playersClient.GetCachedPlayersAsync(playerTags)).Select(cp => cp.Content);
            IEnumerable<Player> players = await _playersClient.GetOrFetchPlayersAsync(playerTags);
            players = players.OrderByDescending(p => p?.TownHallLevel).ThenBy(p => p?.Name).Take(25);

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

        public async Task<bool> CheckMaxDefensesQuestionRequired(SocketMessageComponent interaction)
        {
            try
            {
                var signup = GetSignupFromCache(interaction);
                if (signup!.PlayerThLevel < 16)
                    return false;

                var clanSettings = _botDb.ClanSettings.FirstOrDefault(cs => cs.ClanTag == signup.ClanTag);

                return clanSettings?.ChampStyleCwlRoster ?? false;

            }
            catch
            {
                await HandleInteractionError(interaction);
                return false;
            }
        }

        public async Task<(string, MessageComponent)> CreateCwlSignupClanSelection()
        {
            var clans = await _clansClient.GetCachedClansAsync();
            var clanSettings = _botDb.ClanSettings.AsNoTracking();
            var clanSettingsDict = _botDb.ClanSettings.AsNoTracking().ToDictionary(cs => cs.ClanTag, cs => cs);
            clans = clans.Where(c => clanSettings?.FirstOrDefault(o => o.ClanTag == c.Tag)?.EnableCwlSignup ?? false).ToList();
            clans = clans.OrderBy(c => clanSettingsDict[c.Tag].ClanType).ThenBy(c => clanSettingsDict[c.Tag].Order).ToList();

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

        public (string, MessageComponent) CreateCwlSignupMaxDefensesSelection()
        {
            var menuBuilder = new SelectMenuBuilder()
                .WithPlaceholder("Select answer")
                .WithCustomId("menu_cwl_signup_max_defenses")
                .WithMinValues(1)
                .WithMaxValues(1)
                .AddOption("Yes, almost maxed", "true")
                .AddOption("No, not quite maxed", "false");

            var components = new ComponentBuilder()
                .WithSelectMenu(menuBuilder)
                .Build();

            var message = "Are your defenses close to being maxed for TH16?";
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

            var message = "Please select the days on which you are **__NOT__** able to participate in CWL. \nKeep in mind that the first battle days usually starts on the second day of each month.";
            return (message, components);
        }

        public (string, MessageComponent) CreateCwlSignupStyleSelection()
        {
            var menuBuilder = new SelectMenuBuilder()
                .WithPlaceholder("Select your preference")
                .WithCustomId("menu_cwl_signup_style")
                .WithMinValues(1)
                .WithMaxValues(1)
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
            var players = (await _playersClient.GetOrFetchPlayersAsync(signups.Select(s => s.PlayerTag)));
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
                selectedPlayers = (await _playersClient.GetOrFetchPlayersAsync(selectedValues)).Where(p => p is not null).Cast<Player>().ToList();
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

            _signupCache.Set(interaction.Message.Id, data);
            return true;
        }

        public bool TryUpdateCachedSignupClan(SocketMessageComponent interaction)
        {
            var clanTag = interaction.Data.Values.First();
            if (string.IsNullOrEmpty(clanTag))
                return false;

            return _signupCache.TryUpdate(interaction.Message.Id, data => data.ClanTag = clanTag);
        }

        public bool TryUpdateCachedSignupMaxedDefenses(SocketMessageComponent interaction)
        {
            var isMax = interaction.Data.Values.First() == "true";
            return _signupCache.TryUpdate(interaction.Message.Id, data => data.MaxDefeneses = isMax);
        }

        public bool TryUpdateCachedSignupOptOuts(SocketMessageComponent interaction)
        {
            var optOutDays = ConvertOptOutDataToEnum(interaction.Data.Values);
            return _signupCache.TryUpdate(interaction.Message.Id, data => data.OptOutDays = optOutDays);
        }

        public bool TryUpdateCachedSignupStyle(SocketMessageComponent interaction)
        {
            var styleIdString = interaction.Data.Values.First();
            if (!int.TryParse(styleIdString, out int styleId))
                return false;
            var style = (WarPreference)styleId;

            return _signupCache.TryUpdate(interaction.Message.Id, data => data.WarPreference = style);
        }

        public bool TryUpdateCachedSignupBonus(SocketMessageComponent interaction)
        {
            var bonus = interaction.Data.Values.First() == "true";
            return _signupCache.TryUpdate(interaction.Message.Id, data => data.Bonus = bonus);
        }

        public bool TryUpdateCachedSignupGeneral(SocketMessageComponent interaction)
        {
            var general = interaction.Data.Values.First() == "true";
            return _signupCache.TryUpdate(interaction.Message.Id, data => data.WarGeneral = general);
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
            return _signupCache.Get(interaction.Message.Id);
        }

        public bool CheckValidOptOuts(SocketMessageComponent interaction)
        {
            var values = interaction.Data.Values;
            return values.Count == 1 || !values.Contains(((int)OptOutDays.None).ToString());
        }

        internal static OptOutDays ConvertOptOutDataToEnum(IEnumerable<string> values)
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

        public async Task<string> GetSignupSummaryMessage(SocketMessageComponent interaction)
        {
            var signup = GetSignupFromCache(interaction)!;
            var clan = await _clansClient.GetOrFetchClanAsync(signup.ClanTag);
            return $"Account: {signup.PlayerName} ({signup.PlayerTag})\n" +
                $"Clan: {clan.Name} ({clan.Tag})\n" +
                $"OptOutDays: {signup.OptOutDays}\n" +
                $"WarStyle: {signup.WarPreference}\n" +
                $"Bonuses: {signup.Bonus}";
        }


        public async Task<(string content, Embed[] embeds)> GetSignupHelpEmbed()
        {
            var messageLink = _config["CwlSignupHelpButton"];
            if (messageLink == null)
                return ("", [_embedHelper.ErrorEmbed("Error", "No message link specified in config.")]);

            return await _discordHelper.GetMessageFromLinkAsync(messageLink);
        }
    }
}
