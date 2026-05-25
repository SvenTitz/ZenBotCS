using System.Text;
using CocApi.Cache;
using CocApi.Rest.Models;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ZenBotCS.Clients;
using ZenBotCS.Entities;
using ZenBotCS.Helper;

namespace ZenBotCS.Services.SlashCommands;
public class GatekeepService(ClashKingApiClient _clashKingApiClient, BotDataContext _botDb, EmbedHelper _embedHelper, DiscordSocketClient _discordClient, PlayersClient _playersClient, CustomClansClient _clansClient, IConfiguration _config)
{
    public async Task<Embed[]> Lookup(SocketUser user)
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

    /// <summary>
    /// Builds and posts a gatekeep note embed to the configured channel.
    /// </summary>
    /// <returns>A confirmation string for the ephemeral followup.</returns>
    public async Task<string> Post(
        SocketUser targetUser,
        List<string> playerTags,
        List<string> clanTags,
        string reason,
        SocketUser submittedBy)
    {
        // --- Resolve player IGNs ---
        var players = new List<Player?>();
        foreach (var playerTag in playerTags)
        {
            players.Add(await _playersClient.GetOrFetchPlayerAsync(playerTag));
        }

        var ignLines = new StringBuilder();
        var tagLines = new StringBuilder();

        foreach (var playerTag in playerTags)
        {
            var player = players.FirstOrDefault(p =>
                string.Equals(p?.Tag, playerTag, StringComparison.OrdinalIgnoreCase));

            ignLines.AppendLine(player?.Name ?? "*(unknown)*");
            tagLines.AppendLine(playerTag);
        }

        // --- Resolve clan names ---
        var allClans = await _clansClient.GetCachedClansAsync();
        var clanLines = new StringBuilder();

        foreach (var clanTag in clanTags)
        {
            var clan = allClans.FirstOrDefault(c =>
                string.Equals(c.Tag, clanTag, StringComparison.OrdinalIgnoreCase));

            clanLines.AppendLine(clan is not null ? $"{clan.Name}" : clanTag);
        }

        // --- Build embed ---
        var displayName = (targetUser as SocketGuildUser)?.DisplayName ?? targetUser.Username;
        var submitterName = (submittedBy as SocketGuildUser)?.DisplayName ?? submittedBy.Username;

        var embed = new EmbedBuilder()
            .WithTitle("Leadership Note")
            .WithColor(Color.Purple)
            .WithTimestamp(DateTimeOffset.UtcNow)
            .AddField("Discord User", $"<@{targetUser.Id}> ({displayName})", inline: false)
            .AddField("IGN", ignLines.ToString().TrimEnd(), inline: true)
            .AddField("Tags", tagLines.ToString().TrimEnd(), inline: true)
            .AddField("Clan(s)", clanLines.ToString().TrimEnd(), inline: false)
            .AddField("Reason", reason, inline: false)
            .AddField("Submitted By", $"<@{submittedBy.Id}> ({submitterName})", inline: false)
            .WithImageUrl("https://cdn.discordapp.com/attachments/809874883768614922/1231630801792405704/Zen-CWL-Spacer.png?ex=6629d0d1&is=66287f51&hm=3372eb6161b41bb81bc6d89e02049e8e6ea1bc2126abb3f7bd8079306207b7c9&")
            .Build();

        // --- Post to configured channel ---
        var channelIdStr = _config["LeadershipNotesChannelId"];

        if (!ulong.TryParse(channelIdStr, out var channelId) || channelId == 0)
            return "❌ Gatekeep note channel is not configured. Please set `LeadershipNotesChannelId` in appsettings.json.";

        if (_discordClient.GetChannel(channelId) is not ITextChannel textChannel)
            return $"❌ Could not find a text channel with ID `{channelId}`. Make sure the bot has access to it.";

        await textChannel.SendMessageAsync(embed: embed);

        return $"Leadership note for **{displayName}** posted successfully in <#{channelId}>.";
    }
}
