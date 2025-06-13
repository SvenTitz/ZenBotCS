using System.Text.RegularExpressions;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZenBotCS.Entities;
using ZenBotCS.Entities.Models;

namespace ZenBotCS.Services.Background;

public partial class LeadershipLogBackfillService(IServiceScopeFactory _serviceScopeFactory, ILogger<LeadershipLogBackfillService> _logger, IConfiguration _config) : BackgroundService
{

    private readonly Regex _playerTagRegex = PlayerTagRegex();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Delay a bit to make sure the bot is connected
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Starting message backfill at {Time}", DateTimeOffset.Now);
                await BackfillChannelAsync();
                _logger.LogInformation("Finished backfill at {Time}", DateTimeOffset.Now);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during message backfill.");
            }

            // Wait 1 hour before next run
            await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
        }
    }
    public async Task BackfillChannelAsync()
    {
        var channelId = ulong.Parse(_config["LeadershipNotesChannelId"] ?? "0");

        using var scope = _serviceScopeFactory.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<DiscordSocketClient>();
        var db = scope.ServiceProvider.GetRequiredService<BotDataContext>();

        if (client.GetChannel(channelId) is not ITextChannel textChannel)
        {
            _logger.LogWarning("Could not finde leadership log channel with Id {id}", channelId);
            return;
        }

        IMessage? lastMessage = null;
        bool shouldStop = false;
        int messageCount = 0;

        do
        {
            IEnumerable<IMessage> messages;

            if (lastMessage == null)
            {
                // Get the most recent 100 messages for the first batch
                messages = await textChannel.GetMessagesAsync(100).FlattenAsync();
            }
            else
            {
                // Get 100 messages before lastMessage.Id on subsequent batches
                messages = await textChannel.GetMessagesAsync(lastMessage.Id, Direction.Before, 100).FlattenAsync();
            }

            if (!messages.Any())
                break; // No more messages to fetch

            foreach (var message in messages)
            {
                if (db.LeadershipLogMessages.Any(m => m.Id == message.Id))
                {
                    shouldStop = true;
                    break;
                }

                var stored = await BuildStoredMessageAsync(message, db);
                if (stored.MentionedPlayerTags.Count != 0 || stored.MentionedUsers.Count != 0)
                {
                    db.LeadershipLogMessages.Add(stored);
                    await db.SaveChangesAsync();
                }
                messageCount++;
                lastMessage = message;
            }

        } while (lastMessage != null && !shouldStop);

        _logger.LogInformation("Updated {count} leadership log messages", messageCount);
    }

    private async Task<LeadershipLogMessage> BuildStoredMessageAsync(IMessage message, BotDataContext db)
    {
        var guildId = (message.Channel as IGuildChannel)?.GuildId ?? 0;

        var stored = new LeadershipLogMessage
        {
            Id = message.Id,
            ChannelId = message.Channel.Id,
            GuildId = guildId,
            Timestamp = message.Timestamp,
            FullContent = BuildFullContent(message),
        };

        // User mentions
        foreach (var userId in message.MentionedUserIds.Distinct())
        {
            var trackedUser = await db.LeadershipLogUsers.FindAsync(userId);
            if (trackedUser == null)
            {
                trackedUser = new LeadershipLogUser { Id = userId };
                db.LeadershipLogUsers.Add(trackedUser);
            }

            stored.MentionedUsers.Add(trackedUser);
        }

        // Tag mentions
        var tagMatches = _playerTagRegex.Matches(stored.FullContent);
        foreach (var match in tagMatches.Cast<Match>().Select(m => m.Value.ToUpper()).Distinct())
        {
            var trackedTag = await db.LeadershipLogPlayerTags
                .FirstOrDefaultAsync(t => t.Tag.ToUpper() == match);

            if (trackedTag == null)
            {
                trackedTag = new LeadershipLogPlayerTag { Tag = match };
                db.LeadershipLogPlayerTags.Add(trackedTag);
            }

            stored.MentionedPlayerTags.Add(trackedTag);
        }

        return stored;
    }


    private static string BuildFullContent(IMessage message)
    {
        var content = message.Content;

        if (message.Embeds != null)
        {
            foreach (var embed in message.Embeds)
            {
                content += "\n" + embed.Description;

                if (embed.Fields != null)
                {
                    foreach (var field in embed.Fields)
                    {
                        content += $"\n{field.Name}\n{field.Value}";
                    }
                }
            }
        }

        return content ?? string.Empty;
    }

    [GeneratedRegex(@"#[0289PYLQGRJCUV]{4,}", RegexOptions.IgnoreCase)]
    private static partial Regex PlayerTagRegex();
}
