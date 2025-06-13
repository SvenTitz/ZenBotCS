using Discord.Interactions;
using Discord.WebSocket;
using ZenBotCS.Attributes;
using ZenBotCS.Services.SlashCommands;

namespace ZenBotCS.Modules;

[Group("gatekeep", "Commands related to gatekeeping")]
public class GatekeepModule : InteractionModuleBase<SocketInteractionContext>
{
    public required GatekeepService Service { get; set; }

    [RequireOwner(Group = "Permission")]
    [RequireLeadershipRole(Group = "Permission")]
    [SlashCommand("notes", "returns notes for the given user and all linked accounts if there are any")]
    public async Task Notes(SocketUser user)
    {
        await DeferAsync();
        var embeds = await Service.Notes(user);
        await FollowupAsync(embeds: embeds, ephemeral: true);
    }
}
