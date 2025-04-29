using CocApi.Cache;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZenBotCS.Clients;
using ZenBotCS.Entities;
using ZenBotCS.Extensions;

namespace ZenBotCS.Services.Background;

public class PlayerStatsUpdateService(IServiceScopeFactory _serviceScopeFactory, ILogger<PlayerStatsUpdateService> _logger) : BackgroundService
{

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var clashKingApiClient = scope.ServiceProvider.GetRequiredService<ClashKingApiClient>();
                var botDb = scope.ServiceProvider.GetRequiredService<BotDataContext>();
                var playersClient = scope.ServiceProvider.GetRequiredService<PlayersClient>();

                var chachedPlayers = await playersClient.GetCachedPlayersAsync();

                _logger.LogInformation("Pulling PlayerStats for {count} players", chachedPlayers.Count);
                foreach (var player in chachedPlayers.Where(p => p.TownHallLevel >= 7))
                {
                    //_logger.LogInformation("Pulling Stats for {name} ({tag})", player.Name, player.Tag);
                    var newStats = await clashKingApiClient.GetPlayerStatsAsync(player.Tag);
                    var newWarHits = await clashKingApiClient.GetPlayerWarAttacksAsync(player.Tag, 100);

                    var entry = botDb.PlayerStats.FirstOrDefault(ps => ps.PlayerTag == player.Tag);
                    if (entry is null)
                    {
                        //_logger.LogInformation("Creating new PlayerStats entry for {name} ({tag})", player.Name, player.Tag);
                        entry = new Entities.Models.PlayerStats() { PlayerTag = player.Tag };
                        botDb.PlayerStats.Add(entry);
                    }
                    //_logger.LogInformation("Updating PlayerStats entry for {name} ({tag})", player.Name, player.Tag);

                    if (newStats is not null)
                        entry.Player = newStats;
                    else
                        _logger.LogWarning("Could not get Updated Stats for {name} ({tag})", player.Name, player.Tag);

                    if (newWarHits is not null)
                        entry.PlayerWarhits = newWarHits;
                    else
                        _logger.LogWarning("Could not get Updated War Hits for {name} ({tag})", player.Name, player.Tag);

                    entry.UpdatedAt = DateTime.UtcNow;

                    botDb.SaveChanges();
                    await Task.Delay(new TimeSpan(hours: 0, minutes: 0, seconds: 5), stoppingToken);
                }
            }
        }
    }
}
