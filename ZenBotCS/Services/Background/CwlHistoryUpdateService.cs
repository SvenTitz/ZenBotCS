using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZenBotCS.Clients;
using ZenBotCS.Entities;
using ZenBotCS.Entities.Models;
using ZenBotCS.Entities.Models.Cwl;

namespace ZenBotCS.Services.Background;

/// <summary>
/// Daily snapshot of each managed clan's CWL performance into <see cref="CwlHistory"/>. Computes the
/// same metrics the website shows (from ClashKing's <c>/war/{tag}/previous</c> history, grouped into
/// CWL instances), and — for the current CWL only — stamps each player's <see cref="CwlSignup.Bonus"/>
/// while the signups are still live (bonus can't be recovered from war data later). Finished CWLs are
/// immutable, so older instances are inserted once and never overwritten (never clobbering a bonus
/// captured while they were current). Mirrors <c>WarHistoryUpdateService</c>'s scope/catch pattern.
/// </summary>
public class CwlHistoryUpdateService(IServiceScopeFactory serviceScopeFactory, ILogger<CwlHistoryUpdateService> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
    private readonly ILogger<CwlHistoryUpdateService> _logger = logger;

    // A CWL whose first war started within this window is "current" (owned by the website's live/cache
    // path); the bot persists only finished CWLs. Matches the website's CurrentWindow.
    private static readonly TimeSpan CurrentWindow = TimeSpan.FromDays(9);

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

                _logger.LogInformation("Pulling CwlHistory for {count} clans", cachedClans.Count);
                foreach (var clan in cachedClans)
                {
                    try
                    {
                        await UpdateClanAsync(clashKingApiClient, botDb, clan.Tag, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw; // host is shutting down — let the outer handler exit the loop
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to update CwlHistory for clan {name} ({tag})", clan.Name, clan.Tag);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CwlHistoryUpdateService");
            }

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private static async Task UpdateClanAsync(ClashKingApiClient client, BotDataContext botDb, string clanTag, CancellationToken ct)
    {
        var wars = await client.GetClanWarHistory(clanTag, limit: 500);
        if (wars is null)
            return;

        var instances = CwlPerformanceCalculator.GroupIntoCwlInstances(wars); // newest first
        if (instances.Count == 0)
            return;

        var currentCutoff = DateTime.UtcNow - CurrentWindow;

        foreach (var instanceWars in instances)
        {
            // Skip the current, in-progress CWL: /war/previous delivers its rounds incompletely. The
            // website sources the live CWL from the CoC cache instead; the bot only persists finished
            // (immutable) CWLs, inserted once.
            if (CwlPerformanceCalculator.GroupStart(instanceWars) >= currentCutoff)
                continue;

            var performance = CwlPerformanceCalculator.Compute(clanTag, instanceWars, _ => false);
            if (performance.Players.Count == 0)
                continue;

            var exists = botDb.CwlHistories.Any(
                h => h.ClanTag == clanTag && h.Season == performance.Season && h.StartTime == performance.StartTime);
            if (!exists)
            {
                botDb.CwlHistories.Add(new CwlHistory
                {
                    ClanTag = clanTag,
                    Season = performance.Season,
                    StartTime = performance.StartTime,
                    Performance = performance,
                    UpdatedAt = DateTime.UtcNow,
                });
                await botDb.SaveChangesAsync(ct);
            }
        }
    }
}
