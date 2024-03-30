using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using ZenBotCS.Attributes;
using ZenBotCS.Handler;
using ZenBotCS.Services.SlashCommands;

namespace ZenBotCS.Modules
{
    [Group("cwl", "Commands related to cwl")]
    public class CwlModule : InteractionModuleBase<SocketInteractionContext>
    {
        public required CwlService CwlService { get; set; }
        public required ILogger<CwlModule> Logger { get; set; }

        [SlashCommand("data", "Get data for the current cwl in a spreadsheet")]
        public async Task Data(
            [Summary("ClanTag"), Autocomplete(typeof(ClanTagAutocompleteHandler))] string clantag,
            [Summary("SpreadsheetId")] string? spreadsheetId = null)
        {
            await DeferAsync();
            var embed = await CwlService.Data(clantag, spreadsheetId);
            await FollowupAsync(embed: embed);
        }

        [Group("signup", "Commands related to cwl roster")]
        public class Signups : InteractionModuleBase<SocketInteractionContext>
        {
            public required CwlService CwlService { get; set; }

            [RequireUserPermission(Discord.GuildPermission.Administrator)]
            [SlashCommand("post", "Post the signup embed")]
            public async Task Post()
            {
                (var embeds, var component) = CwlService.SignupPost();
                await Context.Channel.SendMessageAsync(embeds: embeds, components: component);
                await RespondAsync(
                    text: $"There are currently already {CwlService.GetCurrentSignupCount()} signups. Remember to reset these with `/cwl signup reset` if this is not intentional",
                    ephemeral: true);
            }

            [RequireLeadershipRole]
            [SlashCommand("roster", "Creates a spreadsheet for the roster of the chosen clan")]
            public async Task Roster([Summary("ClanTag"), Autocomplete(typeof(ClanTagAutocompleteHandler))] string clantag)
            {
                await DeferAsync();
                var embed = await CwlService.SignupRoster(clantag);
                await FollowupAsync(embed: embed);
            }

            [SlashCommand("summary", "Gives summary of all cwl signups")]
            public async Task Summary(
                [Summary("ClanTag"), Autocomplete(typeof(ClanTagAutocompleteHandler))] string? clantag = null)
            {
                await DeferAsync();
                Embed embed;
                if (clantag is null)
                {
                    embed = await CwlService.SignupSummaryAllClans();
                }
                else
                {
                    embed = await CwlService.SignupSummaryClan(clantag);
                }
                await FollowupAsync(embed: embed);
            }

            [SlashCommand("check", "Check the signups for a player or discord user")]
            public async Task Check(
                [Summary("PlayerTag"), Autocomplete(typeof(PlayerTagAutocompleteHandler))] string? playerTag = null,
                [Summary("User")] SocketUser? user = null)
            {
                await DeferAsync();
                if (playerTag is null && user is null)
                    user = Context.User;
                var embed = await CwlService.SignupCheck(playerTag, user);
                await FollowupAsync(embed: embed);
            }

            [RequireUserPermission(Discord.GuildPermission.Administrator)]
            [SlashCommand("delete", "Deletes a signup for one player")]
            public async Task Delete([Summary("PlayerTag"), Autocomplete(typeof(PlayerTagAutocompleteHandler))] string playerTag)
            {
                await DeferAsync(true);
                await CwlService.SignupDelete(playerTag);
                await FollowupAsync($"Deleted signup of {playerTag}", ephemeral: true);
            }

            [RequireUserPermission(Discord.GuildPermission.Administrator)]
            [SlashCommand("reset", "Archives all current signup, to reset for a new CWL Season")]
            public async Task Reset()
            {
                var (embed, component) = CwlService.SignupArchiveConfirmDialog();
                await RespondAsync(embed: embed, components: component, ephemeral: true);
            }

            [RequireUserPermission(Discord.GuildPermission.Administrator)]
            [ComponentInteraction("button_cwl_signup_reset_confirm", true)]
            public async Task ResetConfirm()
            {
                if (Context.Interaction is not SocketMessageComponent interaction)
                    return;
                await DeferAsync();

                await CwlService.SignupsReset();

                await interaction.ModifyOriginalResponseAsync(x =>
                {
                    x.Content = "Signups reset";
                    x.Components = null;
                    x.Embed = null;
                });
            }

            [RequireUserPermission(Discord.GuildPermission.Administrator)]
            [ComponentInteraction("button_cwl_signup_reset_cancel", true)]
            public async Task ResetCancel()
            {
                if (Context.Interaction is not SocketMessageComponent interaction)
                    return;
                await DeferAsync();

                await interaction.ModifyOriginalResponseAsync(x =>
                {
                    x.Content = "Canaceled";
                    x.Components = null;
                    x.Embed = null;
                });
            }

            [RequireLeadershipRole]
            [SlashCommand("dump", "Dumps the signup table")]
            public async Task Dump(bool includeArchives)
            {
                await DeferAsync();
                var embed = await CwlService.SignupDump(includeArchives);
                await FollowupAsync(embed: embed);
            }
        }

        [Group("roles", "Commands related to CWL Roles")]
        public class Roles : InteractionModuleBase<SocketInteractionContext>
        {
            public required CwlService CwlService { get; set; }

            [RequireUserPermission(Discord.GuildPermission.Administrator)]
            [SlashCommand("assign", "Assigns CWL roles to each user for each clan they have signed up for")]
            public async Task Assign()
            {
                await DeferAsync();
                var message = await CwlService.RolesAssign(Context);
                await FollowupAsync(message);
            }

            [RequireUserPermission(Discord.GuildPermission.Administrator)]
            [SlashCommand("remove", "Removes all CWL roles for every user")]
            public async Task Remove()
            {
                await DeferAsync();
                var message = await CwlService.RolesRemove(Context);
                await FollowupAsync(message);
            }

        }


        //Todo:to signups dump, delete signup, reset all signups, check during signup for: opt out days error



        [ComponentInteraction("button_cwl_signup_create", true)]
        public async Task CreateCwlSignup()
        {
            if (Context.Interaction is not SocketMessageComponent interaction)
                return;
            await DeferAsync();
            (var message, var components) = await CwlService.CreateCwlSignupAccountSelection(interaction.User);
            await FollowupAsync(message, components: components, ephemeral: true);
        }

        [ComponentInteraction("menu_cwl_signup_account", true)]
        public async Task HandleCwlSignupAccountSelected()
        {
            if (Context.Interaction is not SocketMessageComponent interaction)
                return;
            await DeferAsync();

            if (CwlService.CheckAlreadyRegistered(interaction))
            {
                await interaction.ModifyOriginalResponseAsync(x =>
                {
                    x.Content = "You have already registered that account. Type `/cwl signup check` to check your registration";
                    x.Components = null;
                });
                return;
            }

            await CwlService.TryCacheSigupDetails(interaction);

            (var message, var components) = await CwlService.CreateCwlSignupClanSelection();
            await interaction.ModifyOriginalResponseAsync(x =>
            {
                x.Content = message;
                x.Components = components;
            });
        }

        [ComponentInteraction("menu_cwl_signup_clan", true)]
        public async Task HandleCwlSignupClanSelected()
        {
            if (Context.Interaction is not SocketMessageComponent interaction)
                return;
            await DeferAsync();

            if (!CwlService.TryUpdateCachedSignupClan(interaction))
            {
                await CwlService.HandleSignupError(interaction);
                return;
            }

            (var message, var components) = CwlService.CreateCwlSignupOptOutSelection();
            await interaction.ModifyOriginalResponseAsync(x =>
            {
                x.Content = message;
                x.Components = components;
            });
        }

        [ComponentInteraction("menu_cwl_signup_optout", true)]
        public async Task HandleCwlSignupOptOutSelected()
        {
            if (Context.Interaction is not SocketMessageComponent interaction)
                return;
            await DeferAsync();

            if (!CwlService.CheckValidOptOuts(interaction))
            {
                await interaction.ModifyOriginalResponseAsync(x =>
                {
                    x.Content = interaction.Message.Content + "\n```diff\n- You can not be available for all days AND have opt out days at the same time.```";
                });

                return;
            }

            if (!CwlService.TryUpdateCachedSignupOptOuts(interaction))
            {
                await CwlService.HandleSignupError(interaction);
                return;
            }

            (var message, var components) = CwlService.CreateCwlSignupStyleSelection();
            await interaction.ModifyOriginalResponseAsync(x =>
            {
                x.Content = message;
                x.Components = components;
            });
        }

        [ComponentInteraction("menu_cwl_signup_style", true)]
        public async Task HandleCwlSignupStyleSelected()
        {
            if (Context.Interaction is not SocketMessageComponent interaction)
                return;
            await DeferAsync();

            if (!CwlService.TryUpdateCachedSignupStyle(interaction))
            {
                await CwlService.HandleSignupError(interaction);
                return;
            }

            (var message, var components) = CwlService.CreateCwlSignupBonusSelection();
            await interaction.ModifyOriginalResponseAsync(x =>
            {
                x.Content = message;
                x.Components = components;
            });
        }

        [ComponentInteraction("menu_cwl_signup_bonus", true)]
        public async Task HandleCwlSignupBonusSelected()
        {
            if (Context.Interaction is not SocketMessageComponent interaction)
                return;
            await DeferAsync();

            if (!CwlService.TryUpdateCachedSignupBonus(interaction))
            {
                await CwlService.HandleSignupError(interaction);
                return;
            }

            (var message, var components) = CwlService.CreateCwlSignupGeneralSelection();
            await interaction.ModifyOriginalResponseAsync(x =>
            {
                x.Content = message;
                x.Components = components;
            });
        }

        [ComponentInteraction("menu_cwl_signup_general", true)]
        public async Task HandleCwlSignupGeneralSelected()
        {
            if (Context.Interaction is not SocketMessageComponent interaction)
                return;
            await DeferAsync();

            if (!CwlService.TryUpdateCachedSignupGeneral(interaction))
            {
                await CwlService.HandleSignupError(interaction);
                return;
            }

            if (!await CwlService.SaveSignupToDb(interaction))
            {
                await CwlService.HandleSignupError(interaction);
                return;
            }

            var content = await CwlService.GetSignupSummaryMessage(interaction);

            await interaction.ModifyOriginalResponseAsync(x =>
            {
                x.Content = "You have sucessfully signed up for CWL!\n\n" + content;
                x.Components = null;
            });
        }
    }
}
