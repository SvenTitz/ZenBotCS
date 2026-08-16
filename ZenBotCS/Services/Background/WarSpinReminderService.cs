using CocApi.Cache;
using CocApi.Rest.Apis;
using CocApi.Rest.Models;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZenBotCS.Entities;
using ZenBotCS.Entities.Models;
using ZenBotCS.Entities.Models.Enums;
using ZenBotCS.Helper;

namespace ZenBotCS.Services.Background;

internal enum SpinReminderDecision
{
    /// <summary>Nothing to do yet — re-evaluate on a later cycle.</summary>
    Wait,
    Ping,
    /// <summary>Slot is handled without posting; record it so it isn't looked at again.</summary>
    MarkSilent
}

/// <summary>
/// Reminds leadership about the family war spin schedule (see <see cref="WarSpinSchedule"/>):
/// an unpinged heads-up an hour before a mandatory war, and a ping half an hour after a spin the
/// clan appears to have missed.
///
/// The "missed it" signal is deliberately narrow. Supercell's API can't distinguish a clan that is
/// still searching from one that never started, since both read <see cref="WarState.NotInWar"/>, so
/// only <see cref="WarState.WarEnded"/> — the previous war still sitting in the war slot, untouched —
/// counts as forgetting to spin. That trades away the first spin after CWL (the slot is empty, so it
/// reads NotInWar) for never pinging a clan that is mid-search.
/// </summary>
public class WarSpinReminderService(IServiceScopeFactory _serviceScopeFactory, ILogger<WarSpinReminderService> _logger) : BackgroundService
{
    // How long after a scheduled spin a clan is considered to have forgotten.
    internal static readonly TimeSpan CheckDelay = TimeSpan.FromMinutes(30);

    // A previous war that overran pushes the check to 30 minutes after it ends — but only if it ends
    // within this window, past which the clan's schedule has drifted too far for a reminder to help.
    internal static readonly TimeSpan MaxDeferral = TimeSpan.FromHours(6);

    // If the bot was down over the due moment, don't post a stale ping hours after the fact.
    internal static readonly TimeSpan GracePeriod = TimeSpan.FromHours(2);

    // How far ahead of a mandatory spin the heads-up goes out.
    internal static readonly TimeSpan MandoLeadTime = TimeSpan.FromHours(1);

    // Covers a spin deferred by the full MaxDeferral and then its grace period.
    private static readonly TimeSpan SlotLookback = TimeSpan.FromHours(12);

    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var botDb = scope.ServiceProvider.GetRequiredService<BotDataContext>();
                var discordClient = scope.ServiceProvider.GetRequiredService<DiscordSocketClient>();
                var clansClient = scope.ServiceProvider.GetRequiredService<ClansClient>();
                var clansApi = scope.ServiceProvider.GetRequiredService<IClansApi>();

                var enabledClans = botDb.ClanSettings
                    .AsNoTracking()
                    .Where(cs => cs.WarSpinReminderEnabled && cs.LeadershipChannelId != null)
                    .ToList();

                foreach (var settings in enabledClans)
                {
                    try
                    {
                        await PostMandoWarningIfDue(settings, botDb, discordClient, clansClient);
                        await PostSpinReminderIfDue(settings, botDb, discordClient, clansClient, clansApi);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing war spin reminder for clan {clan}", settings.ClanTag);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in WarSpinReminderService");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task PostMandoWarningIfDue(ClanSettings settings, BotDataContext botDb, DiscordSocketClient discordClient, ClansClient clansClient)
    {
        var utcNow = DateTime.UtcNow;

        var nextSpin = WarSpinSchedule.Upcoming(utcNow).FirstOrDefault();
        if (!nextSpin.IsMandatory || !ShouldPostMandoWarning(utcNow, nextSpin.StartUtc))
            return;

        if (botDb.WasReminderSent(settings.ClanTag, ReminderKind.MandoWarning, nextSpin.StartUtc))
            return;

        var channel = await GetLeadershipChannel(settings, discordClient);
        if (channel is null)
            return;

        var clanName = (await clansClient.GetOrFetchClanAsync(settings.ClanTag))?.Name ?? settings.ClanTag;
        var spinTimestamp = ((DateTimeOffset)nextSpin.StartUtc).ToUnixTimeSeconds();

        await channel.SendMessageAsync($"**{clanName}** - the next war (<t:{spinTimestamp}:F>) is probably a **mandatory** war.");
        botDb.MarkReminderSent(settings.ClanTag, ReminderKind.MandoWarning, nextSpin.StartUtc);
    }

    private async Task PostSpinReminderIfDue(
        ClanSettings settings,
        BotDataContext botDb,
        DiscordSocketClient discordClient,
        ClansClient clansClient,
        IClansApi clansApi)
    {
        var utcNow = DateTime.UtcNow;

        var spin = WarSpinSchedule.MostRecent(utcNow, SlotLookback);
        if (spin is null || utcNow < spin.Value.StartUtc + CheckDelay)
            return;

        if (botDb.WasReminderSent(settings.ClanTag, ReminderKind.WarSpin, spin.Value.StartUtc))
            return;

        // The cache lags by its TTL and keeps serving wars it already raised ClanWarEnded for, which
        // is exactly the distinction this decision rests on. Read the live war instead.
        var war = await FetchCurrentWarOrDefaultAsync(clansApi, settings.ClanTag);
        if (war?.State is null)
            return; // transient failure or a private war log — leave the slot for the next cycle

        var decision = Decide(utcNow, spin.Value.StartUtc, war.State.Value, war.EndTime);
        if (decision is SpinReminderDecision.Wait)
            return;

        if (decision is SpinReminderDecision.Ping)
        {
            var channel = await GetLeadershipChannel(settings, discordClient);
            if (channel is null)
                return;

            var clanName = (await clansClient.GetOrFetchClanAsync(settings.ClanTag))?.Name ?? settings.ClanTag;
            var spinTimestamp = ((DateTimeOffset)spin.Value.StartUtc).ToUnixTimeSeconds();

            // Mentions only notify from the message text, not from inside an embed.
            var ping = settings.LeaderRoleId is null ? "" : $"<@&{settings.LeaderRoleId}> - ";
            await channel.SendMessageAsync($"{ping}**{clanName}** hasn't spun yet. Scheduled spin was <t:{spinTimestamp}:t>.");
        }

        botDb.MarkReminderSent(settings.ClanTag, ReminderKind.WarSpin, spin.Value.StartUtc);
    }

    /// <summary>The heads-up goes out in the hour before the spin; afterwards it's too late to be useful.</summary>
    internal static bool ShouldPostMandoWarning(DateTime nowUtc, DateTime spinUtc)
    {
        return nowUtc >= spinUtc - MandoLeadTime && nowUtc < spinUtc;
    }

    /// <summary>
    /// Decides what to do about a scheduled spin, given the clan's live war state. Pure so the timing
    /// rules can be tested without Discord or the CoC API.
    /// </summary>
    internal static SpinReminderDecision Decide(DateTime nowUtc, DateTime spinUtc, WarState state, DateTime? warEndUtc)
    {
        var due = spinUtc + CheckDelay;
        if (nowUtc < due)
            return SpinReminderDecision.Wait;

        switch (state)
        {
            // Matched, so they spun.
            case WarState.Preparation:
                return SpinReminderDecision.MarkSilent;

            // A previous war that overran the schedule. Wait for it to end, unless it ends so late
            // that the clan's rotation has drifted off the family schedule anyway.
            case WarState.InWar when warEndUtc > nowUtc:
                return warEndUtc.Value + CheckDelay > due + MaxDeferral
                    ? SpinReminderDecision.MarkSilent
                    : SpinReminderDecision.Wait;

            // The previous war is still sitting in the war slot untouched — nobody has hit search.
            case WarState.WarEnded:
                var effectiveDue = warEndUtc is null
                    ? due
                    : Later(due, warEndUtc.Value + CheckDelay);

                if (effectiveDue > due + MaxDeferral)
                    return SpinReminderDecision.MarkSilent;
                if (nowUtc < effectiveDue)
                    return SpinReminderDecision.Wait;

                return nowUtc <= effectiveDue + GracePeriod
                    ? SpinReminderDecision.Ping
                    : SpinReminderDecision.MarkSilent;

            // NotInWar covers both an idle clan and one that is mid-search, so stay quiet.
            default:
                return SpinReminderDecision.MarkSilent;
        }
    }

    private static DateTime Later(DateTime a, DateTime b) => a > b ? a : b;

    private async Task<SocketTextChannel?> GetLeadershipChannel(ClanSettings settings, DiscordSocketClient discordClient)
    {
        if (await discordClient.GetChannelAsync(settings.LeadershipChannelId!.Value) is SocketTextChannel channel)
            return channel;

        _logger.LogWarning("Leadership channel {channelId} not found for clan {clan}", settings.LeadershipChannelId, settings.ClanTag);
        return null;
    }

    private async Task<ClanWar?> FetchCurrentWarOrDefaultAsync(IClansApi clansApi, string clanTag)
    {
        try
        {
            var response = await clansApi.FetchCurrentWarAsync(clanTag);
            return response.IsSuccessStatusCode && response.TryOk(out var war) ? war : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch the live current war for clan {clan}", clanTag);
            return null;
        }
    }
}
