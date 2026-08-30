using Discord.Interactions;
using Discord.WebSocket;
using ZenBotCS.Attributes;
using ZenBotCS.Handler;
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

    [RequireOwner(Group = "Permission")]
    [RequireLeadershipRole(Group = "Permission")]
    [SlashCommand("add", "Stores a coc - discord link in the bot's own database")]
    public async Task Add(
        [Summary("PlayerTag"), Autocomplete(typeof(PlayerTagAutocompleteHandler))] string playerTag,
        [Summary("User", "The Discord user the account belongs to")] SocketUser user)
    {
        await DeferAsync();
        var embed = await LinksService.Add(playerTag, user);
        await FollowupAsync(embed: embed);
    }

    [RequireOwner(Group = "Permission")]
    [RequireLeadershipRole(Group = "Permission")]
    [SlashCommand("remove", "Removes a coc - discord link from the bot's own database")]
    public async Task Remove(
        [Summary("PlayerTag"), Autocomplete(typeof(PlayerTagAutocompleteHandler))] string playerTag)
    {
        await DeferAsync();
        var embed = LinksService.Remove(playerTag);
        await FollowupAsync(embed: embed);
    }

    [SlashCommand("lookup", "Shows what the bot's own database has linked for a user or a player tag")]
    public async Task Lookup(
        [Summary("User", "Look up all accounts linked to this user")] SocketUser? user = null,
        [Summary("PlayerTag", "Look up the user linked to this tag"), Autocomplete(typeof(PlayerTagAutocompleteHandler))] string? playerTag = null)
    {
        await DeferAsync();
        var embed = await LinksService.Lookup(user, playerTag);
        await FollowupAsync(embed: embed);
    }

}
