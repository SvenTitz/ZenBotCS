﻿using System.ComponentModel;
using Discord.Interactions;
using Discord.WebSocket;
using ZenBotCS.Attributes;
using ZenBotCS.Handler;
using ZenBotCS.Models.Enums;
using ZenBotCS.Services.SlashCommands;

namespace ZenBotCS.Modules
{
    [Group("player", "Commands related to players")]
    public class PlayerModule : InteractionModuleBase<SocketInteractionContext>
    {
        public required PlayerService PlayerService { get; set; }

        [SlashCommand("to-do", "Lists open war attacks and their remaining times.")]
        public async Task ToDo([Summary("User")] SocketUser? user = null)
        {
            await DeferAsync();
            user ??= Context.User;
            var embeds = await PlayerService.ToDo(user);
            await FollowupAsync(embeds: [.. embeds]);
        }


        [Group("stats", "Commands related to player stats")]
        public class PlayerStatsModule : InteractionModuleBase<SocketInteractionContext>
        {
            public required PlayerService PlayerService { get; set; }

            [SlashCommand("misses", "Get a list of a players missed attacks.")]
            public async Task Misses(
               [Summary("PlayerTag"), Autocomplete(typeof(PlayerTagAutocompleteHandler))] string? playerTag = null,
               [Summary("User")] SocketUser? user = null,
               [Summary("WarTypeFilter")] WarTypeFilter warTypeFiler = WarTypeFilter.RegularAndCWL)
            {
                await DeferAsync();
                if (playerTag is null && user is null)
                    user = Context.User;
                var embed = await PlayerService.StatsMisses(playerTag, user, warTypeFiler);
                await FollowupAsync(embed: embed);
            }

            [SlashCommand("attacks", "Get a breakdown of a players war attacks")]
            public async Task Attacks(
               [Summary("PlayerTag"), Autocomplete(typeof(PlayerTagAutocompleteHandler))] string? playerTag = null,
               [Summary("User")] SocketUser? user = null,
               [Summary("WarTypeFilter")] WarTypeFilter warTypeFiler = WarTypeFilter.RegularAndCWL,
               [Summary("NumberOfDays"), Description("Limits the stats to the last X days")] uint limitDays = 0)
            {
                await DeferAsync();
                if (playerTag is null && user is null)
                    user = Context.User;
                var embed = await PlayerService.StatsAttacks(playerTag, user, warTypeFiler, limitDays);
                await FollowupAsync(embed: embed);
            }

            [RequireOwner(Group = "Permission")]
            [RequireLeadershipRole(Group = "Permission")]
            [SlashCommand("data", "Get all saved data for a player except war attacks")]
            public async Task Data([Summary("PlayerTag"), Autocomplete(typeof(PlayerTagAutocompleteHandler))] string playerTag)
            {
                await DeferAsync();
                var embeds = await PlayerService.StatsData(playerTag);
                await FollowupAsync(embeds: embeds);
            }
        }

    }
}
