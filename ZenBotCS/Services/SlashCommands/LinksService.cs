using System.Text;
using CocApi.Cache;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ZenBotCS.Clients;
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

    /// <summary>
    /// Mirror ClashKing's links into the bot's table: stored links that no longer exist upstream are
    /// deleted, not just skipped. Asks about every tag the table already holds as well as every
    /// tracked player, so an account that left the family still gets validated.
    ///
    /// The prune leans on v2 telling the two failure modes apart: a tag answered with a null value is
    /// genuinely unlinked, while a tag missing from the answer belongs to a batch that failed and is
    /// left alone. Under v1 that distinction didn't exist, which is why this used to only ever add.
    /// </summary>
    public async Task Update()
    {
        var players = await _playersClient.GetCachedPlayersAsync();
        // Read the table once, tracked: the same rows are both the extra tags to ask about and the
        // candidates for pruning, so the stale set is filtered in memory rather than through a
        // several-hundred-value SQL IN clause.
        var storedLinks = _botDb.DiscordLinks.ToList();

        var playerTags = players.Select(p => p.Tag)
            .Concat(storedLinks.Select(dl => dl.PlayerTag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var links = await _ckApiClient.PostDiscordLinksAsync(playerTags);
        if (links is null)
        {
            // The endpoint is down, not "nobody is linked" -- keep the table we already have so it
            // can serve as the backup (see DiscordLinkSource) instead of wiping every player.
            _logger.LogWarning("ClashKing discord link update skipped: the lookup failed for all {count} player accounts", playerTags.Count);
            return;
        }

        var linked = 0;
        var unlinked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var link in links)
        {
            if (link.Value is null)
            {
                unlinked.Add(link.Key);
                continue;
            }
            _botDb.AddOrUpdateDiscordLink(new DiscordLink { DiscordId = link.Value.Value, PlayerTag = link.Key });
            linked++;
        }

        // Answered-and-unlinked: drop our copy so the table stops resolving accounts the owner has
        // since unlinked. Tags absent from `links` were never answered and keep their rows.
        var stale = storedLinks.Where(dl => unlinked.Contains(dl.PlayerTag)).ToList();
        if (stale.Count > 0)
        {
            _botDb.DiscordLinks.RemoveRange(stale);
            _logger.LogInformation("Pruned {count} discord link(s) no longer linked at ClashKing: {playerTags}",
                stale.Count, JsonConvert.SerializeObject(stale.Select(dl => dl.PlayerTag)));
        }

        _botDb.SaveChanges();

        var unanswered = playerTags.Count(t => !links.ContainsKey(t));
        _logger.LogInformation("Updated discord links: {linked} linked, {unlinked} unlinked, {unanswered} unanswered of {asked} accounts asked",
            linked, unlinked.Count, unanswered, playerTags.Count);
    }

    /// <summary>
    /// Manually write a link into the bot's table. <b>The /links add command is currently disabled</b>
    /// (see LinksModule): now that <see cref="Update"/> prunes, a hand-written row for an account
    /// ClashKing doesn't know is deleted again on the next run, so this only makes sense while the
    /// endpoint is down. Kept for that case; overwrites whatever the table held for that tag.
    /// </summary>
    public async Task<Embed> Add(string playerTag, SocketUser user)
    {
        var tag = NormalizeTag(playerTag);
        // 12 is the column's MaxLength; anything outside that never came from CoC.
        if (tag.Length is < 4 or > 12)
            return _embedHelper.ErrorEmbed("Error", $"`{playerTag}` is not a valid player tag.");

        // Best effort: the link is still worth storing when the CoC API can't confirm the name --
        // this command exists precisely for the moments when a lookup is unavailable.
        var player = (await _playersClient.GetOrFetchPlayersAsync([tag])).FirstOrDefault();
        var existing = _botDb.DiscordLinks.AsNoTracking().FirstOrDefault(dl => dl.PlayerTag == tag);

        _botDb.AddOrUpdateDiscordLink(new DiscordLink { PlayerTag = tag, DiscordId = user.Id });
        _botDb.SaveChanges();

        _logger.LogInformation("Manually linked {playerTag} to discord user {discordId}", tag, user.Id);

        var description = new StringBuilder($"Linked `{tag}`{(player is null ? "" : $" ({player.Name})")} to {MentionUtils.MentionUser(user.Id)}.");
        if (existing is not null && existing.DiscordId != user.Id)
            description.Append($"\nReplaced the previous link to {MentionUtils.MentionUser(existing.DiscordId)}.");
        if (player is null)
            description.Append("\n:warning: Could not verify this tag against the CoC API, so double check it.");

        return new EmbedBuilder()
            .WithTitle("Discord Link Added")
            .WithDescription(description.ToString())
            .WithColor(Color.Purple)
            .WithFooter("Bot database only. The next links update overwrites this with what ClashKing says.")
            .Build();
    }

    /// <summary>
    /// Drop a link from the bot's table. <b>The /links remove command is currently disabled</b>
    /// (see LinksModule): <see cref="Update"/> prunes unlinked rows by itself now, and a link
    /// ClashKing still knows about comes straight back on the next run. Kept for manual use.
    /// </summary>
    public Embed Remove(string playerTag)
    {
        var tag = NormalizeTag(playerTag);
        var existing = _botDb.DiscordLinks.Where(dl => dl.PlayerTag == tag).ToList();

        if (existing.Count == 0)
            return _embedHelper.ErrorEmbed("Error", $"No link stored for `{tag}` in the bot's database.");

        _botDb.DiscordLinks.RemoveRange(existing);
        _botDb.SaveChanges();

        _logger.LogInformation("Manually removed {count} discord link(s) for {playerTag}", existing.Count, tag);

        var mentions = string.Join(", ", existing.Select(dl => MentionUtils.MentionUser(dl.DiscordId)).Distinct());

        return new EmbedBuilder()
            .WithTitle("Discord Link Removed")
            .WithDescription($"Removed the link from `{tag}` to {mentions}.")
            .WithColor(Color.Purple)
            .WithFooter("Bot database only. The next links update restores it if ClashKing still has it.")
            .Build();
    }

    /// <summary>
    /// What the bot's own table holds for a user or a player tag -- deliberately not the API, so the
    /// answer shows exactly what the backup would serve during an outage. Exactly one of the two
    /// arguments must be given.
    /// </summary>
    public async Task<Embed> Lookup(SocketUser? user, string? playerTag)
    {
        if ((user is null) == (playerTag is null))
            return _embedHelper.ErrorEmbed("Error", "Provide either a user or a player tag, not both.");

        var tag = playerTag is null ? null : NormalizeTag(playerTag);

        var links = user is not null
            ? _botDb.DiscordLinks.AsNoTracking().Where(dl => dl.DiscordId == user.Id).ToList()
            : _botDb.DiscordLinks.AsNoTracking().Where(dl => dl.PlayerTag == tag).ToList();

        var subject = user is not null ? MentionUtils.MentionUser(user.Id) : $"`{tag}`";

        var builder = new EmbedBuilder()
            .WithTitle("Discord Links")
            .WithColor(Color.DarkPurple)
            .WithFooter("Bot database only. ClashKing may know links that are not stored here.");

        if (links.Count == 0)
            return builder.WithDescription($"No links stored for {subject} in the bot's database.").Build();

        var players = await _playersClient.GetOrFetchPlayersAsync(links.Select(l => l.PlayerTag));
        var nameByTag = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in players)
            nameByTag[p.Tag] = p.Name;

        var data = new List<string[]>
        {
            new[] { "PlayerTag", "Name", "DiscordId", "Updated" }
        };
        foreach (var link in links.OrderBy(l => nameByTag.GetValueOrDefault(l.PlayerTag, "")))
        {
            data.Add([
                link.PlayerTag,
                nameByTag.GetValueOrDefault(link.PlayerTag, "?"),
                link.DiscordId.ToString(),
                link.UpdatedAt.ToString("yyyy-MM-dd")]);
        }

        var header = user is not null
            ? $"{links.Count} account(s) linked to {subject}."
            : $"{subject} is linked to {string.Join(", ", links.Select(l => MentionUtils.MentionUser(l.DiscordId)).Distinct())}.";

        var table = _embedHelper.FormatAsTable(data, TextAlign.Left, TextAlign.Left);

        return builder.WithDescription($"{header}\n```\n{table}\n```").Build();
    }

    /// <summary>Tags typed by hand arrive in every shape; the table stores them the way CoC does.</summary>
    private static string NormalizeTag(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;
        var tag = raw.Trim().ToUpperInvariant().Replace("O", "0");
        return tag.StartsWith('#') ? tag : "#" + tag;
    }
}
