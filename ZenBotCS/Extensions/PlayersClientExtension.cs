using CocApi.Cache;
using CocApi.Rest.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ZenBotCS.Extensions
{
    public static class PlayersClientExtension
    {
        public static async Task<List<Player>> GetCachedPlayersAsync(this PlayersClient playersClient)
        {
            var players = await (from i in playersClient.ScopeFactory.CreateScope().ServiceProvider.GetRequiredService<CacheDbContext>().Players.AsNoTracking()
                                 select i.Content).ToListAsync<Player>().ConfigureAwait(continueOnCapturedContext: false);

            return players;
        }

        public static async Task<List<Player>> GetOrFetchPlayersAsync(this PlayersClient playersClient, IEnumerable<string> tags, CancellationToken? cancellationToken = null)
        {
            var playerTasks = tags.Select(async tag =>
            {
                return await playersClient.GetOrFetchPlayerAsync(tag);
            });
            List<Player> players = [.. (await Task.WhenAll(playerTasks))];
            return players;
        }
    }
}
