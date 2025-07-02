using System.Diagnostics;
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
        var stopwatchTotal = new Stopwatch();
        var stopwatchFirst = new Stopwatch();
        var stopwatchFetch = new Stopwatch();

        stopwatchTotal.Start();
        stopwatchFirst.Start();
        var playerStats = _botDb.PlayerStats.FirstOrDefault(p => p.PlayerTag == playerTag);
        stopwatchFirst.Stop();
        var player = playerStats?.Player;
        if (player is null || playerStats?.UpdatedAt < DateTime.UtcNow.AddDays(-1))
        {
            stopwatchFetch.Start();
            player = await _ckApiClient.GetPlayerStatsAsync(playerTag);
            stopwatchFetch.Stop();
        }
        var timestamp = playerStats?.UpdatedAt ?? DateTime.UtcNow;
        stopwatchTotal.Stop();

        _logger.LogInformation("Total: {1}ms, First: {2}ms, Fetch: {3}ms", stopwatchTotal.ElapsedMilliseconds, stopwatchFirst.ElapsedMilliseconds, stopwatchFetch.ElapsedMilliseconds);

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
