using Discord.Interactions;
using ZenBotCS.Services.SlashCommands;

namespace ZenBotCS.Modules;

[Group("links", "Commands related to coc - discord links.")]
public class LinksModule : InteractionModuleBase<SocketInteractionContext>
{
    public required LinksService LinksService { get; set; }

    [SlashCommand("list-unlinked", "Lists all unlicked players in the family clans")]
    public async Task ListUnlinked()
    {
        await DeferAsync();
        var embed = await LinksService.ListUnlinked();
        await FollowupAsync(embed: embed);
    }

    [SlashCommand("update", "Updates CK/CP links for this bot.")]
    public async Task Update()
    {
        await DeferAsync();
        await LinksService.Update();
        await FollowupAsync("Done.");
    }

}
