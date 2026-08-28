using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZenBotCS.Entities;
using ZenBotCS.Entities.Models;
using ZenBotCS.Entities.Models.Enums;
using ZenBotCS.Services.SlashCommands;

namespace ZenBotCS.Services.Background;

/// <summary>
/// Periodically checks each clan that has CWL roster reminders enabled and, once per upcoming war,
/// posts a reminder if the in-game lineup still doesn't match the pinned roster within the configured
/// lead window. Dedup is persisted in ReminderStates, so a restart mid-window can't repost.
/// </summary>
public class CwlRosterReminderService(IServiceScopeFactory _serviceScopeFactory, ILogger<CwlRosterReminderService> _logger) : BackgroundService
{
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
                    .Where(cs => cs.CwlRosterReminderEnabled && cs.LeadershipChannelId != null)
                    .ToList();

                var subRosters = botDb.SubRosters.AsNoTracking().ToList();

                foreach (var settings in enabledClans)
                {
                    // The clan's own roster, plus every roster it hosts in another clan. Each plays a
                    // separate CWL, so each gets its own reminder — all posted to this clan's channel.
                    foreach (var target in ExpandTargets(settings.ClanTag, subRosters))
                    {
                        try
                        {
                            var prepWar = await rosterService.GetPreparationWar(target.GameClanTag);
                            if (prepWar is null)
                                continue;

                            if (!IsWithinLeadWindow(DateTime.UtcNow, prepWar.StartTime, settings.CwlRosterReminderLeadHours))
                                continue;

                            // One reminder per war (keyed by its start time); skip if we already posted.
                            // Keyed on the GAME clan, not the owner: two rosters keyed on the same owner
                            // tag would suppress each other and only one would ever post.
                            if (botDb.WasReminderSent(target.GameClanTag, ReminderKind.CwlRoster, prepWar.StartTime))
                                continue;

                            var embed = await rosterService.TryBuildRosterReminder(target.GameClanTag, target.RosterName);
                            if (embed is null)
                                continue; // lineup matches the roster — nothing to remind about

                            if (await discordClient.GetChannelAsync(settings.LeadershipChannelId!.Value) is not SocketTextChannel channel)
                            {
                                _logger.LogWarning("Leadership channel {channelId} not found for clan {clan}", settings.LeadershipChannelId, settings.ClanTag);
                                continue;
                            }

                            // Mentions only notify when placed in the message text, not inside an embed.
                            var ping = settings.CwlRosterReminderPingRoleId is null ? null : $"<@&{settings.CwlRosterReminderPingRoleId}>";
                            await channel.SendMessageAsync(text: ping, embed: embed);
                            botDb.MarkReminderSent(target.GameClanTag, ReminderKind.CwlRoster, prepWar.StartTime);
                        }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing CWL roster reminder for clan {clan} (roster in {gameClan})",
                                settings.ClanTag, target.GameClanTag);
                        }
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

    /// <summary>
    /// The rosters this clan is responsible for: its own (played in the clan itself, no roster name)
    /// followed by each subroster it owns, played in that subroster's host clan. Every entry is a
    /// distinct CWL and so a distinct reminder — the game clan tag is what identifies it, which is why
    /// dedup keys on it rather than on the owning clan.
    /// </summary>
    internal static List<(string GameClanTag, string? RosterName)> ExpandTargets(
        string clanTag, IEnumerable<SubRoster> subRosters)
    {
        var targets = new List<(string, string?)> { (clanTag, null) };
        targets.AddRange(subRosters
            .Where(sr => sr.ClanTag == clanTag)
            .OrderBy(sr => sr.Order)
            .Select(sr => (sr.GameClanTag, (string?)sr.Name)));
        return targets;
    }

    // True when the war starts in the future and within 'leadHours' from now.
    internal static bool IsWithinLeadWindow(DateTime nowUtc, DateTime startTimeUtc, int leadHours)
    {
        var startsIn = startTimeUtc - nowUtc;
        return startsIn > TimeSpan.Zero && startsIn <= TimeSpan.FromHours(leadHours);
    }
}
