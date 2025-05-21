using CocApi.Rest.Models;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Text;
using System.Text.RegularExpressions;
using ZenBotCS.Clients;
using ZenBotCS.Entities;
using ZenBotCS.Entities.Models;
using ZenBotCS.Entities.Models.Enums;
using ZenBotCS.Helper;
using ZenBotCS.Models;
using ZenBotCS.Models.Enums;

namespace ZenBotCS.Services.SlashCommands;

public partial class ClanService(CustomClansClient _clansClient, ClashKingApiClient _clashKingApiClient, ClashKingApiService _clashKingApiService, EmbedHelper _embedHelper, BotDataContext _botDb)
{
    [GeneratedRegex("^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{8})$")]
    private static partial Regex ColorHexRegex();
    public async Task<Embed> Add(string clanTag)
    {
        try
        {
            var clan = await _clansClient.GetOrFetchClanAsync(clanTag);

            if (clan == null)
                throw new ArgumentException("The tag you provided does not seem to be valid.");

            await _clansClient.AddOrUpdateAsync(clanTag, downloadMembers: true);

            return new EmbedBuilder()
                .WithTitle("Success")
                .WithColor(Color.DarkGreen)
                .WithDescription($"Successfully added **{clan.Name}** ({clan.Tag}).")
                .WithThumbnailUrl(clan.BadgeUrls.Small)
                .Build();
        }
        catch (Exception ex)
        {
            return new EmbedBuilder()
                    .WithTitle("Error adding Clan")
                    .WithColor(Color.Red)
                    .WithDescription(ex.Message)
                    .Build();
        }
    }

    public async Task<Embed> Delete(string clanTag)
    {
        try
        {
            var cachedClan = await _clansClient.GetCachedClanOrDefaultAsync(clanTag);

            if (cachedClan == null)
                throw new ArgumentException("Either clan tag is invalid or clan has not been added to bot.");

            await _clansClient.DeleteAsync(clanTag);

            return new EmbedBuilder()
                .WithTitle("Success")
                .WithColor(Color.DarkGreen)
                .WithDescription($"Successfully deleted **{cachedClan.Content?.Name}** ({cachedClan.Tag}).")
                .WithThumbnailUrl(cachedClan.Content?.BadgeUrls.Small)
                .Build();
        }
        catch (Exception ex)
        {
            return new EmbedBuilder()
                    .WithTitle("Error deleting Clan")
                    .WithColor(Color.Red)
                    .WithDescription(ex.Message)
                    .Build();
        }
    }

    public async Task<Embed> List()
    {
        try
        {
            var clansGroupedByType = await GetCachedClansByClanType();

            var embedBuilder = new EmbedBuilder()
                .WithTitle("Reddit Zen Dynasty Clans:")
                .WithColor(Color.DarkPurple);

            AddClansFieldByType(embedBuilder, ClanType.War, clansGroupedByType);
            AddClansFieldByType(embedBuilder, ClanType.FWA, clansGroupedByType);
            AddClansFieldByType(embedBuilder, ClanType.Event, clansGroupedByType);
            AddClansFieldByType(embedBuilder, ClanType.Partner, clansGroupedByType);
            AddClansFieldByType(embedBuilder, ClanType.Other, clansGroupedByType);
            await AddCcDumpClans(embedBuilder);

            return embedBuilder.Build();
        }
        catch (Exception ex)
        {
            return new EmbedBuilder()
                    .WithTitle("Error listing Clans")
                    .WithColor(Color.Red)
                    .WithDescription(ex.Message)
                    .Build();
        }

    }

    private async Task<Dictionary<ClanType, List<Clan>>> GetCachedClansByClanType()
    {
        var clans = await _clansClient.GetCachedClansAsync();
        var clanSettings = _botDb.ClanSettings.AsNoTracking();

        var clanSettingsDict = clanSettings.ToDictionary(cs => cs.ClanTag, cs => cs);

        var clansWithSettings = clans.Where(c => clanSettingsDict.ContainsKey(c.Tag));
        var clansWithoutSettings = clans.Where(c => !clanSettingsDict.ContainsKey(c.Tag));

        var clansGroupedByType = clansWithSettings
            .GroupBy(c => clanSettingsDict[c.Tag].ClanType)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(c => clanSettingsDict[c.Tag].Order).ToList()
            );

        if (clansWithoutSettings.Any())
        {
            if (!clansGroupedByType.ContainsKey(ClanType.Other))
            {
                clansGroupedByType[ClanType.Other] = [];
            }
            clansGroupedByType[ClanType.Other].AddRange(clansWithoutSettings);
        }

        return clansGroupedByType;
    }

    private void AddClansFieldByType(EmbedBuilder embedBuilder, ClanType clanType, Dictionary<ClanType, List<Clan>> clansGroupedByType)
    {
        if (!clansGroupedByType.TryGetValue(clanType, out List<Clan>? clans))
            return;

        var clansFieldBuilder = new EmbedFieldBuilder()
            .WithIsInline(false)
            .WithName(clanType.ToString());

        var stringBuilder = new StringBuilder();
        foreach (var clan in clans)
        {
            stringBuilder.AppendLine($"- [**{clan.Name}** ({clan.Tag})]({clan.ClanProfileUrl}) {clan.Members.Count}/50");
        }
        clansFieldBuilder.WithValue(stringBuilder.ToString());

        embedBuilder.AddField(clansFieldBuilder);
    }

    private async Task AddCcDumpClans(EmbedBuilder embedBuilder)
    {
        var ccDumpclanTags = _botDb.ClanSettings.Where(cs => cs.CcGoldDump).Select(cs => cs.ClanTag).ToList();

        if (ccDumpclanTags.Count == 0)
            return;

        var clans = await _clansClient.GetCachedClansAsync();
        clans = clans.Where(c => ccDumpclanTags.Contains(c.Tag)).ToList();

        var clansFieldBuilder = new EmbedFieldBuilder()
            .WithIsInline(false)
            .WithName("CC Gold Dump");

        var stringBuilder = new StringBuilder();
        foreach (var clan in clans)
        {
            stringBuilder.AppendLine($"- [**{clan.Name}** ({clan.Tag})]({clan.ClanProfileUrl})");
        }
        clansFieldBuilder.WithValue(stringBuilder.ToString());

        embedBuilder.AddField(clansFieldBuilder);
    }

    public async Task<Embed> Warlog(string clantag, bool includeCwl)
    {
        try
        {
            var clan = await _clansClient.GetOrFetchClanAsync(clantag);

            var warDataList = await _clashKingApiClient.GetClanWarHistory(clantag, 50);

            if (warDataList is null)
            {
                return _embedHelper.ErrorEmbed("Error", $"Could not fetch War History for {clan.Name} ({clan.Tag})");
            }

            if (!includeCwl)
            {
                warDataList = warDataList.Where(d => d.AttacksPerMember == 2).ToList();
            }
            warDataList = warDataList.Take(25).ToList();

            if (clan is null || warDataList.Count < 1)
                return _embedHelper.ErrorEmbed("Error fetching clan war history", "Could not find any wars for the given Clan.");

            var builder = new EmbedBuilder()
                            .WithTitle($"Warlog for {clan.Name}")
                            .WithColor(Color.DarkPurple)
                            .WithThumbnailUrl(clan.BadgeUrls.Small);

            var tableData = new List<string[]>
        {
            new[] { "Win", "Attacks", "Stars", "Destruction", "Date" }
        };

            foreach (var warData in warDataList)
            {

                var (warClan, opponent) = warData.Clan.Tag == clan.Tag ? (warData.Clan, warData.Opponent) : (warData.Opponent, warData.Clan);
                var isWin = warClan.Stars > opponent.Stars || warClan.Stars == opponent.Stars && warClan.DestructionPercentage > opponent.DestructionPercentage;
                var endDate = DateTime.ParseExact(warData.EndTime, "yyyyMMddTHHmmss.fffZ", null, System.Globalization.DateTimeStyles.RoundtripKind);
                tableData.Add(
                [
                    isWin ? "✓ " : "✗ ",
                    $"{warClan.Attacks}/{warData.AttacksPerMember * warData.TeamSize}",
                    $"{warClan.Stars}",
                    $"{warClan.DestructionPercentage:0.00}%",
                    endDate.ToString("yyyy-MM-dd")
                ]);
            }

            builder.Description = _embedHelper.FormatAsTableOld(tableData);
            return builder.Build();
        }
        catch (Exception ex)
        {
            return _embedHelper.ErrorEmbed("Error fetching warlog", ex.Message);
        }
    }

    public async Task<Embed> StatsAttacks(string clanTag, AttackStatFilter attackStatFilter, WarTypeFilter warTypeFilter, uint limitWars, uint limitDays, uint minNumberAttacks, bool clanExclusive, int? playerTh = null)
    {
        Func<int, int?, bool> attackFilter = null!;
        Func<int, bool> successFilter = null!;
        switch (attackStatFilter)
        {
            case AttackStatFilter.Even3Star:
                attackFilter = (memberTh, oppTh) => memberTh == oppTh;
                successFilter = (stars) => stars >= 3;
                break;
            case AttackStatFilter.PlusOne3Star:
                attackFilter = (memberTh, oppTh) => memberTh + 1 == oppTh;
                successFilter = (stars) => stars >= 3;
                break;
            case AttackStatFilter.Reach2Star:
                attackFilter = (memberTh, oppTh) => memberTh < oppTh;
                successFilter = (stars) => stars >= 2;
                break;
        }


        var attackData = clanExclusive switch
        {
            true => await GetAttackStatsClanOnly(clanTag, warTypeFilter, limitWars, limitDays, minNumberAttacks, attackFilter, successFilter, playerTh),
            false => await GetAttackStatsAllClans(clanTag, warTypeFilter, limitWars, limitDays, minNumberAttacks, attackFilter, successFilter, playerTh)
        };

        attackData = attackData.OrderByDescending(d => d.GetSuccessRate()).ToList();

        List<string[]> data = [["Rate", "Count", "Player"]];
        foreach (var item in attackData)
        {
            data.Add([
                item.GetSuccessRate().ToString("0%"),
                $"{item.SuccessCount}/{item.AttackCount}",
                $"{item.PlayerName}"
           ]);
        }

        var tableString = _embedHelper.FormatAsTable(data, TextAlign.Left, TextAlign.Left);


        var clan = await _clansClient.GetOrFetchClanAsync(clanTag);
        var title = $"{clan.Name} {attackStatFilter} Ranking";
        var playerThText = playerTh is null ? "all" : playerTh.ToString();
        var filter = $"{warTypeFilter}, limitWars = {limitWars}, LimitDays: {limitDays}, minNumberOfAttacks = {minNumberAttacks}, playerTHs = {playerThText}";
        return new EmbedBuilder()
            .WithTitle(title)
            .WithDescription("```\n" + tableString + "\n```")
            .WithFooter(filter)
            .WithColor(Color.DarkPurple)
            .Build();
    }

    private async Task<List<AttackSuccessModel>> GetAttackStatsClanOnly(
        string clanTag,
        WarTypeFilter warTypeFilter,
        uint limitWars,
        uint limitDays,
        uint minNumberAttacks,
        Func<int, int?, bool> attackFilterFunc,
        Func<int, bool> successFunc,
        int? playerTh = null)
    {
        var wars = _botDb.WarHistories.FirstOrDefault(wh => wh.ClanTag == clanTag)?.WarData;
        var currentClan = await _clansClient.GetOrFetchClanAsync(clanTag);
        var currentMemberTags = currentClan.Members.Select(m => m.Tag);

        if (wars is null || wars.Count <= 0)
            return [];

        wars = wars
            .OrderByDescending(w => w.EndTime).Take((int)limitWars)
            .Where(w =>
            {
                var endTime = DateTime.ParseExact(w.EndTime, "yyyyMMddTHHmmss.fffZ", null, System.Globalization.DateTimeStyles.RoundtripKind);
                TimeSpan difference = DateTime.UtcNow - endTime;
                return difference.TotalDays <= limitDays;
            })
            .ToList();

        var attackDataDict = new Dictionary<string, AttackSuccessModel>();

        foreach (var warData in wars)
        {
            if (warTypeFilter == WarTypeFilter.CWLOnly && warData.AttacksPerMember != 1)
                continue;
            if (warTypeFilter == WarTypeFilter.RegularOnly && warData.AttacksPerMember != 2)
                continue;

            var clan = warData.Clan.Tag == clanTag ? warData.Clan : warData.Opponent;
            var opponent = warData.Clan.Tag != clanTag ? warData.Clan : warData.Opponent;

            foreach (var member in clan.Members)
            {
                if (playerTh is not null && member.TownhallLevel != playerTh)
                    continue;

                if (!currentMemberTags.Contains(member.Tag))
                    continue;

                foreach (var attack in member.Attacks)
                {
                    var opponentTh = opponent.Members.FirstOrDefault(p => p.Tag == attack.DefenderTag)?.TownhallLevel;
                    if (!attackFilterFunc(member.TownhallLevel, opponentTh))
                        continue;

                    attackDataDict.TryGetValue(member.Tag, out var attackDataEntry);
                    if (attackDataEntry == null)
                    {
                        attackDataEntry = new AttackSuccessModel(member.Name, member.Tag, member.TownhallLevel);
                        attackDataDict[member.Tag] = attackDataEntry;
                    }

                    if (successFunc(attack.Stars))
                        attackDataEntry.AddSuccess();
                    else
                        attackDataEntry.AddMiss();
                }
            }
        }

        return [.. attackDataDict.Values.Where(x => x.AttackCount >= minNumberAttacks)];
    }


    private async Task<List<AttackSuccessModel>> GetAttackStatsAllClans(
      string clanTag,
      WarTypeFilter warTypeFilter,
      uint limitWars,
      uint limitDays,
      uint minNumberAttacks,
      Func<int, int?, bool> attackFilterFunc,
      Func<int, bool> successFunc,
      int? playerTh = null)
    {
        var clan = await _clansClient.GetOrFetchClanAsync(clanTag);
        var attackDataDict = new Dictionary<string, AttackSuccessModel>();

        foreach (var member in clan.Members)
        {
            var warHits = await _clashKingApiService.GetOrFetchPlayerWarhitsAsync(member.Tag);
            if (warHits is null)
                continue;

            var warHitsFiltered = warHits.Items
                .OrderByDescending(w => w.WarData.EndTime).Take((int)limitWars)
                .Where(w =>
                {
                    var endTime = DateTime.ParseExact(w.WarData.EndTime, "yyyyMMddTHHmmss.fffZ", null, System.Globalization.DateTimeStyles.RoundtripKind);
                    TimeSpan difference = DateTime.UtcNow - endTime;
                    return difference.TotalDays <= limitDays;
                });

            var attackDataEntry = new AttackSuccessModel(member.Name, member.Tag, member.TownHallLevel ?? 0);
            attackDataDict[member.Tag] = attackDataEntry;

            foreach (var warHit in warHitsFiltered)
            {
                if (playerTh is not null && warHit.MemberData.TownhallLevel != playerTh)
                    continue;
                if (warTypeFilter == WarTypeFilter.CWLOnly && warHit.WarData.AttacksPerMember != 1)
                    continue;
                if (warTypeFilter == WarTypeFilter.RegularOnly && warHit.WarData.AttacksPerMember != 2)
                    continue;

                foreach (var attack in warHit.Attacks)
                {
                    var opponentTh = attack.Defender.TownhallLevel;
                    if (!attackFilterFunc(warHit.MemberData.TownhallLevel, opponentTh))
                        continue;

                    if (successFunc(attack.Stars))
                        attackDataEntry.AddSuccess();
                    else
                        attackDataEntry.AddMiss();
                }
            }
        }

        return [.. attackDataDict.Values.Where(x => x.AttackCount >= minNumberAttacks)];
    }

    public async Task<Embed> SettingsEdit(
        string clanTag,
        ClanType? clanType,
        int? order,
        SocketRole? memberRole,
        SocketRole? elderRole,
        SocketRole? leadershipRole,
        SocketRole? cwlRole,
        string? colorHex,
        bool? enableCwlSignup,
        bool? enableChampStyleSignup,
        bool? isCcGoldDumpClan)
    {
        var clan = await _clansClient.GetOrFetchClanAsync(clanTag);
        var clanSettings = _botDb.ClanSettings.FirstOrDefault(cs => cs.ClanTag == clanTag);
        if (clanSettings is null)
        {
            clanSettings = new ClanSettings { ClanTag = clanTag };
            _botDb.Add(clanSettings);
        }

        if (clanType is not null)
        {
            clanSettings.ClanType = clanType.Value;
        }

        if (order is not null)
        {
            clanSettings.Order = order.Value;
        }

        if (memberRole is not null)
        {
            clanSettings.MemberRoleId = memberRole.Id;
        }

        if (elderRole is not null)
        {
            clanSettings.ElderRoleId = elderRole.Id;
        }

        if (leadershipRole is not null)
        {
            clanSettings.LeaderRoleId = leadershipRole.Id;
        }

        if (cwlRole is not null)
        {
            clanSettings.CwlRoleId = cwlRole.Id;
        }

        if (!string.IsNullOrEmpty(colorHex))
        {
            if (!ColorHexRegex().IsMatch(colorHex))
            {
                return _embedHelper.ErrorEmbed("Error", "wrong format for hex color");
            }
            clanSettings.ColorHex = colorHex;
        }

        if (enableCwlSignup is not null)
        {
            clanSettings.EnableCwlSignup = enableCwlSignup.Value;
        }


        if (enableChampStyleSignup is not null)
        {
            clanSettings.ChampStyleCwlRoster = enableChampStyleSignup.Value;
        }

        if (isCcGoldDumpClan is not null)
        {
            clanSettings.CcGoldDump = isCcGoldDumpClan.Value;
        }

        _botDb.SaveChanges();

        string description = JsonConvert.SerializeObject(clanSettings, Formatting.Indented);


        return new EmbedBuilder()
            .WithTitle($"{clan.Name} Updated Settings")
            .WithDescription($"```{description}```")
            .WithColor(Color.Purple)
            .Build();
    }

    internal Embed SettingsReset(string clanTag)
    {
        var clanSettings = _botDb.ClanSettings.FirstOrDefault(cs => cs.ClanTag == clanTag);
        if (clanSettings is null)
        {
            return _embedHelper.ErrorEmbed("Error resetting clan settings", "This clan had no settings saved.");
        }
        try
        {
            _botDb.Remove(clanSettings);
            _botDb.SaveChanges();
        }
        catch (Exception ex)
        {
            return _embedHelper.ErrorEmbed("Error resetting clan settings", ex.Message);
        }

        return new EmbedBuilder()
            .WithDescription("Done")
            .Build();
    }
}
