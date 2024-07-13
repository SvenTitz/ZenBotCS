using ZenBotCS.Entities;
using ZenBotCS.Entities.Models.ClashKingApi.PlayerStats;

namespace ZenBotCS.Services;

public class ClashKingApiService(ClashKingApiClient _ckApiClient, BotDataContext _botDb)
{

    public async Task<(Player player, DateTime lastUpdated)> GetOrFetchPlayerStatsAsync(string playerTag)
    {
        var playerStats = _botDb.PlayerStats.FirstOrDefault(p => p.PlayerTag == playerTag);
        var player = playerStats?.Player ?? await _ckApiClient.GetPlayerStatsAsync(playerTag);
        var timestamp = playerStats?.UpdatedAt ?? DateTime.UtcNow;
        return (player, timestamp);
    }
}
