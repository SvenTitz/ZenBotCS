using Discord;
using Discord.Interactions;
using ZenBotCS.Models.Enums;
using ZenBotCS.Services.SlashCommands;

namespace ZenBotCS;


public class UtilModule : InteractionModuleBase<SocketInteractionContext>
{
    public required UtilService UtilService { get; set; }

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

    [SlashCommand("timestamp", "creates a discord timestamp of the given time/date")]
    public async Task Timestamp(TimeZoneEnum timeZone, string time, int? day = null, int? month = null, int? year = null)
    {
        var message = UtilService.Timestamp(timeZone, time, day, month, year);
        await RespondAsync(message, ephemeral: true);
    }

    [SlashCommand("spintimes", "returns a list of the next spin times and mandatory wars")]
    public async Task SpinTimes()
    {
        var embed = UtilService.SpinTimes();
        await RespondAsync(embed: embed);
    }

}

