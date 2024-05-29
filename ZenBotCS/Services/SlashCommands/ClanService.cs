using CocApi.Cache;
using CocApi.Rest.Models;
using Discord;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using ZenBotCS.Entities;
using ZenBotCS.Helper;
using ZenBotCS.Models;
using ZenBotCS.Models.Enums;

namespace ZenBotCS.Services.SlashCommands
{
    public class ClanService(ClansClient _clansClient, ClashKingApiClient _clashKingApiClient, EmbedHelper _embedHelper, BotDataContext _botDb)
    {

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
                var clans = await (from i in _clansClient.ScopeFactory.CreateScope().ServiceProvider.GetRequiredService<CacheDbContext>().Clans.AsNoTracking()
                                   where i.Download
                                   select i.Content).ToListAsync<Clan>().ConfigureAwait(continueOnCapturedContext: false);

                var builder = new EmbedBuilder()
                    .WithTitle("Clans:")
                    .WithColor(Color.DarkPurple);

                var stringBuilder = new StringBuilder();

                foreach (var clan in clans)
                {
                    if (stringBuilder.Length > 0)
                        stringBuilder.Append('\n');

                    stringBuilder.Append($"[**{clan.Name}** ({clan.Tag})]({clan.ClanProfileUrl}) {clan.Members.Count}/50");
                }

                builder.WithDescription(stringBuilder.ToString());
                return builder.Build();
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

        public async Task<Embed> Warlog(string clantag, bool includeCwl)
        {
            try
            {
                var clan = await _clansClient.GetOrFetchClanAsync(clantag);

                var warDataList = await _clashKingApiClient.GetClanWarHistory(clantag, 50);
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

        public async Task<Embed> StatsAttacks(string clanTag, AttackStatFilter attackStatFilter, WarTypeFilter warTypeFilter, uint limitWars, uint minNumberAttacks, int? playerTh = null)
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

            var attackData = await GetAttackStats(clanTag, warTypeFilter, limitWars, minNumberAttacks, attackFilter, successFilter, playerTh);
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
            var filter = $"{warTypeFilter}, limitWars = {limitWars}, minNumberOfAttacks = {minNumberAttacks}, playerTHs = {playerThText}";
            return new EmbedBuilder()
                .WithTitle(title)
                .WithDescription("```\n" + tableString + "\n```")
                .WithFooter(filter)
                .WithColor(Color.DarkPurple)
                .Build();
        }

        private async Task<List<AttackSuccessModel>> GetAttackStats(
            string clanTag,
            WarTypeFilter warTypeFilter,
            uint limitWars,
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

            wars = wars.OrderByDescending(w => w.EndTime).Take((int)limitWars).ToList();

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


    }
}
