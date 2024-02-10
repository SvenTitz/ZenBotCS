using CocApi.Cache;
using CocApi.Rest.Models;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text;
using ZenBotCS.Entities;
using ZenBotCS.Entities.Models;
using ZenBotCS.Entities.Models.ClashKingApi;
using ZenBotCS.Extensions;
using ZenBotCS.Helper;
using ZenBotCS.Models;

namespace ZenBotCS.Services.SlashCommands
{
    public class PlayerService(EmbedHelper embedHelper, ClashKingApiClient ckApiClient, BotDataContext botDb, ILogger<PlayerService> logger, PlayersClient playersClient)
    {
        private readonly EmbedHelper _embedHelper = embedHelper;
        private readonly ClashKingApiClient _ckApiClient = ckApiClient;
        private readonly BotDataContext _botDb = botDb;
        private readonly ILogger<PlayerService> _logger = logger;
        private readonly PlayersClient _playersClient = playersClient;

        public async Task<Embed> StatsMisses(string? playerTag, SocketUser? user)
        {

            if (playerTag is null && user is null)
            {
                return _embedHelper.ErrorEmbed("Error", "You need to provide either a User or Playertag.");
            }

            try
            {
                var players = await GetPlayersFromTagAndUser(playerTag, user);
                var playerTags = players.Select(x => x.Tag).ToList();

                if (!players.Any())
                    return _embedHelper.ErrorEmbed("Error", "Could not find any Players linked to that user.");

                List<MissedAttackRecord> missedAttacks = new();
                foreach (var history in _botDb.WarHistories)
                {
                    foreach (var warData in history.WarData ?? [])
                    {
                        var clan = warData.Clan;
                        var member = clan.Members.FirstOrDefault(m => playerTags.Contains(m.Tag));
                        if (member is null)
                        {
                            clan = warData.Opponent;
                            member = clan.Members.FirstOrDefault(m => playerTags.Contains(m.Tag));
                        }
                        if (member is null)
                            continue;

                        if (member.Attacks.Count() < warData.AttacksPerMember)
                        {
                            missedAttacks.Add(new MissedAttackRecord()
                            {
                                ClanName = clan.Name,
                                ClanTag = clan.Tag,
                                PlayerName = member.Name,
                                PlayerTag = member.Tag,
                                Attacks = warData.AttacksPerMember,
                                Misses = warData.AttacksPerMember - member.Attacks.Count(),
                                Date = DateTime.ParseExact(warData.EndTime, "yyyyMMddTHHmmss.fffZ", null, System.Globalization.DateTimeStyles.RoundtripKind)
                            });
                        }
                    }
                }

                missedAttacks = missedAttacks
                    .OrderBy(m => m.PlayerName)
                    .ThenBy(m => m.ClanName)
                    .ThenBy(m => m.Date)
                    .ToList();


                var stringBuilder = new StringBuilder();

                stringBuilder.AppendLine("```");
                stringBuilder.Append($"Bases:         ");
                stringBuilder.AppendLine(string.Join(" | ", players.Select(p => p?.Name)));
                stringBuilder.Append($"THs:           ");
                stringBuilder.AppendLine(string.Join(" | ", players.Select(p => p?.TownHallLevel)));
                stringBuilder.Append($"Total Misses:  ");
                stringBuilder.AppendLine(missedAttacks.Sum(ma => ma.Misses).ToString());
                stringBuilder.AppendLine();

                var data = new List<string[]>
                {
                    new[] { "Player", "Clan", "Misses", "Date" }
                };
                foreach (var missedAttack in missedAttacks)
                {
                    data.Add([
                        missedAttack.PlayerName,
                        missedAttack.ClanName,
                        $"{missedAttack.Misses}/{missedAttack.Attacks}",
                        missedAttack.Date.ToString("yyyy-MM-dd"),
                    ]);
                }

                stringBuilder.AppendLine(_embedHelper.FormatAsNewTable(data));

                stringBuilder.AppendLine("```");

                var embedBuilder = new EmbedBuilder()
                    .WithTitle("Player Missed Attacks*")
                    .WithColor(Color.DarkPurple)
                    .WithDescription(stringBuilder.ToString())
                    .WithFooter("*in the least 50 recorded wars for each family clan.");

                return embedBuilder.Build();

            }
            catch (Exception ex)
            {
                return _embedHelper.ErrorEmbed("Error", ex.Message);
            }

        }

        public async Task UpdateDiscordLinks()
        {
            var players = await _playersClient.GetCachedPlayersAsync();
            var playerTags = players.Select(p => p.Tag).ToList();

            var links = await _ckApiClient.PostDiscordLinksAsync(playerTags);

            foreach (var link in links)
            {
                if (link.Value is null)
                    continue;
                var linkModel = new DiscordLink { DiscordId = (ulong)link.Value!, PlayerTag = link.Key };
                _botDb.AddOrUpdateDiscordLink(linkModel);
            }
            _botDb.SaveChanges();

            _logger.LogInformation("Updated discord links for {count} player accounts", links.Where(l => l.Value is not null).Count());

            var nonLinked = links.Where(l => l.Value is null).Select(kvp => kvp.Key).ToList();
            _logger.LogWarning("Could not find discord link for the following {count} player accounts: {playerTags}", nonLinked.Count, JsonConvert.SerializeObject(nonLinked));
        }

        public async Task<Embed> StatsAttacks(string? playerTag, SocketUser? user)
        {
            if (playerTag is null && user is null)
            {
                return _embedHelper.ErrorEmbed("Error", "You need to provide either a User or Playertag.");
            }

            try
            {
                var players = await GetPlayersFromTagAndUser(playerTag, user);

                if (!players.Any())
                    return _embedHelper.ErrorEmbed("Error", "Could not find any Players linked to that user.");

                List<WarAttack> warHits = [];
                foreach (var player in players)
                {
                    warHits.AddRange(await _ckApiClient.GetPlayerWarAttacksAsync(player!.Tag));
                }

                var stringBuilder = new StringBuilder();

                stringBuilder.AppendLine("```");
                stringBuilder.Append($"Bases:    ");
                stringBuilder.AppendLine(string.Join(" | ", players.Select(p => p?.Name)));
                stringBuilder.Append($"THs:      ");
                stringBuilder.AppendLine(string.Join(" | ", players.Select(p => p?.TownHallLevel)));
                stringBuilder.Append($"Attacks:  ");
                stringBuilder.AppendLine(warHits.Count.ToString());
                stringBuilder.AppendLine();


                // stringBuilder.Append("\n\n");

                var groupedHits = warHits.GroupBy(wh => wh.Townhall).OrderByDescending(g => g.Key);

                var data = new List<string[]>
                {
                    new[] {"", "0*", "1*", "2*", "3*", "Succ%" }
                };

                foreach (var group in groupedHits)
                {
                    var reacheHits = group.Where(wh => wh.DefenderTownhall > group.Key);
                    data.Add(
                    [
                      $"{group.Key} Reach",
                        $"{reacheHits.Count(wh => wh.Stars == 0)}/{reacheHits.Count()}",
                        $"{reacheHits.Count(wh => wh.Stars == 1)}/{reacheHits.Count()}",
                        $"{reacheHits.Count(wh => wh.Stars == 2)}/{reacheHits.Count()}",
                        $"{reacheHits.Count(wh => wh.Stars == 3)}/{reacheHits.Count()}",
                        (reacheHits.Count(wh => wh.Stars > 2) / (double)reacheHits.Count()).ToString("0%")
                    ]);
                    if (group.Key < 10)
                        data.Last()[0] = data.Last()[0] + " ";

                    var evenHits = group.Where(wh => wh.DefenderTownhall == group.Key);
                    data.Add(
                    [
                      $"{group.Key} vs {group.Key}",
                        $"{evenHits.Count(wh => wh.Stars == 0)}/{evenHits.Count()}",
                        $"{evenHits.Count(wh => wh.Stars == 1)}/{evenHits.Count()}",
                        $"{evenHits.Count(wh => wh.Stars == 2)}/{evenHits.Count()}",
                        $"{evenHits.Count(wh => wh.Stars == 3)}/{evenHits.Count()}",
                        (evenHits.Count(wh => wh.Stars == 3) / (double)evenHits.Count()).ToString("0%")
                    ]);
                    if (group.Key < 10)
                        data.Last()[0] = data.Last()[0] + "  ";

                    var dipHits = group.Where(wh => wh.DefenderTownhall < group.Key);
                    data.Add(
                    [
                      $"{group.Key} Dip  ",
                        $"{dipHits.Count(wh => wh.Stars == 0)}/{dipHits.Count()}",
                        $"{dipHits.Count(wh => wh.Stars == 1)}/{dipHits.Count()}",
                        $"{dipHits.Count(wh => wh.Stars == 2)}/{dipHits.Count()}",
                        $"{dipHits.Count(wh => wh.Stars == 3)}/{dipHits.Count()}",
                        (dipHits.Count(wh => wh.Stars == 3) / (double)dipHits.Count()).ToString("0%")
                    ]);
                    if (group.Key < 10)
                        data.Last()[0] = data.Last()[0] + " ";

                    data.Add(["", "", "", "", "", ""]);
                }

                stringBuilder.AppendLine(_embedHelper.FormatAsNewTable(data));

                stringBuilder.AppendLine("```");



                var embedBuilder = new EmbedBuilder()
                    .WithTitle("Player Attack Stats")
                    .WithColor(Color.DarkPurple)
                    .WithDescription(stringBuilder.ToString());

                return embedBuilder.Build();
            }
            catch (Exception ex)
            {
                return _embedHelper.ErrorEmbed("Error", ex.Message);
            }
        }

        private async Task<List<Player>> GetPlayersFromTagAndUser(string? playerTag, SocketUser? user)
        {
            var playerTags = new List<string>();
            var players = new List<Player?>();
            if (playerTag is not null)
            {
                playerTags.Add(playerTag);

                players.Add(await _playersClient.GetOrFetchPlayerAsync(playerTag));
            }
            if (user is not null)
            {
                var userTags = _botDb.DiscordLinks.Where(dl => dl.DiscordId == user.Id).Select(dl => dl.PlayerTag).ToList();
                playerTags.AddRange(userTags);
                var userPlayers = (await _playersClient.GetCachedPlayersAsync(userTags)).Select(cp => cp.Content);
                players.AddRange(userPlayers);
            }

            List<Player> result = [.. players.Where(p => p is not null).OrderBy(p => p?.TownHallLevel)];
            return result;
        }
    }

}
