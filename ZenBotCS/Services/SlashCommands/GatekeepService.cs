using System.Text;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using ZenBotCS.Clients;
using ZenBotCS.Entities;
using ZenBotCS.Helper;

namespace ZenBotCS.Services.SlashCommands;
public class GatekeepService(ClashKingApiClient _clashKingApiClient, BotDataContext _botDb, EmbedHelper _embedHelper)
{
    public async Task<Embed[]> Notes(SocketUser user)
    {
        var playerTags = await _clashKingApiClient.PostDiscordLinksAsync(user.Id);
        var normalizedTags = playerTags.Select(t => t.ToUpper()).ToList();

        // Get all message IDs where users are mentioned
        var userMessages = await _botDb.LeadershipLogUsers
            .Where(u => u.Id == user.Id)
            .Include(u => u.MessagesMentionedIn)
            .SelectMany(u => u.MessagesMentionedIn)
            .ToListAsync();

        // Get all message IDs where tags are mentioned
        var tagMessages = await _botDb.LeadershipLogPlayerTags
            .Where(t => normalizedTags.Contains(t.Tag.ToUpper()))
            .Include(t => t.MessagesMentionedIn)
            .SelectMany(t => t.MessagesMentionedIn)
            .ToListAsync();

        // Combine and remove duplicates
        var allMessages = userMessages
            .Concat(tagMessages)
            .Distinct()
            .ToList();

        var stringBuilder = new StringBuilder();

        foreach (var message in allMessages)
        {
            stringBuilder.AppendLine($"- {message.MessageLink}");
        }

        if (stringBuilder.Length <= 0)
        {
            stringBuilder.AppendLine("No notes found");
        }

        var baseEmbedBuilder = new EmbedBuilder()
            .WithTitle($"Leadership Notes for {(user as SocketGuildUser)?.DisplayName}")
            .WithColor(Color.Purple);

        return _embedHelper.BuildEmbedsFromLongDescription(stringBuilder, baseEmbedBuilder);
    }
}
