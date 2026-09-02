using Discord.Interactions;
using Discord.WebSocket;
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

    // (Re-enabling either block below also needs `using ZenBotCS.Attributes;` back.)
    // Disabled: /links add was the break-glass path for when the link endpoint was down and the
    // table had nothing cached. The v2 endpoint is reliable and Update() now prunes properly, so a
    // hand-written row would only be deleted again on the next run. LinksService.Add stays in place
    // -- re-enabling is uncommenting this block.
    //[RequireOwner(Group = "Permission")]
    //[RequireLeadershipRole(Group = "Permission")]
    //[SlashCommand("add", "Stores a coc - discord link in the bot's own database")]
    //public async Task Add(
    //    [Summary("PlayerTag"), Autocomplete(typeof(PlayerTagAutocompleteHandler))] string playerTag,
    //    [Summary("User", "The Discord user the account belongs to")] SocketUser user)
    //{
    //    await DeferAsync();
    //    var embed = await LinksService.Add(playerTag, user);
    //    await FollowupAsync(embed: embed);
    //}

    // Disabled alongside /links add: Update() now prunes unlinked rows on its own, and a row
    // ClashKing still knows about comes straight back on the next run anyway.
    //[RequireOwner(Group = "Permission")]
    //[RequireLeadershipRole(Group = "Permission")]
    //[SlashCommand("remove", "Removes a coc - discord link from the bot's own database")]
    //public async Task Remove(
    //    [Summary("PlayerTag"), Autocomplete(typeof(PlayerTagAutocompleteHandler))] string playerTag)
    //{
    //    await DeferAsync();
    //    var embed = LinksService.Remove(playerTag);
    //    await FollowupAsync(embed: embed);
    //}

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
