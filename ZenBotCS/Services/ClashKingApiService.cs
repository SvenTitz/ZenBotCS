using Microsoft.Extensions.Logging;
using ZenBotCS.Clients;
using ZenBotCS.Entities;
using ZenBotCS.Entities.Models.ClashKingApi.PlayerStats;
using ZenBotCS.Entities.Models.ClashKingApi.PlayerWarHits;

namespace ZenBotCS.Services;

public class ClashKingApiService(ClashKingApiClient _ckApiClient, BotDataContext _botDb, ILogger<ClashKingApiService> _logger)
{

    public async Task<(Player? player, DateTime lastUpdated)> GetOrFetchPlayerStatsAsync(string playerTag)
    {
        var playerStats = _botDb.PlayerStats.FirstOrDefault(p => p.PlayerTag == playerTag);
        var player = playerStats?.Player;
        if (player is null || playerStats?.UpdatedAt < DateTime.UtcNow.AddDays(-1))
        {
            _logger.LogWarning("Had to get player stats for {name} {playerTag}. Last Updated: {updatedAt}", player?.Name, playerTag, playerStats?.UpdatedAt);
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
            _logger.LogWarning("Had to get player war hits for {name} {playerTag}. Last Updated: {updatedAt}", playerWarHits?.Items.FirstOrDefault()?.MemberData.Name, playerTag, playerStats?.UpdatedAt);
            playerWarHits = await _ckApiClient.GetPlayerWarAttacksAsync(playerTag, 100);
        }
        return playerWarHits;
    }
}
