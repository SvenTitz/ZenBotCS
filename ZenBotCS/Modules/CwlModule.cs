using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
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

            [RequireOwner(Group = "Permission")]
            [RequireLeadershipRole(Group = "Permission")]
            [SlashCommand("roster", "Returns the pinned roster for a given clan, or generates a new one if none is pinned")]
            public async Task Roster(
                [Summary("ClanTag"), Autocomplete(typeof(ClanTagAutocompleteHandler))] string clantag,
                [Summary("ForceNew")] bool forceNew = false)
            {
                await DeferAsync();
                var (embed, components) = await CwlService.SignupRoster(clantag, forceNew);
                await FollowupAsync(embed: embed, components: components);
            }

            [RequireOwner(Group = "Permission")]
            [RequireLeadershipRole(Group = "Permission")]
            [SlashCommand("pin-roster", "Pins the roster for the given clan")]
            public async Task RosterPin(
                [Summary("ClanTag"), Autocomplete(typeof(ClanTagAutocompleteHandler))] string clantag,
                [Summary("RosterSpreadsheetUrl")] string rosterUrl = "")
            {
                await DeferAsync();
                var embed = await CwlService.SingupRosterPin(clantag, rosterUrl);
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

            [RequireOwner(Group = "Permission")]
            [RequireLeadershipRole(Group = "Permission")]
            [SlashCommand("dump", "Dumps the signup table")]
            public async Task Dump(bool includeArchives)
            {
                await DeferAsync();
                var embed = await CwlService.SignupDump(includeArchives);
                await FollowupAsync(embed: embed);
            }

            [RequireUserPermission(Discord.GuildPermission.Administrator)]
            [SlashCommand("move", "Move player signups between clans")]
            public async Task Move(
                [Summary("MoveFrom"), Autocomplete(typeof(ClanTagAutocompleteHandler))] string clantagFrom,
                [Summary("MoveTo"), Autocomplete(typeof(ClanTagAutocompleteHandler))] string clantagTo)
            {
                await DeferAsync(true);
                var message = await FollowupAsync($"Builder members selection...");
                (var content, var components) = await CwlService.CreateCwlSignupMove(clantagFrom, clantagTo, message.Id);
                await Context.Interaction.ModifyOriginalResponseAsync(x =>
                {
                    x.Content = content;
                    x.Components = components;
                });
            }
        }

        [Group("roles", "Commands related to CWL Roles")]
        public class Roles : InteractionModuleBase<SocketInteractionContext>
        {
            public required CwlService CwlService { get; set; }

            [RequireUserPermission(Discord.GuildPermission.ManageRoles)]
            [SlashCommand("assign", "Assigns CWL roles form roster sheet. Either provide roster URL or select clan to use pinned roster")]
            public async Task Assign(
                [Description("The role to be assigned")] SocketRole role,
                [Description("The URL of the roster spreadsheet for which you want to apply roles")] string? spreadsheetUrl = null,
                [Summary("clan"), Description("Use this clans pinned roster spreadsheet url"), Autocomplete(typeof(ClanTagAutocompleteHandler))] string? clantag = null)
            {
                await DeferAsync();
                var message = await CwlService.RolesAssign(Context, role, spreadsheetUrl, clantag);
                await FollowupAsync(message);
            }

            [RequireUserPermission(Discord.GuildPermission.ManageRoles)]
            [SlashCommand("remove", "Removes all CWL roles for every user")]
            public async Task Remove()
            {
                await DeferAsync();
                var message = await CwlService.RolesRemove(Context);
                await FollowupAsync(message);
            }

        }

        [RequireUserPermission(Discord.GuildPermission.Administrator)]
        [ComponentInteraction("button_cwl_signup_close", true)]
        public async Task HandleCwlSignupClose()
        {
            await DeferAsync(true);
            (var message, var embes, var components) = CwlService.HandleCwlSignupClose();
            await Context.Interaction.ModifyOriginalResponseAsync(x =>
            {
                x.Content = message;
                x.Embeds = embes;
                x.Components = components;
            });
        }

        [RequireUserPermission(Discord.GuildPermission.Administrator)]
        [ComponentInteraction("button_cwl_signup_reopen", true)]
        public async Task HandleCwlSignupReopen()
        {
            await DeferAsync(true);
            (var message, var embes, var components) = CwlService.HandleCwlSignupReopen();
            await Context.Interaction.ModifyOriginalResponseAsync(x =>
            {
                x.Content = message;
                x.Embeds = embes;
                x.Components = components;
            });
        }


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
                await CwlService.HandleInteractionError(interaction);
                return;
            }

            if (!await CwlService.CheckCorrectClan(interaction))
            {
                (var message2, var components2) = await CwlService.CreateClanConfirmationCheck(interaction);
                await interaction.ModifyOriginalResponseAsync(x =>
                {
                    x.Content = message2;
                    x.Components = components2;
                });
                return;
            }

            if (await CwlService.CheckMaxDefensesQuestionRequired(interaction))
            {
                (var message, var components) = CwlService.CreateCwlSignupMaxDefensesSelection();
                await interaction.ModifyOriginalResponseAsync(x =>
                {
                    x.Content = message;
                    x.Components = components;
                });
            }
            else
            {
                (var message, var components) = CwlService.CreateCwlSignupOptOutSelection();
                await interaction.ModifyOriginalResponseAsync(x =>
                {
                    x.Content = message;
                    x.Components = components;
                });
            }
        }

        [ComponentInteraction("menu_cwl_signup_max_defenses", true)]
        public async Task HandleCwlSignupMaxDefensesSelected()
        {
            if (Context.Interaction is not SocketMessageComponent interaction)
                return;
            await DeferAsync();

            if (!CwlService.TryUpdateCachedSignupMaxedDefenses(interaction))
            {
                await CwlService.HandleInteractionError(interaction);
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
                await CwlService.HandleInteractionError(interaction);
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
                await CwlService.HandleInteractionError(interaction);
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
                await CwlService.HandleInteractionError(interaction);
                return;
            }

            if (!await CwlService.SaveSignupToDb(interaction))
            {
                await CwlService.HandleInteractionError(interaction);
                return;
            }

            var content = await CwlService.GetSignupSummaryMessage(interaction);

            await interaction.ModifyOriginalResponseAsync(x =>
            {
                x.Content = "You have sucessfully signed up for CWL!\n\n" + content;
                x.Components = null;
            });
        }

        [ComponentInteraction("button_cwl_signup_clan_confirm", true)]
        public async Task HandleClanSelectionConfirmed()
        {
            if (Context.Interaction is not SocketMessageComponent interaction)
                return;
            await DeferAsync();

            if (await CwlService.CheckMaxDefensesQuestionRequired(interaction))
            {
                (var message, var components) = CwlService.CreateCwlSignupMaxDefensesSelection();
                await interaction.ModifyOriginalResponseAsync(x =>
                {
                    x.Content = message;
                    x.Components = components;
                });
            }
            else
            {
                (var message, var components) = CwlService.CreateCwlSignupOptOutSelection();
                await interaction.ModifyOriginalResponseAsync(x =>
                {
                    x.Content = message;
                    x.Components = components;
                });
            }
        }

        [ComponentInteraction("button_cwl_signup_clan_cancel", true)]
        public async Task HandleClanSelectionCancled()
        {
            if (Context.Interaction is not SocketMessageComponent interaction)
                return;
            await DeferAsync();

            (var message, var components) = await CwlService.CreateCwlSignupClanSelection();
            await interaction.ModifyOriginalResponseAsync(x =>
            {
                x.Content = message;
                x.Components = components;
            });
        }

        [ComponentInteraction("menu_cwl_signup_move", true)]
        public async Task HandleCwlSignupMoveSelected()
        {
            if (Context.Interaction is not SocketMessageComponent interaction)
                return;
            await DeferAsync();

            (var message, var components) = await CwlService.HandleCwlSignupMoveSelection(interaction);

            await interaction.ModifyOriginalResponseAsync(x =>
            {
                x.Content = message;
                x.Components = components;
            });
        }
    }
}
