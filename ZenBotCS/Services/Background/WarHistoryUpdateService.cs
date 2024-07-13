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
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var clashKingApiClient = scope.ServiceProvider.GetRequiredService<ClashKingApiClient>();
                var botDb = scope.ServiceProvider.GetRequiredService<BotDataContext>();
                var clansClient = scope.ServiceProvider.GetRequiredService<CustomClansClient>();

                var cachedClans = await clansClient.GetCachedClansAsync();

                _logger.LogInformation("Pulling WarHistory for {count} clans", cachedClans.Count);
                foreach (var clan in cachedClans)
                {
                    //_logger.LogInformation("Pulling WarHistory for {name}", clan.Name);
                    var newWarData = await clashKingApiClient.GetClanWarHistory(clan.Tag);

                    var entry = botDb.WarHistories.FirstOrDefault(wh => wh.ClanTag == clan.Tag);
                    if (entry is null)
                    {
                        //_logger.LogInformation("Creating new WarHistory entry for {name}", clan.Name);
                        entry = new Entities.Models.WarHistory() { ClanTag = clan.Tag };
                        botDb.WarHistories.Add(entry);
                    }
                    //_logger.LogInformation("Updating WarHistory entry for {name}", clan.Name);
                    entry.WarData = newWarData;
                    entry.UpdatedAt = DateTime.UtcNow;

                    botDb.SaveChanges();
                }

            }

            await Task.Delay(new TimeSpan(hours: 0, minutes: 15, seconds: 0), stoppingToken);
        }
    }
}
