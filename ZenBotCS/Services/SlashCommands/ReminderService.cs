using CocApi.Cache;
using CocApi.Rest.Models;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text;
using ZenBotCS.Entities;
using ZenBotCS.Entities.Models;
using ZenBotCS.Helper;

namespace ZenBotCS.Services.SlashCommands;

public class ReminderService(BotDataContext _botDb, ClansClient _clansClient, EmbedHelper _embedHelper, DiscordSocketClient _discordClient, ILogger<ReminderService> _logger)
{

    public async Task<Embed> MissesAdd(string clantag, SocketTextChannel channel, SocketRole? role = null)
    {
        CocApi.Rest.Models.Clan? clan;
        try
        {
            clan = await _clansClient.GetOrFetchClanAsync(clantag);
        }
        catch (Exception ex)
        {
            return _embedHelper.ErrorEmbed("Error", ex.Message);
        }

        if (_botDb.ReminderMisses.FirstOrDefault(rm => rm.ChannelId == channel.Id && rm.ClanTag == clantag) is not null)
        {
            return _embedHelper.ErrorEmbed("Error", $"Reminder for **{clan?.Name}** ({clantag}) in {channel?.Mention} already exists.");
        }

        _botDb.Add(new ReminderMisses { ChannelId = channel.Id, ClanTag = clantag, PingRoleId = role?.Id });
        _botDb.SaveChanges();

        var description = $"Add missed attacks reminder for **{clan?.Name}** ({clantag}) in {channel?.Mention}";
        if (role != null)
        {
            description += $", pinging role {role?.Mention}";
        }
        return new EmbedBuilder()
            .WithTitle("Missed Attacks Reminder Added")
            .WithDescription(description)
            .WithColor(Color.DarkPurple)
            .Build();
    }

    public async Task<Embed> MissesRemove(string clantag, SocketTextChannel channel)
    {
        CocApi.Rest.Models.Clan? clan;
        try
        {
            clan = await _clansClient.GetOrFetchClanAsync(clantag);
        }
        catch (Exception ex)
        {
            return _embedHelper.ErrorEmbed("Error", ex.Message);
        }

        var entry = _botDb.ReminderMisses.FirstOrDefault(rm => rm.ChannelId == channel.Id && rm.ClanTag == clantag);
        if (entry is null)
        {
            return _embedHelper.ErrorEmbed("Error", $"No reminder for **{clan?.Name}** ({clantag}) in {channel?.Mention} exists.");
        }

        _botDb.Remove(entry);
        _botDb.SaveChanges();

        return new EmbedBuilder()
            .WithTitle("Missed Attacks Reminder Removed")
            .WithDescription($"Removed missed attacks reminder for **{clan?.Name}** ({clantag}) in {channel?.Mention}")
            .WithColor(Color.DarkPurple)
            .Build();
    }

    public async Task<Embed> MissesList()
    {
        var description = new StringBuilder();
        foreach (var rem in _botDb.ReminderMisses.AsNoTracking())
        {
            var clan = await _clansClient.GetOrFetchClanAsync(rem.ClanTag);
            description.Append($"- **{clan?.Name}** ({rem.ClanTag}) in <#{rem.ChannelId}>");
            if (rem.PingRoleId != null)
                description.AppendLine($", pinging <@&{rem.PingRoleId}>");
            else
                description.AppendLine();
        }

        return new EmbedBuilder()
            .WithTitle("Missed Attacks Reminders")
            .WithDescription(description.ToString())
            .WithColor(Color.DarkPurple)
            .Build();
    }

    public async Task PostMissedAttacksReminderForWar(WarEventArgs e)
    {
        await PostMissedAttackReminderForClan(e.War.Clan.Tag, e.War);
        await PostMissedAttackReminderForClan(e.War.Opponent.Tag, e.War);
    }

    private async Task PostMissedAttackReminderForClan(string clantag, ClanWar war)
    {
        var reminders = _botDb.ReminderMisses.Where(rm => rm.ClanTag == clantag);

        var clan = war.Clan.Tag == clantag ? war.Clan : war.Opponent;
        var opponent = war.Clan.Tag != clantag ? war.Clan : war.Opponent;
        var memberWithMisses = clan.Members.Where(m => (m.Attacks?.Count ?? 0) < war.AttacksPerMember);

        if (!reminders.Any() || !memberWithMisses.Any())
            return;

        var recentMissesDict = GetRecentMissesCount(memberWithMisses.Select(m => m.Tag).ToList());

        var description = new StringBuilder();
        foreach (var member in memberWithMisses.OrderByDescending(m => m.Attacks?.Count ?? 0))
        {
            var discordUserId = _botDb.DiscordLinks.FirstOrDefault(dl => dl.PlayerTag == member.Tag)?.DiscordId;
            var missedCount = war.AttacksPerMember - member.Attacks?.Count ?? war.AttacksPerMember;
            description.Append($"- {missedCount}/{war.AttacksPerMember} **{member.Name}** ({member.Tag}");
            if (discordUserId is null)
                description.AppendLine(")");
            else
                description.AppendLine($", <@{discordUserId}>)");

            if (recentMissesDict[member.Tag] > 2)
            {
                description.AppendLine($"  - {recentMissesDict[member.Tag]} other attacks missed in recent* wars");
            }
        }
        description.Append($"\nWar ended: <t:{((DateTimeOffset)war.EndTime).ToUnixTimeSeconds()}:f>");

        var fieldBuilder = new EmbedFieldBuilder()
            .WithName("Missed Attacks")
            .WithValue(description.ToString())
            .WithIsInline(false);

        var embedBuilder = new EmbedBuilder()
            .WithTitle($"{clan.Name} vs {opponent.Name}")
            .WithFields(fieldBuilder)
            .WithColor(Color.DarkPurple);

        if (recentMissesDict.Values.Any(v => v > 2))
        {
            embedBuilder.WithFooter("*in the last 50 recorded wars for each family clan");
        }

        foreach (var reminder in reminders)
        {
            try
            {
                if (await _discordClient.GetChannelAsync(reminder.ChannelId) is not SocketTextChannel channel)
                {
                    _logger.LogError("No channel found with Id: {id}", reminder.ChannelId);
                    continue;
                }

                if (reminder.PingRoleId is null)
                {
                    embedBuilder.WithDescription("");
                }
                else
                {
                    embedBuilder.WithDescription($"<@&{reminder.PingRoleId}>");
                }
                var embed = embedBuilder.Build();

                await channel.SendMessageAsync(embed: embed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "reminderId: {id}", reminder.Id);
            }
        }
    }

    private Dictionary<string, int> GetRecentMissesCount(List<string> playerTags)
    {
        var result = playerTags.ToDictionary(tag => tag, tag => 0);

        foreach (var history in _botDb.WarHistories)
        {
            foreach (var warData in history.WarData ?? [])
            {
                var members = warData.Clan.Members
                                        .Union(warData.Opponent.Members)
                                        .Where(m => playerTags.Contains(m.Tag));

                foreach (var member in members)
                {
                    var missesCount = warData.AttacksPerMember - (member.Attacks?.Count ?? 0);
                    result[member.Tag] += missesCount;
                }
            }
        }

        return result;
    }



}
