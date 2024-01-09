using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;

namespace ZenBotCS.Services
{
    public class HelpService
    {
        private readonly IConfiguration _config;
        private readonly EmbedHelper _embedHelper;
        private readonly DiscordSocketClient _client;

        private readonly Embed[] _missingLinkEmbeds;
        private readonly Embed[] _noMessageEmbed;

        public HelpService(IConfiguration config, EmbedHelper embedHelper, DiscordSocketClient client)
        {
            _config = config;
            _embedHelper = embedHelper;
            _client = client;

            _missingLinkEmbeds = [_embedHelper.ErrorEmbed("Error", "No message link specified in config.")];
            _noMessageEmbed = [_embedHelper.ErrorEmbed("Error", "Message link is not a valid discord message link.")];
        }

        public async Task<(string content, Embed[] embeds)> HelpBotsLinking()
        {
            var messageLink = _config["HelpBotsLinking"];
            if (messageLink == null)
                return ("", _missingLinkEmbeds);

            return await GetMessageFromLinkAsync(messageLink);
        }

        public async Task<(string content, Embed[] embeds)> HelpBotsGatekeeper()
        {
            var messageLink = _config["HelpBotsGatekeeper"];
            if (messageLink == null)
                return ("", _missingLinkEmbeds);

            return await GetMessageFromLinkAsync(messageLink);
        }

        private async Task<(string content, Embed[] embeds)> GetMessageFromLinkAsync(string messageLink)
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

        private (ulong guildId, ulong channelId, ulong messageId) ExtractMessageIds(string messageLink)
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
