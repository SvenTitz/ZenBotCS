using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace ZenBotCS.Helper
{
    public class DiscordHelper
    {
        private readonly Embed[] _noMessageEmbed;
        private readonly ILogger<DiscordHelper> _logger;
        private readonly EmbedHelper _embedHelper;
        private readonly DiscordSocketClient _client;

        public DiscordHelper(ILogger<DiscordHelper> logger, EmbedHelper embedHelper, DiscordSocketClient client)
        {
            _logger = logger;
            _embedHelper = embedHelper;
            _client = client;

            _noMessageEmbed = [_embedHelper.ErrorEmbed("Error", "Message link is not a valid discord message link.")];
        }


        public async Task<(string content, Embed[] embeds)> GetMessageFromLinkAsync(string messageLink)
        {
            (ulong guildId, ulong channelId, ulong messageId) = ExtractMessageIds(messageLink);
            if (guildId is 0 || channelId is 0 || messageId is 0)
                return ("", _noMessageEmbed);

            var guild = _client.GetGuild(guildId);
            var channel = guild.GetTextChannel(channelId);
            var message = await channel.GetMessageAsync(messageId);

            if (message is null)
                return ("", _noMessageEmbed);

            return (message.Content, message.Embeds.Cast<Embed>().ToArray());
        }

        public static (ulong guildId, ulong channelId, ulong messageId) ExtractMessageIds(string messageLink)
        {
            var match = Regex.Match(messageLink, @"/channels/(\d+)/(\d+)/(\d+)");

            if (match.Success && match.Groups.Count == 4)
            {
                ulong guildId = ulong.Parse(match.Groups[1].Value);
                ulong channelId = ulong.Parse(match.Groups[2].Value);
                ulong messageId = ulong.Parse(match.Groups[3].Value);

                return (guildId, channelId, messageId);
            }

            return (0, 0, 0);
        }
    }
}
