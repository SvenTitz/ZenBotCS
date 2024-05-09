using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ZenBotCS.Services.Background
{
    public class DiscordLinkUpdateService(IServiceScopeFactory serviceScopeFactory, ILogger<DiscordLinkUpdateService> logger) : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
        private readonly ILogger<DiscordLinkUpdateService> _logger = logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var _linksService = scope.ServiceProvider.GetRequiredService<SlashCommands.LinksService>();

                    try {
                        await _linksService.Update();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in DiscordLinkUpdateService");
                    }
                }

                await Task.Delay(new TimeSpan(hours: 0, minutes: 10, seconds: 0), stoppingToken);
            }
        }

    }
}
