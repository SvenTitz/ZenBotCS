using System.Text;
using CocApi.Cache;
using CocApi.Rest.Models;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ZenBotCS.Entities;
using ZenBotCS.Entities.Models;
using ZenBotCS.Extensions;
using ZenBotCS.Helper;
using ZenBotCS.Models.Enums;

namespace ZenBotCS.Services.SlashCommands;

public class StrikeService(BotDataContext _botDb, EmbedHelper _embedHelper, PlayersClient _playersClient, DiscordSocketClient _discordClient, IConfiguration _config, ILogger<StrikeService> _logger)
{
    /// <summary>Insert attempts before giving up on a unique strike ID; the unique index is the backstop.</summary>
    private const int MaxInsertAttempts = 2;

    public async Task<Embed> Add(string playerInput, string reason, int? rolloverDays, int weight, SocketUser createdBy)
    {
        var cachedPlayers = await _playersClient.GetCachedPlayersAsync();

        var (player, error) = await ResolvePlayerAsync(playerInput, cachedPlayers);
        if (player is null)
            return error!;

        var strike = new Strike
        {
            PlayerTag = PlayerInputParser.NormalizeTag(player.Tag),
            PlayerName = player.Name,
            Reason = reason,
            Weight = weight,
            RolloverDays = rolloverDays,
            CreatedByDiscordId = createdBy.Id,
        };

        try
        {
            await InsertWithUniqueIdAsync(strike);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save strike for {PlayerTag}", strike.PlayerTag);
            return _embedHelper.ErrorEmbed("Strike not saved", "The strike could not be saved. Please try again.");
        }

        var rollsOver = strike.RollsOverAt is { } rollsOverAt
            ? $"{Timestamp(rollsOverAt, "R")} ({strike.RolloverDays} days)"
            : "never";

        var embed = new EmbedBuilder()
            .WithTitle("Strike Added")
            .WithColor(Color.Purple)
            .AddField("Player", $"{player.Name} ({strike.PlayerTag})")
            .AddField("Strike ID", $"`{strike.StrikeId}`", inline: true)
            .AddField("Weight", strike.Weight, inline: true)
            .AddField("Rolls over", rollsOver, inline: true)
            .AddField("Reason", strike.Reason)
            .AddField("Given by", $"<@{createdBy.Id}>")
            .Build();

        await PostToLogChannelAsync(embed);
        return embed;
    }

    public async Task<Embed> Remove(string strikeInput, SocketUser removedBy)
    {
        var strikeId = NormalizeStrikeId(strikeInput);

        var strike = await _botDb.Strikes.FirstOrDefaultAsync(s => s.StrikeId == strikeId);
        if (strike is null)
            return _embedHelper.ErrorEmbed("Strike not found", $"No strike found with ID `{strikeId}`. Get valid IDs from /strike list.");

        if (strike.IsRemoved)
        {
            var removedByWhom = strike.RemovedByDiscordId is { } removedById ? $" by <@{removedById}>" : string.Empty;
            return _embedHelper.ErrorEmbed("Strike already removed", $"`{strike.StrikeId}` was already removed {Timestamp(strike.RemovedAt!.Value, "d")}{removedByWhom}.");
        }

        strike.RemovedAt = DateTime.UtcNow;
        strike.RemovedByDiscordId = removedBy.Id;
        await _botDb.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var remaining = (await _botDb.Strikes
                .Where(s => s.PlayerTag == strike.PlayerTag && s.RemovedAt == null)
                .ToListAsync())
            .Where(s => s.IsActiveAt(now))
            .ToList();

        var embed = new EmbedBuilder()
            .WithTitle("Strike Removed")
            .WithColor(Color.Green)
            .WithDescription($"`{strike.StrikeId}` (weight {strike.Weight}) for {strike.PlayerName} ({strike.PlayerTag}) was removed.")
            .AddField("Original reason", strike.Reason)
            .AddField("Remaining active weight for this player", $"{remaining.Sum(s => s.Weight)} ({remaining.Count} strike{Plural(remaining.Count)})")
            .AddField("Removed by", $"<@{removedBy.Id}>")
            .Build();

        await PostToLogChannelAsync(embed);
        return embed;
    }

    /// <summary>
    /// Mirrors an add/remove into the <c>StrikeLogChannelId</c> channel. Optional: unset or 0 skips it.
    /// The strike is already saved by now, so a failed post is logged and never fails the command.
    /// </summary>
    private async Task PostToLogChannelAsync(Embed embed)
    {
        if (!ulong.TryParse(_config["StrikeLogChannelId"], out var channelId) || channelId == 0)
            return;

        try
        {
            if (await _discordClient.GetChannelAsync(channelId) is not ITextChannel channel)
            {
                _logger.LogWarning("Strike log channel {ChannelId} not found", channelId);
                return;
            }

            await channel.SendMessageAsync(embed: embed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post to strike log channel {ChannelId}", channelId);
        }
    }

    public async Task<Embed[]> List(StrikeListView view, string? clanTag, string? playerInput, int? strikeAmount, bool viewExpiredStrikes)
    {
        if (strikeAmount is not null && view == StrikeListView.Strike)
            return [_embedHelper.ErrorEmbed("Invalid options", "`strike_amount` can only be used with the player view.")];

        var now = DateTime.UtcNow;

        // One cache read for both the player resolver and the clan filter.
        var cachedPlayers = clanTag is not null || playerInput is not null
            ? await _playersClient.GetCachedPlayersAsync()
            : [];

        var filterLines = new List<string>();
        var query = _botDb.Strikes.Where(s => s.RemovedAt == null);

        // Resolve the player first so the filter runs in SQL (PlayerTag is indexed).
        if (playerInput is not null)
        {
            var (player, error) = await ResolvePlayerAsync(playerInput, cachedPlayers);
            if (player is null)
                return [error!];

            var playerTag = PlayerInputParser.NormalizeTag(player.Tag);
            query = query.Where(s => s.PlayerTag == playerTag);

            filterLines.Add($"Player: {player.Name} ({playerTag})");
        }

        var strikes = await query.ToListAsync();

        var active = strikes.Where(s => s.IsActiveAt(now)).ToList();
        var rolledOver = strikes.Where(s => !s.IsActiveAt(now)).ToList();

        if (clanTag is not null)
        {
            var normalizedClanTag = PlayerInputParser.NormalizeTag(clanTag);

            // Strikes filter by CURRENT clan membership: players who left the family only show up unfiltered.
            var clanByPlayer = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var player in cachedPlayers)
            {
                if (player.Clan is not null)
                    clanByPlayer[player.Tag] = player.Clan.Tag;
            }

            bool CurrentlyInClan(Strike strike) =>
                clanByPlayer.TryGetValue(strike.PlayerTag, out var currentClanTag)
                && string.Equals(currentClanTag, normalizedClanTag, StringComparison.OrdinalIgnoreCase);

            active = active.Where(CurrentlyInClan).ToList();
            rolledOver = rolledOver.Where(CurrentlyInClan).ToList();

            // The cached players are the only place we know clan names from; fall back to the tag.
            var clanName = cachedPlayers
                .FirstOrDefault(p => p.Clan is not null && string.Equals(p.Clan.Tag, normalizedClanTag, StringComparison.OrdinalIgnoreCase))
                ?.Clan?.Name;

            filterLines.Add($"Clan: {(clanName is null ? normalizedClanTag : $"{clanName} ({normalizedClanTag})")}");
        }

        if (strikeAmount is not null)
        {
            // Weight is the tally: a weight-2 strike counts as two strikes.
            var qualifyingPlayers = active
                .GroupBy(s => s.PlayerTag, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Sum(s => s.Weight) >= strikeAmount.Value)
                .Select(g => g.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            active = active.Where(s => qualifyingPlayers.Contains(s.PlayerTag)).ToList();
            rolledOver = rolledOver.Where(s => qualifyingPlayers.Contains(s.PlayerTag)).ToList();
        }

        // Rolled-over strikes are shown only when asked for, clearly marked, and never counted as active.
        var shown = viewExpiredStrikes ? active.Concat(rolledOver).ToList() : active;

        if (shown.Count == 0)
        {
            var message = "No strikes found for the given filters.";
            if (!viewExpiredStrikes && rolledOver.Count > 0)
                message += " Rolled-over strikes are hidden; enable `view_expired_strikes` to include them.";

            return [new EmbedBuilder().WithTitle("Strikes").WithColor(Color.Purple).WithDescription(message).Build()];
        }

        var playerCount = shown
            .Select(s => s.PlayerTag)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        var summary = new StringBuilder($"**{playerCount} player{Plural(playerCount)}, {active.Count} active strike{Plural(active.Count)} (total weight {active.Sum(s => s.Weight)})");
        if (viewExpiredStrikes && rolledOver.Count > 0)
            summary.Append($", {rolledOver.Count} rolled over");
        summary.Append("**");

        var description = new StringBuilder();
        description.AppendLine(summary.ToString());
        if (filterLines.Count > 0)
            description.AppendLine(string.Join(" - ", filterLines));
        description.AppendLine();

        description.Append(view == StrikeListView.Player
            ? BuildPlayerView(shown, now)
            : BuildStrikeView(shown, now));

        var baseEmbed = new EmbedBuilder()
            .WithTitle("Strikes")
            .WithColor(Color.Purple);

        return _embedHelper.BuildEmbedsFromLongDescription(description, baseEmbed);
    }

    /// <summary>Accounts with the strikes they have, worst offenders first.</summary>
    private static StringBuilder BuildPlayerView(List<Strike> strikes, DateTime now)
    {
        var builder = new StringBuilder();

        var groups = strikes
            .GroupBy(s => s.PlayerTag, StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                Tag = g.Key,
                Name = g.OrderByDescending(s => s.CreatedAt).First().PlayerName,
                Active = g.Where(s => s.IsActiveAt(now)).ToList(),
                RolledOver = g.Where(s => !s.IsActiveAt(now)).ToList(),
            })
            .OrderByDescending(g => g.Active.Sum(s => s.Weight))
            .ThenByDescending(g => g.Active.Count)
            .ThenBy(g => g.Name)
            .ToList();

        foreach (var group in groups)
        {
            var header = $"**{group.Name}** ({group.Tag}) - {group.Active.Count} active, total weight {group.Active.Sum(s => s.Weight)}";
            if (group.RolledOver.Count > 0)
                header += $" ({group.RolledOver.Count} rolled over)";
            builder.AppendLine(header);

            foreach (var strike in group.Active.Concat(group.RolledOver).OrderByDescending(s => s.CreatedAt))
                builder.AppendLine($"{StrikeLine(strike, now, Timestamp(strike.CreatedAt, "d"))} - {strike.Reason}");

            builder.AppendLine();
        }

        return builder;
    }

    /// <summary>Individual strikes, newest first.</summary>
    private static StringBuilder BuildStrikeView(List<Strike> strikes, DateTime now)
    {
        var builder = new StringBuilder();

        foreach (var strike in strikes.OrderByDescending(s => s.CreatedAt))
        {
            builder.AppendLine(StrikeLine(strike, now, $"added {Timestamp(strike.CreatedAt, "d")} by <@{strike.CreatedByDiscordId}>"));
            builder.AppendLine($"> {strike.Reason}");
            builder.AppendLine();
        }

        return builder;
    }

    private static string StrikeLine(Strike strike, DateTime now, string suffix)
    {
        var line = $"`{strike.StrikeId}` (w{strike.Weight}) {suffix}";
        if (!strike.IsActiveAt(now) && strike.RollsOverAt is { } rolledOverAt)
            line += $" - rolled over {Timestamp(rolledOverAt, "d")}";
        return line;
    }

    /// <summary>
    /// Resolves free text into a player: explicit tag, exact name, tag typed without "#", or a fuzzy
    /// name match. Returns the player, or the error embed to show the user instead.
    /// </summary>
    private async Task<(Player? Player, Embed? Error)> ResolvePlayerAsync(string input, List<Player> cachedPlayers)
    {
        var trimmed = input.Trim();
        if (trimmed.Length == 0)
            return (null, _embedHelper.ErrorEmbed("No player given", "Provide a player name or tag."));

        // A. explicit player tag
        if (trimmed.StartsWith('#'))
        {
            var tag = PlayerInputParser.NormalizeTag(trimmed);
            var player = await FindByTagAsync(tag, cachedPlayers);

            return player is not null
                ? (player, null)
                : (null, _embedHelper.ErrorEmbed("Player not found", $"No Clash of Clans player found for tag `{tag}`."));
        }

        // B. exact name match
        var exactMatches = cachedPlayers
            .Where(p => string.Equals(p.Name, trimmed, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (exactMatches.Count == 1)
            return (exactMatches[0], null);
        if (exactMatches.Count > 1)
            return (null, _embedHelper.ErrorEmbed("Ambiguous player", $"Multiple players are named {trimmed}; use their player tag."));

        // C. a tag typed without "#" (copied from a sheet, for example)
        if (PlayerInputParser.LooksLikeTag(trimmed))
        {
            var tag = PlayerInputParser.NormalizeTag(trimmed);
            var byTag = await FindByTagAsync(tag, cachedPlayers);
            if (byTag is not null)
                return (byTag, null);
        }

        // D. fuzzy name match
        var matches = cachedPlayers
            .Where(p => p.Name?.Contains(trimmed, StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        if (matches.Count == 1)
            return (matches[0], null);
        if (matches.Count > 1)
            return (null, _embedHelper.ErrorEmbed("Ambiguous player", $"Multiple players match {trimmed}; pick from the autocomplete list or use the tag."));

        return (null, _embedHelper.ErrorEmbed("Player not found", $"No player found for {trimmed}."));
    }

    /// <summary>
    /// Looks the tag up in the cache first, then at the CoC API - so strikes work for players the bot
    /// does not track. A failed fetch is "not found", never an exception. Null means the tag resolved
    /// to nothing; callers decide whether that is an error (tag path) or a fall-through (fuzzy path).
    /// </summary>
    private async Task<Player?> FindByTagAsync(string tag, List<Player> cachedPlayers)
    {
        var cached = cachedPlayers.FirstOrDefault(p => IsTag(p.Tag, tag));
        if (cached is not null)
            return cached;

        try
        {
            return await _playersClient.GetOrFetchPlayerAsync(tag);
        }
        catch (Exception ex)
        {
            _playersClient.Logger.LogWarning(ex, "Failed to fetch player with tag {Tag}", tag);
            return null;
        }
    }

    private async Task InsertWithUniqueIdAsync(Strike strike)
    {
        for (var attempt = 1; ; attempt++)
        {
            strike.StrikeId = await GenerateUniqueStrikeIdAsync();
            _botDb.Strikes.Add(strike);

            try
            {
                await _botDb.SaveChangesAsync();
                return;
            }
            catch (DbUpdateException ex) when (attempt < MaxInsertAttempts && IsDuplicateId(ex))
            {
                // The unique index rejected the ID (a race with another insert) - try once more.
                _botDb.Entry(strike).State = EntityState.Detached;
            }
        }
    }

    private async Task<string> GenerateUniqueStrikeIdAsync()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var strikeId = StrikeIdGenerator.Generate();
            if (!await _botDb.Strikes.AnyAsync(s => s.StrikeId == strikeId))
                return strikeId;
        }

        throw new InvalidOperationException("Could not generate a unique strike ID.");
    }

    private static bool IsDuplicateId(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsTag(string? playerTag, string tag) =>
        string.Equals(playerTag, tag, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeStrikeId(string raw) => raw.Trim().TrimStart('#').ToUpperInvariant();

    private static string Plural(int count) => count == 1 ? string.Empty : "s";

    /// <summary>
    /// Discord timestamp markup. Rows read back from MySQL carry <see cref="DateTimeKind.Unspecified"/>,
    /// so pin the value to UTC - otherwise the conversion assumes local time and shifts the timestamp.
    /// </summary>
    private static string Timestamp(DateTime utc, string style) =>
        $"<t:{new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds()}:{style}>";
}
