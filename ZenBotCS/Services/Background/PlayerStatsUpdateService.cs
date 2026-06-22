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
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var clashKingApiClient = scope.ServiceProvider.GetRequiredService<ClashKingApiClient>();
                var botDb = scope.ServiceProvider.GetRequiredService<BotDataContext>();
                var playersClient = scope.ServiceProvider.GetRequiredService<PlayersClient>();

                var chachedPlayers = await playersClient.GetCachedPlayersAsync();

                _logger.LogInformation("Pulling PlayerStats for {count} players", chachedPlayers.Count);
                foreach (var player in chachedPlayers.Where(p => p.TownHallLevel >= 7))
                {
                    try
                    {
                        var newStats = await clashKingApiClient.GetPlayerStatsAsync(player.Tag);
                        var newWarHits = await clashKingApiClient.GetPlayerWarAttacksAsync(player.Tag, 100);

                        var entry = botDb.PlayerStats.FirstOrDefault(ps => ps.PlayerTag == player.Tag);
                        if (entry is null)
                        {
                            entry = new Entities.Models.PlayerStats() { PlayerTag = player.Tag };
                            botDb.PlayerStats.Add(entry);
                        }

                        if (newStats is not null)
                            entry.Player = newStats;
                        else
                            _logger.LogWarning("Could not get Updated Stats for {name} ({tag})", player.Name, player.Tag);

                        if (newWarHits is not null)
                            entry.PlayerWarhits = newWarHits;
                        else
                            _logger.LogWarning("Could not get Updated War Hits for {name} ({tag})", player.Name, player.Tag);

                        entry.UpdatedAt = DateTime.UtcNow;

                        await botDb.SaveChangesAsync(stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw; // host is shutting down — let the outer handler exit the loop
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to update PlayerStats for {name} ({tag})", player.Name, player.Tag);
                    }

                    await Task.Delay(new TimeSpan(hours: 0, minutes: 0, seconds: 5), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PlayerStatsUpdateService");
            }

            await Task.Delay(new TimeSpan(hours: 0, minutes: 15, seconds: 0), stoppingToken);
        }
    }
}
