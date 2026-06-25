using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZenBotCS.Entities;
using ZenBotCS.Services.SlashCommands;

namespace ZenBotCS.Services.Background;

/// <summary>
/// Periodically checks each clan that has CWL roster reminders enabled and, once per upcoming war,
/// posts a reminder if the in-game lineup still doesn't match the pinned roster within the configured
/// lead window. Dedup is in-memory only, so a duplicate is possible if the bot restarts mid-window.
/// </summary>
public class CwlRosterReminderService(IServiceScopeFactory _serviceScopeFactory, ILogger<CwlRosterReminderService> _logger) : BackgroundService
{
    // ClanTag -> StartTime of the war we last reminded for. Reset on restart.
    private readonly Dictionary<string, string> _lastReminded = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var botDb = scope.ServiceProvider.GetRequiredService<BotDataContext>();
                var rosterService = scope.ServiceProvider.GetRequiredService<CwlRosterService>();
                var discordClient = scope.ServiceProvider.GetRequiredService<DiscordSocketClient>();

                var enabledClans = botDb.ClanSettings
                    .AsNoTracking()
                    .Where(cs => cs.CwlRosterReminderEnabled && cs.CwlRosterReminderChannelId != null)
                    .ToList();

                foreach (var settings in enabledClans)
                {
                    try
                    {
                        var prepWar = await rosterService.GetPreparationWar(settings.ClanTag);
                        if (prepWar is null)
                            continue;

                        if (!IsWithinLeadWindow(DateTime.UtcNow, prepWar.StartTime, settings.CwlRosterReminderLeadHours))
                            continue;

                        // One reminder per war (keyed by its start time); skip if we already posted.
                        var warKey = prepWar.StartTime.ToString("o");
                        if (_lastReminded.TryGetValue(settings.ClanTag, out var last) && last == warKey)
                            continue;

                        var embed = await rosterService.TryBuildRosterReminder(settings.ClanTag);
                        if (embed is null)
                            continue; // lineup matches the roster — nothing to remind about

                        if (await discordClient.GetChannelAsync(settings.CwlRosterReminderChannelId!.Value) is not SocketTextChannel channel)
                        {
                            _logger.LogWarning("CWL roster reminder channel {channelId} not found for clan {clan}", settings.CwlRosterReminderChannelId, settings.ClanTag);
                            continue;
                        }

                        // Mentions only notify when placed in the message text, not inside an embed.
                        var ping = settings.CwlRosterReminderPingRoleId is null ? null : $"<@&{settings.CwlRosterReminderPingRoleId}>";
                        await channel.SendMessageAsync(text: ping, embed: embed);
                        _lastReminded[settings.ClanTag] = warKey;
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing CWL roster reminder for clan {clan}", settings.ClanTag);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CwlRosterReminderService");
            }

            await Task.Delay(new TimeSpan(hours: 0, minutes: 15, seconds: 0), stoppingToken);
        }
    }

    // True when the war starts in the future and within 'leadHours' from now.
    internal static bool IsWithinLeadWindow(DateTime nowUtc, DateTime startTimeUtc, int leadHours)
    {
        var startsIn = startTimeUtc - nowUtc;
        return startsIn > TimeSpan.Zero && startsIn <= TimeSpan.FromHours(leadHours);
    }
}
