using Discord.Interactions;
using Discord.WebSocket;
using ZenBotCS.Attributes;
using ZenBotCS.Handler;
using ZenBotCS.Services.SlashCommands;

namespace ZenBotCS.Modules;

[Group("gatekeep", "Commands related to gatekeeping")]
public class GatekeepModule : InteractionModuleBase<SocketInteractionContext>
{
    [Group("notes", "Commands related to gatekeep notes")]
    public class GatekeepNotesModule : InteractionModuleBase<SocketInteractionContext>
    {
        public required GatekeepService Service { get; set; }

        [RequireOwner(Group = "Permission")]
        [RequireLeadershipRole(Group = "Permission")]
        [SlashCommand("lookup", "Returns notes for the given user and all linked accounts if there are any")]
        public async Task Lookup(SocketUser user)
        {
            await DeferAsync(ephemeral: true);
            var embeds = await Service.Lookup(user);
            await FollowupAsync(embeds: embeds, ephemeral: true);
        }

        [RequireOwner(Group = "Permission")]
        [RequireLeadershipRole(Group = "Permission")]
        [SlashCommand("post", "Submit a gatekeep note for a player")]
        public async Task Post(
            [Summary("User", "The Discord user to gatekeep")] SocketUser user,
            [Summary("Tag1", "Player tag #1 (required)"), Autocomplete(typeof(PlayerTagAutocompleteHandler))] string tag1,
            [Summary("Reason", "Reason for the gatekeep")] string reason,
            [Summary("Clan1", "Clan tag #1 (required)"), Autocomplete(typeof(ClanTagAutocompleteHandler))] string clan1,
            [Summary("Tag2", "Player tag #2 (optional)"), Autocomplete(typeof(PlayerTagAutocompleteHandler))] string? tag2 = null,
            [Summary("Tag3", "Player tag #3 (optional)"), Autocomplete(typeof(PlayerTagAutocompleteHandler))] string? tag3 = null,
            [Summary("Tag4", "Player tag #4 (optional)"), Autocomplete(typeof(PlayerTagAutocompleteHandler))] string? tag4 = null,
            [Summary("Tag5", "Player tag #5 (optional)"), Autocomplete(typeof(PlayerTagAutocompleteHandler))] string? tag5 = null,
            [Summary("Clan2", "Clan tag #2 (optional)"), Autocomplete(typeof(ClanTagAutocompleteHandler))] string? clan2 = null,
            [Summary("Clan3", "Clan tag #3 (optional)"), Autocomplete(typeof(ClanTagAutocompleteHandler))] string? clan3 = null,
            [Summary("Clan4", "Clan tag #4 (optional)"), Autocomplete(typeof(ClanTagAutocompleteHandler))] string? clan4 = null,
            [Summary("Clan5", "Clan tag #5 (optional)"), Autocomplete(typeof(ClanTagAutocompleteHandler))] string? clan5 = null)
        {
            await DeferAsync(ephemeral: true);

            var playerTags = new List<string> { tag1 };
            if (tag2 is not null) playerTags.Add(tag2);
            if (tag3 is not null) playerTags.Add(tag3);
            if (tag4 is not null) playerTags.Add(tag4);
            if (tag5 is not null) playerTags.Add(tag5);

            var clanTags = new List<string> { clan1 };
            if (clan2 is not null) clanTags.Add(clan2);
            if (clan3 is not null) clanTags.Add(clan3);
            if (clan4 is not null) clanTags.Add(clan4);
            if (clan5 is not null) clanTags.Add(clan5);

            var result = await Service.Post(user, playerTags, clanTags, reason, Context.User);

            await FollowupAsync(result, ephemeral: true);
        }
    }
}
