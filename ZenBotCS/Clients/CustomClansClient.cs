using CocApi.Cache;
using CocApi.Cache.Services;
using CocApi.Cache.Services.Options;
using CocApi.Rest.Apis;
using CocApi.Rest.Models;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using ZenBotCS.Entities;

namespace ZenBotCS.Clients
{
    public class CustomClansClient : ClansClient
    {
        private IServiceScopeFactory _scopeFactory;
        private DiscordSocketClient _discordClient;
        private ILogger<CustomClansClient> _logger;

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
            _discordClient = discordClient;
            _logger = logger;

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
            await PostMissedAttacksReminderForWar(e);
            return;
        }


        public async Task<List<Clan>> GetCachedClansAsync()
        {
            var clans = await (from i in this.ScopeFactory.CreateScope().ServiceProvider.GetRequiredService<CacheDbContext>().Clans.AsNoTracking()
                               where i.Download
                               select i.Content).ToListAsync<Clan>().ConfigureAwait(continueOnCapturedContext: false);

            return clans ?? [];
        }

        private async Task PostMissedAttacksReminderForWar(WarEventArgs e)
        {
            await PostMissedAttackReminderForClan(e.War.Clan.Tag, e.War);
            await PostMissedAttackReminderForClan(e.War.Opponent.Tag, e.War);
        }

        private async Task PostMissedAttackReminderForClan(string clantag, ClanWar war)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var botDb = scope.ServiceProvider.GetRequiredService<BotDataContext>();

                var reminders = botDb.ReminderMisses.Where(rm => rm.ClanTag == clantag);

                var clan = war.Clan.Tag == clantag ? war.Clan : war.Opponent;
                var opponent = war.Clan.Tag != clantag ? war.Clan : war.Opponent;
                var memberWithMisses = clan.Members.Where(m => (m.Attacks?.Count ?? 0) < war.AttacksPerMember);

                if (!reminders.Any() || !memberWithMisses.Any())
                    return;

                var description = new StringBuilder();
                foreach (var member in memberWithMisses.OrderByDescending(m => m.Attacks?.Count ?? 0))
                {
                    var discordUserId = botDb.DiscordLinks.FirstOrDefault(dl => dl.PlayerTag == member.Tag)?.DiscordId;
                    var missedCount = war.AttacksPerMember - member.Attacks?.Count ?? war.AttacksPerMember;
                    description.Append($"- {missedCount}/{war.AttacksPerMember} **{member.Name}** ({member.Tag}");
                    if (discordUserId is null)
                        description.AppendLine(")");
                    else
                        description.AppendLine($", <@{discordUserId}>)");
                }
                description.Append($"\nWar ended: <t:{((DateTimeOffset)war.EndTime).ToUnixTimeSeconds()}:f>");

                var fieldBuilder = new EmbedFieldBuilder()
                    .WithName("Missed Attacks")
                    .WithValue(description.ToString())
                    .WithIsInline(false);

                var embedBuilder = new EmbedBuilder()
                    .WithTitle($"{clan.Name} vs {opponent.Name}")
                    .WithFields(fieldBuilder)
                    .WithColor(Color.DarkPurple);

                foreach (var reminder in reminders)
                {
                    try
                    {
                        if (await _discordClient.GetChannelAsync(reminder.ChannelId) is not SocketTextChannel channel)
                        {
                            _logger.LogError("No channel found with Id: {id}", reminder.ChannelId);
                            continue;
                        }

                        if (reminder.PingRoleId is null)
                        {
                            embedBuilder.WithDescription("");
                        }
                        else
                        {
                            embedBuilder.WithDescription($"<@&{reminder.PingRoleId}>");
                        }
                        var embed = embedBuilder.Build();

                        await channel.SendMessageAsync(embed: embed);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "reminderId: {id}", reminder.Id);
                    }
                }
            }

        }

    }
}
