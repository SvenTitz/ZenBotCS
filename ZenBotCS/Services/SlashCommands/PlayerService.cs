﻿using System.Text;
using CocApi.Cache;
using CocApi.Rest.Models;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using ZenBotCS.Clients;
using ZenBotCS.Entities;
using ZenBotCS.Entities.Models.ClashKingApi.PlayerWarHits;
using ZenBotCS.Extensions;
using ZenBotCS.Helper;
using ZenBotCS.Models;
using ZenBotCS.Models.Enums;

namespace ZenBotCS.Services.SlashCommands
{
    public class PlayerService(EmbedHelper embedHelper, ClashKingApiClient ckApiClient, ClashKingApiService _ckApiService, BotDataContext botDb, ILogger<PlayerService> logger, PlayersClient playersClient, CustomClansClient clansClient)
    {
        private readonly EmbedHelper _embedHelper = embedHelper;
        private readonly ClashKingApiClient _ckApiClient = ckApiClient;
        private readonly BotDataContext _botDb = botDb;
        private readonly ILogger<PlayerService> _logger = logger;
        private readonly PlayersClient _playersClient = playersClient;
        private readonly CustomClansClient _clansClient = clansClient;

        public async Task<Embed> StatsMisses(string? playerTag, SocketUser? user, WarTypeFilter warTypeFilter)
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
                int warCount = 0;
                foreach (var history in _botDb.WarHistories)
                {
                    foreach (var warData in history.WarData ?? [])
                    {
                        if (warTypeFilter == WarTypeFilter.CWLOnly && warData.AttacksPerMember != 1)
                            continue;
                        if (warTypeFilter == WarTypeFilter.RegularOnly && warData.AttacksPerMember != 2)
                            continue;

                        var clan = warData.Clan;
                        var member = clan.Members.FirstOrDefault(m => playerTags.Contains(m.Tag));
                        if (member is null)
                        {
                            clan = warData.Opponent;
                            member = clan.Members.FirstOrDefault(m => playerTags.Contains(m.Tag));
                        }
                        if (member is null)
                            continue;

                        warCount++;
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

                double missesCount = missedAttacks.Sum(ma => ma.Misses);

                var stringBuilder = new StringBuilder();

                stringBuilder.AppendLine("```");
                stringBuilder.Append($"Bases:      ");
                stringBuilder.AppendLine(string.Join(" | ", players.Select(p => p?.Name)));
                stringBuilder.Append($"THs:        ");
                stringBuilder.AppendLine(string.Join(" | ", players.Select(p => p?.TownHallLevel)));
                stringBuilder.Append("# Wars:      ");
                stringBuilder.AppendLine(warCount.ToString());
                stringBuilder.Append("# Misses:    ");
                stringBuilder.AppendLine(missesCount.ToString());
                stringBuilder.Append("Misses/War:  ");
                stringBuilder.AppendLine((missesCount / warCount).ToString("0.00"));

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

                stringBuilder.AppendLine(_embedHelper.FormatAsTable(data, TextAlign.Left, TextAlign.Right));

                stringBuilder.AppendLine("```");

                var embedBuilder = new EmbedBuilder()
                    .WithTitle("Player Missed Attacks*")
                    .WithColor(Color.DarkPurple)
                    .WithDescription(stringBuilder.ToString())
                    .WithFooter($"Filter: {warTypeFilter}\n*in the last 50 recorded wars for each family clan.");

                return embedBuilder.Build();

            }
            catch (Exception ex)
            {
                return _embedHelper.ErrorEmbed("Error", ex.Message);
            }

        }


        public async Task<Embed> StatsAttacks(string? playerTag, SocketUser? user, WarTypeFilter warTypeFilter, uint limitDays)
        {
            if (playerTag is null && user is null)
            {
                return _embedHelper.ErrorEmbed("Error", "You need to provide either a User or Playertag.");
            }

            try
            {
                var players = await GetPlayersFromTagAndUser(playerTag, user);

                if (players.Count == 0)
                    return _embedHelper.ErrorEmbed("Error", "Could not find any Players linked to that user.");

                PlayerWarhits warHits = new();
                foreach (var player in players)
                {
                    var warHitItems = await _ckApiClient.GetPlayerWarAttacksAsync(player!.Tag, limitDays);
                    if (warHitItems is not null)
                        warHits.Items.AddRange(warHitItems.Items);
                }

                if (warTypeFilter == WarTypeFilter.CWLOnly)
                {
                    warHits.Items = warHits.Items.Where(i => i.WarData.Type == "cwl").ToList();
                }
                else if (warTypeFilter == WarTypeFilter.RegularOnly)
                {
                    warHits.Items = warHits.Items.Where(i => i.WarData.Type != "cwl").ToList();
                }

                var stringBuilder = new StringBuilder();

                stringBuilder.AppendLine("```");
                stringBuilder.Append($"Bases:    ");
                stringBuilder.AppendLine(string.Join(" | ", players.Select(p => p?.Name)));
                stringBuilder.Append($"THs:      ");
                stringBuilder.AppendLine(string.Join(" | ", players.Select(p => p?.TownHallLevel)));
                stringBuilder.Append($"Attacks:  ");
                stringBuilder.AppendLine(warHits.Items.Sum(i => i.Attacks.Count).ToString());
                stringBuilder.Append("Misses:   ");
                stringBuilder.AppendLine(warHits.Items.Sum(i => (i.WarData.AttacksPerMember > 0 ? i.WarData.AttacksPerMember : 1) - i.Attacks.Count).ToString());
                stringBuilder.AppendLine();


                // stringBuilder.Append("\n\n");

                var groupedHits = warHits.Items.GroupBy(i => i.MemberData.TownhallLevel).OrderByDescending(g => g.Key);

                var data = new List<string[]>
                {
                    new[] {"", "0*", "1*", "2*", "3*", "Succ%" }
                };

                foreach (var group in groupedHits)
                {
                    var attacks = group.SelectMany(i => i.Attacks).ToList();
                    var reacheHits = attacks.Where(a => a.Defender.TownhallLevel > group.Key);
                    data.Add(
                    [
                      $"{group.Key}Rea",
                        $"{reacheHits.Count(wh => wh.Stars == 0)}/{reacheHits.Count()}",
                        $"{reacheHits.Count(wh => wh.Stars == 1)}/{reacheHits.Count()}",
                        $"{reacheHits.Count(wh => wh.Stars == 2)}/{reacheHits.Count()}",
                        $"{reacheHits.Count(wh => wh.Stars == 3)}/{reacheHits.Count()}",
                        reacheHits.Count() == 0 ? " - " : (reacheHits.Count(wh => wh.Stars >= 2) / (double)reacheHits.Count()).ToString("0%")
                    ]);
                    if (group.Key < 10)
                        data.Last()[0] = data.Last()[0] + " ";

                    var evenHits = attacks.Where(a => a.Defender.TownhallLevel == group.Key);
                    data.Add(
                    [
                      $"{group.Key}v{group.Key}",
                        $"{evenHits.Count(wh => wh.Stars == 0)}/{evenHits.Count()}",
                        $"{evenHits.Count(wh => wh.Stars == 1)}/{evenHits.Count()}",
                        $"{evenHits.Count(wh => wh.Stars == 2)}/{evenHits.Count()}",
                        $"{evenHits.Count(wh => wh.Stars == 3)}/{evenHits.Count()}",
                        evenHits.Count() == 0 ? " - " : (evenHits.Count(wh => wh.Stars == 3) / (double)evenHits.Count()).ToString("0%")
                    ]);
                    if (group.Key < 10)
                        data.Last()[0] = data.Last()[0] + "  ";

                    var dipHits = attacks.Where(a => a.Defender.TownhallLevel < group.Key);
                    data.Add(
                    [
                      $"{group.Key}Dip",
                        $"{dipHits.Count(wh => wh.Stars == 0)}/{dipHits.Count()}",
                        $"{dipHits.Count(wh => wh.Stars == 1)}/{dipHits.Count()}",
                        $"{dipHits.Count(wh => wh.Stars == 2)}/{dipHits.Count()}",
                        $"{dipHits.Count(wh => wh.Stars == 3)}/{dipHits.Count()}",
                        dipHits.Count() == 0 ? " - " : (dipHits.Count(wh => wh.Stars == 3) / (double)dipHits.Count()).ToString("0%")
                    ]);
                    if (group.Key < 10)
                        data.Last()[0] = data.Last()[0] + " ";

                    data.Add(["", "", "", "", "", ""]);
                }

                stringBuilder.AppendLine(_embedHelper.FormatAsTable(data, TextAlign.Right, TextAlign.Right));

                stringBuilder.AppendLine("```");



                var embedBuilder = new EmbedBuilder()
                    .WithTitle("Player Attack Stats")
                    .WithColor(Color.DarkPurple)
                    .WithFooter($"Filter: {warTypeFilter}, LimitDays: {limitDays}")
                    .WithDescription(stringBuilder.ToString());

                return embedBuilder.Build();
            }
            catch (Exception ex)
            {
                return _embedHelper.ErrorEmbed("Error", ex.Message);
            }
        }

        public async Task<List<Embed>> ToDo(SocketUser user)
        {
            try
            {
                var players = await GetPlayersFromTagAndUser(null, user);

                if (players is null || players.Count == 0)
                    return [_embedHelper.ErrorEmbed("Error", "No accounts linked to you.")];

                var playerTags = players.Select(p => p.Tag).ToList();
                var clanTags = (await _clansClient.GetCachedClansAsync()).Select(c => c.Tag);
                List<OpenAttacks> openAttacks = [];
                List<OpenAttacks> upcomingAttacks = [];

                foreach (var clanTag in clanTags)
                {
                    // var war = (await _clansClient.GetActiveClanWarOrDefaultAsync(clanTag))?.Content;
                    var wars = (await _clansClient.GetClanWarsAsync(clanTag))
                        .Select(cw => cw.Content)
                        .Where(w => w is not null && w.EndTime > DateTime.UtcNow);

                    foreach (var war in wars)
                    {
                        if (war is null
                            || war.EndTime < DateTime.Now
                            || (war.State != WarState.InWar && war.State != WarState.Preparation))
                            continue;

                        var warClan = war.Clan.Tag == clanTag
                            ? war.Clan
                            : war.Opponent;

                        var warMembers = warClan.Members.Where(m => playerTags.Contains(m.Tag)).ToList();

                        if (warMembers is null || warMembers.Count == 0)
                            continue;

                        foreach (var warMember in warMembers)
                        {
                            if (war.State == WarState.InWar
                                && (warMember.Attacks is null || warMember.Attacks.Count < war.AttacksPerMember))
                            {
                                var attackCount = war.AttacksPerMember - (warMember.Attacks?.Count ?? 0);
                                openAttacks.Add(new OpenAttacks(warMember.Name, warMember.TownhallLevel, warClan.Name, attackCount, war.EndTime, war.StartTime));
                            }
                            else if (war.State == WarState.Preparation)
                            {
                                upcomingAttacks.Add(new OpenAttacks(warMember.Name, warMember.TownhallLevel, warClan.Name, war.AttacksPerMember, war.EndTime, war.StartTime));
                            }
                        }
                    }
                }

                openAttacks = [.. openAttacks.OrderBy(a => a.WarStartTime)];
                upcomingAttacks = [.. upcomingAttacks.OrderBy(a => a.WarStartTime)];

                List<Embed> embeds = [];
                var embedBuilder = new EmbedBuilder()
                    .WithTitle($"Open Attacks for {(user as SocketGuildUser)?.DisplayName ?? user.GlobalName}")
                    .WithImageUrl("https://cdn.discordapp.com/attachments/809874883768614922/1231630801792405704/Zen-CWL-Spacer.png?ex=6629d0d1&is=66287f51&hm=3372eb6161b41bb81bc6d89e02049e8e6ea1bc2126abb3f7bd8079306207b7c9&")
                    .WithColor(Color.DarkPurple);

                var description = new StringBuilder();

                if (openAttacks.Count > 0)
                {
                    foreach (var a in openAttacks)
                    {
                        var timestamp = (long)a.WarEndTime.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
                        var attacksSuffix = a.AttackCount > 1 ? "s" : "";
                        description.AppendLine($"- **{a.AttackCount}** attack{attacksSuffix} with **{a.PlayerName}{_embedHelper.ToSuperscript(a.PlayerTh)}** in **{a.ClanName}** until <t:{timestamp}:t>, <t:{timestamp}:R>");
                    }

                }
                else
                {
                    description.AppendLine("- None");
                }
                embedBuilder.WithDescription(description.ToString());
                embeds.Add(embedBuilder.Build());

                embedBuilder.WithTitle($"Upcoming Attacks for {(user as SocketGuildUser)?.DisplayName ?? user.GlobalName}");
                description = new StringBuilder();
                if (upcomingAttacks.Count > 0)
                {
                    foreach (var a in upcomingAttacks)
                    {
                        var timestamp = (long)a.WarStartTime.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
                        var attacksSuffix = a.AttackCount > 1 ? "s" : "";
                        description.AppendLine($"- **{a.AttackCount}** attack{attacksSuffix} with **{a.PlayerName}{_embedHelper.ToSuperscript(a.PlayerTh)}** in **{a.ClanName}**. War starts at <t:{timestamp}:t>, <t:{timestamp}:R>");
                    }
                }
                else
                {
                    description.AppendLine("- None");
                }
                embedBuilder.WithDescription(description.ToString());
                embeds.Add(embedBuilder.Build());


                embedBuilder.WithTitle($"Legend League Attacks for {(user as SocketGuildUser)?.DisplayName ?? user.GlobalName}");
                description = new StringBuilder();
                var season = await _ckApiClient.GetCurrentSeason();
                foreach (var player in players)
                {
                    if (player.League?.Name != "Legend League" || season is null)
                        continue;

                    var legendPlayer = await _ckApiClient.GetPlayerLegends(player.Tag, season);
                    var numAttacks = legendPlayer.Legends[_ckApiClient.GetCurrentLegendDay()].NumAttacks;
                    if (numAttacks == 8)
                        description.Append("~~");
                    description.Append($"- {numAttacks}/8 on **{player.Name}{_embedHelper.ToSuperscript(player.TownHallLevel)}**");
                    if (numAttacks == 8)
                        description.Append("~~");
                    description.AppendLine();
                }
                if (description.Length > 0)
                {
                    embedBuilder.WithDescription(description.ToString());
                    embeds.Add(embedBuilder.Build());
                }



                return embeds;
            }
            catch (Exception ex)
            {
                return [_embedHelper.ErrorEmbed("Error", ex.Message)];
            }
        }


        internal async Task<List<Player>> GetPlayersFromTagAndUser(string? playerTag, SocketUser? user)
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
                var userPlayers = (await _playersClient.GetOrFetchPlayersAsync(userTags));
                players.AddRange(userPlayers);
            }

            List<Player> result = [.. players.Where(p => p is not null).OrderBy(p => p?.TownHallLevel)];
            return result;
        }

        public async Task<Embed[]> StatsData(string playerTag)
        {
            var (playerStats, lastUpdated) = await _ckApiService.GetOrFetchPlayerStatsAsync(playerTag);

            if (playerStats is null)
            {
                return [embedHelper.ErrorEmbed("Error", "Could not fetch player data from CK API.")];
            }

            var clan = await _clansClient.GetOrFetchClanOrDefaultAsync(playerStats.ClanTag);

            var embedTemplate = new EmbedBuilder()
                .WithColor(Color.Purple);


            var playerInfoText =
                $"`Name:         ` {playerStats.Name}\n" +
                $"`Tag:          ` {playerStats.Tag}\n" +
                $"`Townhall:     ` {playerStats.Townhall}\n" +
                $"`LastOnline:   ` <t:{playerStats.LastOnline}:f>\n" +
                //$"`Trophies:     ` {playerStats.Trophies}\n" +
                //$"`WarStars:     ` {playerStats.WarStars}\n" +
                $"`ClanCapContr.:` {playerStats.ClanCapitalContributions:N0}\n" +
                $"`Clan:         ` {clan?.Name ?? ""}\n" +
                $"`League:       ` {playerStats.League}\n" +
                $"`LastUpdated:  ` <t:{new DateTimeOffset(lastUpdated, TimeSpan.Zero).ToUnixTimeSeconds()}:f>\n";
            var playerInfoEmbed = embedTemplate
                .WithTitle("Player Data")
                .WithDescription(playerInfoText)
                .Build();

            var playerDonationText = new StringBuilder();
            foreach (var kvp in playerStats.Donations ?? [])
            {
                playerDonationText.AppendLine($"- `{kvp.Key}`");
                playerDonationText.AppendLine($"  - `donated: ` {kvp.Value.Donated}");
                playerDonationText.AppendLine($"  - `received:` {kvp.Value.Received}");
            }
            var playerDonationEmbed = embedTemplate
                .WithTitle("Player Donations")
                .WithDescription(playerDonationText.ToString())
                .Build();

            var playerCapText = new StringBuilder();
            foreach (var kvp in playerStats.CapitalRaids ?? [])
            {
                playerCapText.AppendLine($"- `{kvp.Key}`");
                playerCapText.AppendLine($"  - `donated:` [ {string.Join(", ", kvp.Value.Donate)} ]");
                if (kvp.Value.Raid.Count != 0)
                    playerCapText.AppendLine($"  - `raided: ` [ {string.Join(", ", kvp.Value.Raid)} ]");
            }
            var playerCapEmbed = embedTemplate
                .WithTitle("Player Capital Raids")
                .WithDescription(playerCapText.ToString())
                .Build();

            var playerClanGamesText = new StringBuilder();
            foreach (var kvp in playerStats.ClanGames ?? [])
            {
                playerClanGamesText.AppendLine($"- `{kvp.Key}`");
                playerClanGamesText.AppendLine($"  - `clan:  ` {kvp.Value.ClanTag}");
                playerClanGamesText.AppendLine($"  - `points:` {kvp.Value.Points}");
            }
            var playerClanGamesEmbed = embedTemplate
                .WithTitle("Player Clan Game Points")
                .WithDescription(playerClanGamesText.ToString())
                .Build();

            var playerAttackWinsText = new StringBuilder();
            foreach (var kvp in playerStats.AttackWins ?? [])
            {
                playerAttackWinsText.AppendLine($"- `{kvp.Key}:` {kvp.Value}");
            }
            var playerAttackWinsEmbed = embedTemplate
                .WithTitle("Player Multiplayer Attack Wins")
                .WithDescription(playerAttackWinsText.ToString())
                .Build();

            var playerActivityText = new StringBuilder();
            foreach (var kvp in playerStats.Activity ?? [])
            {
                playerActivityText.AppendLine($"- `{kvp.Key}:` {kvp.Value}");
            }
            var playerActivityEmbed = embedTemplate
                .WithTitle("Player Activity")
                .WithDescription(playerActivityText.ToString())
                .Build();

            return [playerInfoEmbed, playerDonationEmbed, playerCapEmbed, playerClanGamesEmbed, playerAttackWinsEmbed, playerActivityEmbed];
        }
    }

}
