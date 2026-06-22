using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZenBotCS.Clients;
using ZenBotCS.Entities;

namespace ZenBotCS.Services.Background;

public class WarHistoryUpdateService(IServiceScopeFactory serviceScopeFactory, ILogger<WarHistoryUpdateService> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
    private readonly ILogger<WarHistoryUpdateService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var clashKingApiClient = scope.ServiceProvider.GetRequiredService<ClashKingApiClient>();
                var botDb = scope.ServiceProvider.GetRequiredService<BotDataContext>();
                var clansClient = scope.ServiceProvider.GetRequiredService<CustomClansClient>();

                var cachedClans = await clansClient.GetCachedClansAsync();

                _logger.LogInformation("Pulling WarHistory for {count} clans", cachedClans.Count);
                foreach (var clan in cachedClans)
                {
                    try
                    {
                        var newWarData = await clashKingApiClient.GetClanWarHistory(clan.Tag);

                        var entry = botDb.WarHistories.FirstOrDefault(wh => wh.ClanTag == clan.Tag);
                        if (entry is null)
                        {
                            entry = new Entities.Models.WarHistory() { ClanTag = clan.Tag };
                            botDb.WarHistories.Add(entry);
                        }

                        if (newWarData is not null)
                            entry.WarData = newWarData;
                        else
                            _logger.LogWarning("Could not get Updated War Data for Clan {name} ({tag})", clan.Name, clan.Tag);

                        entry.UpdatedAt = DateTime.UtcNow;

                        await botDb.SaveChangesAsync(stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw; // host is shutting down — let the outer handler exit the loop
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to update WarHistory for clan {name} ({tag})", clan.Name, clan.Tag);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in WarHistoryUpdateService");
            }

            await Task.Delay(new TimeSpan(hours: 0, minutes: 15, seconds: 0), stoppingToken);
        }
    }
}
