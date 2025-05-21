using CocApi.Cache.Services;
using Discord.Interactions;
using Discord.WebSocket;
using ZenBotCS.Entities.Models.Enums;
using ZenBotCS.Handler;
using ZenBotCS.Models.Enums;
using ZenBotCS.Services.SlashCommands;

namespace ZenBotCS.Modules;

[Group("clan", "Commands to add, remove or get info about clans.")]
public class ClanModule : InteractionModuleBase<SocketInteractionContext>
{
    public required Services.SlashCommands.ClanService ClanService { get; set; }

    [RequireUserPermission(Discord.GuildPermission.Administrator)]
    [SlashCommand("add", "Add a Clan to the bot.")]
    public async Task Add(string clantag)
    {
        await DeferAsync();
        var embed = await ClanService.Add(clantag);
        await FollowupAsync(embed: embed);
    }

    [RequireUserPermission(Discord.GuildPermission.Administrator)]
    [SlashCommand("delete", "Delete a Clan from the bot.")]
    public async Task Delete(string clantag)
    {
        await DeferAsync();
        var embed = await ClanService.Delete(clantag);
        await FollowupAsync(embed: embed);
    }

    [SlashCommand("list", "Lists all Clans")]
    public async Task List()
    {
        await DeferAsync();
        var embed = await ClanService.List();
        await FollowupAsync(embed: embed);
    }

    [SlashCommand("warlog", "Fetch a clans available warlog.")]
    public async Task Warlog(
        [Summary("ClanTag"), Autocomplete(typeof(ClanTagAutocompleteHandler))] string clantag,
        [Summary("IncludeCWL")] bool includeCWl = false)
    {
        await DeferAsync();
        var embed = await ClanService.Warlog(clantag, includeCWl);
        await FollowupAsync(embed: embed);
    }

    [Group("stats", "Commands related to clan stats")]
    public class ClanStatsModule : InteractionModuleBase<SocketInteractionContext>
    {
        public required Services.SlashCommands.ClanService ClanService { get; set; }

        [SlashCommand("attacks", "Get a breakdown of players war attacks")]
        public async Task Attacks
        (
            [Summary("ClanTag"), Autocomplete(typeof(ClanTagAutocompleteHandler))]
                string clanTag,
            [Summary("AttackStatFilter", "Determin which stat you are looking for (default = Even3Star)")]
                AttackStatFilter attackStatFilter = AttackStatFilter.Even3Star,
            [Summary("WarTypeFilter", "Filter between Regular wars and CWL (default = RegularAndCWL)")]
                WarTypeFilter warTypeFilter = WarTypeFilter.RegularAndCWL,
            [Summary("LimitWars", "Limits the stats to the last X wars (max = default = 50)")]
                uint limitWars = 50,
            [Summary("LimitDays", "Limits the stats to the last X Days (default = 90)")]
                uint limitDays = 90,
            [Summary("MinNumberOfAttacks", "Minimum Number of attacks need to display stats (default = 4)")]
                uint minNumberAttacks = 4,
            [Summary("PlayerTownhall", "Let's you only show stats for a certain TH. No entry = all THs (default = all THs)"), Autocomplete(typeof(TownHallAutocompleteHandler))]
                int? playerTh = null,
            [Summary("ExclusivelyClanAttacks", "Only use attacks done in this clan vs. attacks done in any clan (default = False)")]
                bool clanExclusive = false)
        {
            await DeferAsync();
            var embed = await ClanService.StatsAttacks(clanTag, attackStatFilter, warTypeFilter, limitWars, limitDays, minNumberAttacks, clanExclusive, playerTh);
            await FollowupAsync(embed: embed);
        }
    }

    [Group("settings", "Commands related to clan settings")]
    public class ClanSettingsModule : InteractionModuleBase<SocketInteractionContext>
    {
        public required Services.SlashCommands.ClanService ClanService { get; set; }

        [RequireUserPermission(Discord.GuildPermission.Administrator)]
        [SlashCommand("edit", "Add or Edit Clan settings.")]
        public async Task Edit
            (
                [Summary("ClanTag"), Autocomplete(typeof(ClanTagAutocompleteHandler))] string clanTag,
                ClanType? clanType = null,
                int order = 1,
                SocketRole? memberRole = null,
                SocketRole? elderRole = null,
                SocketRole? leadershipRole = null,
                SocketRole? cwlRole = null,
                string? colorHex = null,
                bool? enableCwlSignup = null,
                bool? enableChampStyleSignup = null,
                bool? isCcGoldDumpClan = null
            )
        {
            await DeferAsync();
            var embed = await ClanService.SettingsEdit(clanTag, clanType, order, memberRole, elderRole, leadershipRole, cwlRole, colorHex, enableCwlSignup, enableChampStyleSignup, isCcGoldDumpClan);
            await FollowupAsync(embed: embed);
        }

        [RequireUserPermission(Discord.GuildPermission.Administrator)]
        [SlashCommand("reset", "Reset Clan settings.")]
        public async Task Reset([Summary("ClanTag"), Autocomplete(typeof(ClanTagAutocompleteHandler))] string clanTag)
        {
            await DeferAsync();
            var embed = ClanService.SettingsReset(clanTag);
            await FollowupAsync(embed: embed);
        }
    }
}