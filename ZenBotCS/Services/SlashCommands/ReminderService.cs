using CocApi.Cache;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using System.Text;
using ZenBotCS.Entities;
using ZenBotCS.Entities.Models;
using ZenBotCS.Helper;

namespace ZenBotCS.Services.SlashCommands;

public class ReminderService(BotDataContext _botDb, ClansClient _clansClient, EmbedHelper _embedHelper)
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



}
