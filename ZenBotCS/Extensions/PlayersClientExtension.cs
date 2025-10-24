using CocApi.Cache;
using CocApi.Rest.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

        public static async Task<List<Player>> GetOrFetchPlayersAsync(
            this PlayersClient playersClient,
            IEnumerable<string> tags,
            CancellationToken? cancellationToken = null)
        {
            var playerTasks = tags.Select(async tag =>
            {
                try
                {
                    return await playersClient.GetOrFetchPlayerAsync(tag, cancellationToken ?? default);
                }
                catch (Exception ex)
                {
                    playersClient.Logger.LogError(ex, "Failed to fetch player with tag {Tag}", tag);
                    return null;
                }
            });

            var results = await Task.WhenAll(playerTasks);

            var players = results
                .Where(static p => p is not null)
                .Select(static p => p!)
                .ToList();

            return players;
        }
    }
}
