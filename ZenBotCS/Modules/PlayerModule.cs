using Discord.Interactions;
using Discord.WebSocket;
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
            var embed = await PlayerService.ToDo(user);
            await FollowupAsync(embed: embed);
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
               [Summary("WarTypeFilter")] WarTypeFilter warTypeFiler = WarTypeFilter.RegularAndCWL)
            {
                await DeferAsync();
                if (playerTag is null && user is null)
                    user = Context.User;
                var embed = await PlayerService.StatsAttacks(playerTag, user, warTypeFiler);
                await FollowupAsync(embed: embed);
            }
        }

    }
}
