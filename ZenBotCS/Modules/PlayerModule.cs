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
                var embed = await PlayerService.StatsAttacks(playerTag, user, warTypeFiler);
                await FollowupAsync(embed: embed);
            }
        }

    }
}
