using CocApi.Rest.Apis;
using Discord;
using Discord.Interactions;
using ZenBotCS.Handler;
using ZenBotCS.Services;

namespace ZenBotCS;


public class TestModule : InteractionModuleBase<SocketInteractionContext>
{
    public required TestService TestService { get; set; }
    public required ClashKingApiClient ClashKingApiClient { get; set; }

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

    [SlashCommand("test", "a test command")]
    public async Task Test()
    {
       
        await RespondAsync(text: await TestService.TestReturn());
    }

    [SlashCommand("add_clan", "adds a clan")]
    public async Task AddClan(string clanTag)
    {
        await RespondAsync(text: await TestService.AddClan(clanTag));
    }

    [SlashCommand("test_api", "test clash king api")]
    public async Task TestApi()
    {
        await DeferAsync();
        var text = await ClashKingApiClient.Test();
        await FollowupAsync(text: text);
    }

    [SlashCommand("test_cwl_data", "test cwl data")]
    public async Task TestCwlData(
        [Summary("PlayerTag"), Autocomplete(typeof(PlayerTagAutocompleteHandler))] string playerTag)
    {
        await DeferAsync();
        var embed = await TestService.Cwl_Data_Test(playerTag);
        await FollowupAsync(embed: embed);

    }
}

