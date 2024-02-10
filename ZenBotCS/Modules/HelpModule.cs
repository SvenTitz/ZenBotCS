using Discord.Interactions;
using ZenBotCS.Services.SlashCommands;

namespace ZenBotCS.Modules
{
    [Group("help", "Help Commands")]
    public class HelpModule : InteractionModuleBase<SocketInteractionContext>
    {
        [Group("bots", "Help Commands related to bots")]
        public class HelpBotsModule : InteractionModuleBase<SocketInteractionContext>
        {
            public required HelpService HelpService { get; set; }

            [SlashCommand("linking", "Commands related to linking player and discord accounts")]
            public async Task Linking()
            {
                await DeferAsync();
                var (content, embeds) = await HelpService.HelpBotsLinking();
                await FollowupAsync(text: content, embeds: embeds);
            }

            [SlashCommand("gatekeeper", "Commands related to the gatekeepr role")]
            public async Task Gatekeeper()
            {
                await DeferAsync();
                var (content, embeds) = await HelpService.HelpBotsGatekeeper();
                await FollowupAsync(text: content, embeds: embeds);
            }
        }

    }
}
