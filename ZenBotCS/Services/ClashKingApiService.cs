using ZenBotCS.Clients;
using ZenBotCS.Entities;
using ZenBotCS.Entities.Models.ClashKingApi.PlayerStats;
using ZenBotCS.Entities.Models.ClashKingApi.PlayerWarHits;

namespace ZenBotCS.Services;

public class ClashKingApiService(ClashKingApiClient _ckApiClient, BotDataContext _botDb)
{

    public async Task<(Player? player, DateTime lastUpdated)> GetOrFetchPlayerStatsAsync(string playerTag)
    {
        var playerStats = _botDb.PlayerStats.FirstOrDefault(p => p.PlayerTag == playerTag);
        var player = playerStats?.Player;
        if (player is null || playerStats?.UpdatedAt < DateTime.UtcNow.AddDays(-1))
        {
            player = await _ckApiClient.GetPlayerStatsAsync(playerTag);
        }
        var timestamp = playerStats?.UpdatedAt ?? DateTime.UtcNow;
        return (player, timestamp);
    }

    public async Task<PlayerWarhits?> GetOrFetchPlayerWarhitsAsync(string playerTag)
    {
        var playerStats = _botDb.PlayerStats.FirstOrDefault(p => p.PlayerTag == playerTag);
        var playerWarHits = playerStats?.PlayerWarhits;
        if (playerWarHits is null || playerStats?.UpdatedAt < DateTime.UtcNow.AddDays(-1))
        {
            playerWarHits = await _ckApiClient.GetPlayerWarAttacksAsync(playerTag, 100);
        }
        return playerWarHits;
    }
}
