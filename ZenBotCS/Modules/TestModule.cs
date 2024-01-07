using CocApi.Rest.Apis;
using Discord;
using Discord.Interactions;
using ZenBotCS.Handler;
using ZenBotCS.Services;

namespace ZenBotCS;


public class TestModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("ping", "ping the bot")]
    public async Task Ping()
    {
        var embed = new EmbedBuilder()
            .WithTitle(":ping_pong: Pong!")
            .WithDescription($"The bot latency is {Context.Client.Latency}ms.")
            .WithColor(0x9C84EF)
            .Build();
        await RespondAsync(embed: embed);
    }
}

