using Discord.Interactions;
using Discord.WebSocket;
using ZenBotCS.Handler;
using ZenBotCS.Services.SlashCommands;

namespace ZenBotCS.Modules;

[Group("reminder", "Commands related setting up reminders")]
public class ReminderModule : InteractionModuleBase<SocketInteractionContext>
{
    [Group("misses", "Commands related to missed attacks reminder posts")]
    public class Misses : InteractionModuleBase<SocketInteractionContext>
    {
        public required ReminderService ReminderService { get; set; }

        [SlashCommand("add", "Add a missed attack reminder for a clan")]
        public async Task Add(
            [Summary("ClanTag"), Autocomplete(typeof(ClanTagAutocompleteHandler))] string clantag,
            SocketTextChannel? channel = null,
            SocketRole? role = null)
        {
            await DeferAsync(ephemeral: true);
            channel ??= Context.Channel as SocketTextChannel;
            var embed = await ReminderService.MissesAdd(clantag, channel!, role);
            await FollowupAsync(embed: embed);
        }

        [SlashCommand("remove", "Remove a missed attack reminder for a clan")]
        public async Task Remove(
            [Summary("ClanTag"), Autocomplete(typeof(ClanTagAutocompleteHandler))] string clantag,
            SocketTextChannel? channel = null)
        {
            await DeferAsync(ephemeral: true);
            channel ??= Context.Channel as SocketTextChannel;
            var embed = await ReminderService.MissesRemove(clantag, channel!);
            await FollowupAsync(embed: embed);
        }

        [SlashCommand("list", "lists all missed attack reminders")]
        public async Task List()
        {
            await DeferAsync();
            var embed = await ReminderService.MissesList();
            await FollowupAsync(embed: embed);
        }
    }
}
