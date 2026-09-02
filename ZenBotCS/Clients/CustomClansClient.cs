using CocApi.Cache;
using CocApi.Cache.Services;
using CocApi.Cache.Services.Options;
using CocApi.Rest.Apis;
using CocApi.Rest.Models;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZenBotCS.Services.SlashCommands;

namespace ZenBotCS.Clients
{
    public class CustomClansClient : ClansClient
    {
        private IServiceScopeFactory _scopeFactory;


        public CustomClansClient(
        ILogger<CustomClansClient> logger,
        IServiceScopeFactory scopeFactory,
        IClansApi clansApi,
        Synchronizer synchronizer,
        CocApi.Cache.Services.ClanService clanService,
        NewWarService newWarService,
        NewCwlWarService newCwlWarService,
        CwlWarService cwlWarService,
        WarService warService,
        IOptions<CacheOptions> options,
        DiscordSocketClient discordClient
        )
        : base(logger, clansApi, scopeFactory, synchronizer, clanService, newWarService, newCwlWarService, warService, cwlWarService, options)
        {
            _scopeFactory = scopeFactory;

            ClanWarUpdated += OnClanWarUpdated;
            ClanWarEnded += OnClanWarEnded;
        }

        private Task OnClanWarUpdated(object sender, ClanWarUpdatedEventArgs e)
        {
            //Logger.LogInformation("ClanWarUpdate called for {clanName}", e.Fetched.Clan.Name);
            //var newAttacks = ClanWar.NewAttacks(e.Stored, e.Fetched);
            //Logger.LogInformation("{newAttackCount} new attacks between {clanName} vs {opponentName}.", newAttacks.Count, e.Fetched.Clan.Name, e.Fetched.Opponent.Name);
            //foreach (var attack in newAttacks)
            //{
            //    var attacker = e.Fetched.Clan.Members.FirstOrDefault(m => m.Tag == attack.AttackerTag) ?? e.Fetched.Opponent.Members.FirstOrDefault(m => m.Tag == attack.AttackerTag);
            //    var defender = e.Fetched.Clan.Members.FirstOrDefault(m => m.Tag == attack.DefenderTag) ?? e.Fetched.Opponent.Members.FirstOrDefault(m => m.Tag == attack.DefenderTag);
            //    Logger.LogInformation("{attackerName}({attckerPos}) attacked {defenderName}({defenderPos}) and got {stars} with {dest}%",
            //        attacker!.Name, attack.AttackerMapPosition, defender!.Name, attack.DefenderMapPosition, attack.Stars, attack.DestructionPercentage);
            //}


            return Task.CompletedTask;
        }

        private async Task OnClanWarEnded(object sender, WarEventArgs e)
        {
            using var scope = _scopeFactory.CreateScope();
            var reminderService = scope.ServiceProvider.GetRequiredService<ReminderService>();
            await reminderService.PostMissedAttacksReminderForWar(e);
        }


        /// <summary>
        /// A league group's wars, paired with the war tag each came from, read from the CoC cache.
        /// Replaces the base <c>GetOrFetchLeagueWarsAsync</c>, which has two defects: on a cache miss
        /// it dereferences the live-fetch result without a null check, so one war tag that fails to
        /// resolve throws a <see cref="NullReferenceException"/> and loses the <i>whole</i> group; and
        /// its <c>realtime</c> argument only reaches that fallback, so a cached war is always returned
        /// as stored and realtime cannot be used to read a fresh lineup. Here an unreadable tag is
        /// skipped and the rest of the group survives. For a current lineup, fetch the one war you
        /// care about through <see cref="CocApi.Cache.ClansClient.ClansApi"/> with realtime.
        /// </summary>
        public async Task<List<(string WarTag, ClanWar War)>> GetLeagueWarsSafeAsync(ClanWarLeagueGroup group)
        {
            var wars = new List<(string, ClanWar)>();

            foreach (var round in group.Rounds)
            {
                // "#0" is the placeholder the game uses for a round it hasn't revealed yet.
                foreach (var warTag in round.WarTags.Where(t => t != "#0"))
                {
                    try
                    {
                        var war = (await GetLeagueWarOrDefaultAsync(warTag, group.Season))?.Content;
                        if (war is null)
                            continue; // not ingested yet; the other rounds are still fine

                        // Same season guard the base method applies: war tags recur across seasons.
                        if (war.PreparationStartTime.Month == group.Season.Month
                            && war.PreparationStartTime.Year == group.Season.Year)
                        {
                            wars.Add((warTag, war));
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "Skipping CWL war {warTag} that could not be read", warTag);
                    }
                }
            }

            return wars;
        }

        public async Task<List<Clan>> GetCachedClansAsync()
        {
            var clans = await (from i in this.ScopeFactory.CreateScope().ServiceProvider.GetRequiredService<CacheDbContext>().Clans.AsNoTracking()
                               where i.Download
                               select i.Content).ToListAsync<Clan>().ConfigureAwait(continueOnCapturedContext: false);

            return clans ?? [];
        }


    }
}
