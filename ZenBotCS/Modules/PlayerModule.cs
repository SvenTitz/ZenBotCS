using Discord.Interactions;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZenBotCS.Handler;

namespace ZenBotCS.Modules
{
    [Group("player", "Commands related to players")]
    public class PlayerModule : InteractionModuleBase<SocketInteractionContext>
    {
        [Group("stats", "Commands related to player stats")]
        public class PlayerStatsModule : InteractionModuleBase<SocketInteractionContext>
        {
            [SlashCommand("misses", "Get a list of a players missed attacks.")]
            public async Task Misses(
               [Summary("PlayerTag"), Autocomplete(typeof(PlayerTagAutocompleteHandler))] string? playerTag = null,
               [Summary("User")] SocketUser? user = null)
            {
                await DeferAsync();

            }
        }

    }
}
