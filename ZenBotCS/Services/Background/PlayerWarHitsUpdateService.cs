using CocApi.Cache;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZenBotCS.Clients;
using ZenBotCS.Entities;
using ZenBotCS.Entities.Models;
using ZenBotCS.Extensions;
using ZenBotCS.Services;

namespace ZenBotCS.Services.Background;

/// <summary>
/// Keeps <see cref="PlayerWarHitsCache"/> warm for every tracked player, so the commands that walk a
/// whole clan read from the DB instead of making one ClashKing call per member. Replaces the old
/// PlayerStatsUpdateService, which also pulled the player-stats payload that v2 no longer serves.
/// Follows the same scope/catch pattern as the other workers: own DI scope, log-and-continue inside
/// the loop so one bad player can't stop the host.
/// </summary>
public class PlayerWarHitsUpdateService(IServiceScopeFactory _serviceScopeFactory, ILogger<PlayerWarHitsUpdateService> _logger) : BackgroundService
{
    // Paced so a full sweep doesn't hammer the API; the loop is long-running by design.
    private static readonly TimeSpan BetweenPlayers = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan BetweenCycles = TimeSpan.FromMinutes(15);

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
                var players = chachedPlayers.Where(p => p.TownHallLevel >= 7).ToList();

                _logger.LogInformation("Pulling war hits for {count} players", players.Count);
                foreach (var player in players)
                {
                    try
                    {
                        var newWarHits = await clashKingApiClient.GetPlayerWarAttacksAsync(
                            player.Tag, ClashKingApiService.LimitDays);

                        if (newWarHits is null)
                        {
                            // Keep whatever is stored: a failed fetch is not "this player has no hits".
                            _logger.LogWarning("Could not get updated war hits for {name} ({tag})", player.Name, player.Tag);
                        }
                        else
                        {
                            var entry = botDb.PlayerWarHitsCaches.FirstOrDefault(c => c.PlayerTag == player.Tag);
                            if (entry is null)
                            {
                                entry = new PlayerWarHitsCache { PlayerTag = player.Tag };
                                botDb.PlayerWarHitsCaches.Add(entry);
                            }

                            entry.WarHits = newWarHits;
                            entry.UpdatedAt = DateTime.UtcNow;

                            await botDb.SaveChangesAsync(stoppingToken);
                        }
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw; // host is shutting down — let the outer handler exit the loop
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to update war hits for {name} ({tag})", player.Name, player.Tag);
                    }

                    await Task.Delay(BetweenPlayers, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PlayerWarHitsUpdateService");
            }

            await Task.Delay(BetweenCycles, stoppingToken);
        }
    }
}
