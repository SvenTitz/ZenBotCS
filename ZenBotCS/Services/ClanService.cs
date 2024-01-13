using CocApi.Cache;
using CocApi.Rest.Apis;
using CocApi.Rest.Models;
using Discord;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZenBotCS.Services
{
    public class ClanService(ClansClient clansClient, ClashKingApiClient clashKingApiClient, EmbedHelper embedHelper)
    {
        private readonly ClansClient _clansClient = clansClient;
        private readonly ClashKingApiClient _clashKingApiClient = clashKingApiClient;
        private readonly EmbedHelper _embedHelper = embedHelper;

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
                    if(stringBuilder.Length > 0)
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
            var clan = await _clansClient.GetOrFetchClanAsync(clantag);

            var warDataList = await _clashKingApiClient.GetClanWarHistory(clantag, 50);
            if(!includeCwl) 
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

            foreach(var warData in warDataList)
            {

                var (warClan, opponent) = warData.Clan.Tag == clan.Tag ? (warData.Clan, warData.Opponent) : (warData.Opponent, warData.Clan);
                var isWin = warClan.Stars > opponent.Stars || warClan.Stars == opponent.Stars && warClan.DestructionPercentage > opponent.DestructionPercentage;
                var endDate =  DateTime.ParseExact(warData.EndTime, "yyyyMMddTHHmmss.fffZ", null, System.Globalization.DateTimeStyles.RoundtripKind);
                tableData.Add(
                [
                    isWin ? "✓ " : "✗ ",
                    $"{warClan.Attacks}/{warData.AttacksPerMember*warData.TeamSize}",
                    $"{warClan.Stars}",
                    $"{warClan.DestructionPercentage:0.00}%",
                    endDate.ToString("yyyy-MM-dd")
                ]);
            }

            builder.Description = _embedHelper.FormatAsTable(tableData);
            return builder.Build();
        }



    }
}
