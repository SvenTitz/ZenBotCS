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


        public async Task<List<Clan>> GetCachedClansAsync()
        {
            var clans = await (from i in this.ScopeFactory.CreateScope().ServiceProvider.GetRequiredService<CacheDbContext>().Clans.AsNoTracking()
                               where i.Download
                               select i.Content).ToListAsync<Clan>().ConfigureAwait(continueOnCapturedContext: false);

            return clans ?? [];
        }


    }
}
