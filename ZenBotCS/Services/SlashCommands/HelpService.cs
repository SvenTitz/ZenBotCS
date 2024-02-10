using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using ZenBotCS.Helper;

namespace ZenBotCS.Services.SlashCommands
{
    public class HelpService
    {
        private readonly IConfiguration _config;
        private readonly EmbedHelper _embedHelper;
        private readonly DiscordHelper _discordHelper;

        private readonly Embed[] _missingLinkEmbeds;

        public HelpService(IConfiguration config, EmbedHelper embedHelper, DiscordSocketClient client, DiscordHelper discordHelper)
        {
            _config = config;
            _embedHelper = embedHelper;
            _discordHelper = discordHelper;

            _missingLinkEmbeds = [_embedHelper.ErrorEmbed("Error", "No message link specified in config.")];
        }

        public async Task<(string content, Embed[] embeds)> HelpBotsLinking()
        {
            var messageLink = _config["HelpBotsLinking"];
            if (messageLink == null)
                return ("", _missingLinkEmbeds);

            return await _discordHelper.GetMessageFromLinkAsync(messageLink);
        }

        public async Task<(string content, Embed[] embeds)> HelpBotsGatekeeper()
        {
            var messageLink = _config["HelpBotsGatekeeper"];
            if (messageLink == null)
                return ("", _missingLinkEmbeds);

            return await _discordHelper.GetMessageFromLinkAsync(messageLink);
        }

    }
}
