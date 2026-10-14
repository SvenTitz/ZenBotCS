using Discord.Interactions;
using Discord.WebSocket;
using ZenBotCS.Attributes;
using ZenBotCS.Handler;
using ZenBotCS.Models.Enums;
using ZenBotCS.Services.SlashCommands;

namespace ZenBotCS.Modules;

[Group("strike", "Track player strikes (rule-breaking warnings)")]
public class StrikeModule : InteractionModuleBase<SocketInteractionContext>
{
    public required StrikeService Service { get; set; }

    [RequireOwner(Group = "Permission")]
    [RequireLeadershipRole(Group = "Permission")]
    [SlashCommand("add", "Add a strike to a player")]
    public async Task Add(
        [Summary("player", "Player username or tag"), Autocomplete(typeof(PlayerTagAutocompleteHandler)), MaxLength(100)] string player,
        [Summary("reason", "Why this strike is being given"), MaxLength(500)] string reason,
        [Summary("rollover_days", "Days the strike stays active (omit = stays active forever)"), MinValue(1), MaxValue(365)] int? rolloverDays = null,
        [Summary("strike_weight", "Severity of the strike (default 1)"), MinValue(1), MaxValue(10)] int strikeWeight = 1)
    {
        await DeferAsync(ephemeral: true);
        var embed = await Service.Add(player, reason, rolloverDays, strikeWeight, Context.User);
        await FollowupAsync(embed: embed, ephemeral: true);
    }

    [RequireOwner(Group = "Permission")]
    [RequireLeadershipRole(Group = "Permission")]
    [SlashCommand("remove", "Remove a strike by its ID")]
    public async Task Remove(
        [Summary("strike_id", "Strike ID shown by /strike list (letters and numbers)"), MaxLength(32)] string strikeId)
    {
        await DeferAsync(ephemeral: true);
        var embed = await Service.Remove(strikeId, Context.User);
        await FollowupAsync(embed: embed, ephemeral: true);
    }

    [RequireOwner(Group = "Permission")]
    [RequireLeadershipRole(Group = "Permission")]
    [SlashCommand("list", "List strikes: grouped per player or strike by strike")]
    public async Task List(
        [Summary("view", "How to display strikes (player = grouped per account, strike = individual entries)")] StrikeListView view,
        [Summary("clan", "Only show strikes for a specific clan"), Autocomplete(typeof(ClanTagAutocompleteHandler)), MaxLength(12)] string? clan = null,
        [Summary("player", "Only show strikes of a specific player (can be combined with clan)"), Autocomplete(typeof(PlayerTagAutocompleteHandler)), MaxLength(100)] string? player = null,
        [Summary("strike_amount", "Player view only: only show players with at least this many active strikes"), MinValue(1)] int? strikeAmount = null,
        [Summary("view_expired_strikes", "Also show strikes that have rolled over")] bool viewExpiredStrikes = false)
    {
        await DeferAsync(ephemeral: true);
        var embeds = await Service.List(view, clan, player, strikeAmount, viewExpiredStrikes);
        await FollowupAsync(embeds: embeds, ephemeral: true);
    }
}
