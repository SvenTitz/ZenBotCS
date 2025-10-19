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
            using var scope = playersClient.ScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<CacheDbContext>();

            var players = await dbContext.Players
                .AsNoTracking()
                .Where(p => p.RawContent != null)
                .Select(p => p.Content!)
                .ToListAsync()
                .ConfigureAwait(false);

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
