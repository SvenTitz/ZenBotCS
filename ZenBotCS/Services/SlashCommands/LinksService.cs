using CocApi.Cache;
using Discord;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ZenBotCS.Entities;
using ZenBotCS.Entities.Models;
using ZenBotCS.Extensions;
using ZenBotCS.Helper;
using ZenBotCS.Models.Enums;

namespace ZenBotCS.Services.SlashCommands;

public class LinksService(BotDataContext botDb, PlayersClient playersClient, EmbedHelper embedHelper, ClashKingApiClient ckApiClient, ILogger<LinksService> logger)
{

    private readonly BotDataContext _botDb = botDb;
    private readonly PlayersClient _playersClient = playersClient;
    private readonly EmbedHelper _embedHelper = embedHelper;
    private readonly ClashKingApiClient _ckApiClient = ckApiClient;
    private readonly ILogger<LinksService> _logger = logger;

    public async Task<Embed> ListUnlinked()
    {
        var players = await _playersClient.GetCachedPlayersAsync();
        var linkedTags = _botDb.DiscordLinks.Select(dl => dl.PlayerTag);
        var missingPlayers = players.Where(p => !linkedTags.Contains(p.Tag));
        missingPlayers = missingPlayers.OrderBy(p => p.Clan?.Tag ?? "");


        var data = new List<string[]>
        {
            new[] { "PlayerTag", "Name", "Clan" }
        };
        foreach (var player in missingPlayers)
        {
            data.Add([player.Tag, player.Name, player.Clan?.Name ?? ""]);
        }

        var table = _embedHelper.FormatAsTable(data, TextAlign.Left, TextAlign.Left);
        var description = "```\n" + table + "\n```";

        var builder = new EmbedBuilder()
            .WithColor(Color.DarkPurple)
            .WithTitle("Players Missing Discord Link")
            .WithDescription(description);

        return builder.Build();
    }

    public async Task Update()
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


}
