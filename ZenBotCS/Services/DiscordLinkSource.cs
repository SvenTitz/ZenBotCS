using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZenBotCS.Clients;
using ZenBotCS.Entities;
using ZenBotCS.Entities.Models;

namespace ZenBotCS.Services;

/// <summary>
/// Single source of truth for "which Discord user owns this player tag" and the reverse. ClashKing's
/// /v2/links/shared endpoint is asked first; whenever it has no answer -- it is down, or it simply
/// does not know the tag -- the bot's own <c>DiscordLinks</c> table answers instead. That table is a
/// rolling copy of the API kept by <see cref="Background.DiscordLinkUpdateService"/> and topped up
/// here on every successful lookup, so it stays usable while the endpoint is broken.
///
/// The backup can be stale: a player who unlinked upstream keeps resolving to their old user until
/// the row is replaced. That is the deliberate trade -- a CWL signup that works off last-known data
/// beats one that refuses to run at all.
/// </summary>
public class DiscordLinkSource(
    BotDataContext _botDb,
    ClashKingApiClient _ckApiClient,
    ILogger<DiscordLinkSource> _logger)
{
    /// <summary>The Discord user linked to a player tag, or null if neither source knows it.</summary>
    public async Task<ulong?> GetDiscordIdAsync(string playerTag)
    {
        var links = await GetDiscordIdsAsync([playerTag]);
        return links.TryGetValue(playerTag, out var discordId) ? discordId : null;
    }

    /// <summary>
    /// Player tag -> Discord user id for a batch of tags. Tags neither source knows are absent from
    /// the result rather than mapped to null. Keys compare case-insensitively, so a caller can look
    /// up with the casing it passed in.
    /// </summary>
    public async Task<Dictionary<string, ulong>> GetDiscordIdsAsync(IEnumerable<string> playerTags)
    {
        var tags = playerTags.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var links = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);
        if (tags.Count == 0)
            return links;

        var apiLinks = await _ckApiClient.PostDiscordLinksAsync(tags);
        if (apiLinks is null)
        {
            _logger.LogWarning("ClashKing discord link lookup failed for {count} player tags, falling back to the bot's link table", tags.Count);
        }
        else
        {
            foreach (var kvp in apiLinks)
            {
                if (kvp.Value is null)
                    continue;
                links[kvp.Key] = kvp.Value.Value;
            }
            CacheLinks(links);
        }

        var missing = tags.Where(t => !links.ContainsKey(t)).ToList();
        if (missing.Count == 0)
            return links;

        var cached = _botDb.DiscordLinks.AsNoTracking()
            .Where(dl => missing.Contains(dl.PlayerTag))
            .ToList();

        foreach (var link in cached)
            links[link.PlayerTag] = link.DiscordId;

        if (cached.Count > 0)
            _logger.LogInformation("Resolved {count} of {missing} unanswered player tags from the bot's link table", cached.Count, missing.Count);

        return links;
    }

    /// <summary>
    /// The player tags linked to a Discord user. Falls back to the bot's link table both when the
    /// API errors and when it answers with nothing -- either way the user would otherwise look like
    /// they have no accounts.
    /// </summary>
    public async Task<List<string>> GetPlayerTagsAsync(ulong discordId)
    {
        var apiTags = await _ckApiClient.PostDiscordLinksAsync(discordId);

        if (apiTags is null)
            _logger.LogWarning("ClashKing discord link lookup failed for user {discordId}, falling back to the bot's link table", discordId);

        if (apiTags is { Count: > 0 })
        {
            CacheLinks(apiTags.ToDictionary(tag => tag, _ => discordId, StringComparer.OrdinalIgnoreCase));
            return apiTags;
        }

        var cached = _botDb.DiscordLinks.AsNoTracking()
            .Where(dl => dl.DiscordId == discordId)
            .Select(dl => dl.PlayerTag)
            .ToList();

        if (cached.Count > 0)
            _logger.LogInformation("Resolved {count} player tags for user {discordId} from the bot's link table", cached.Count, discordId);

        return cached;
    }

    /// <summary>
    /// Mirror freshly fetched links into the bot's table so the backup keeps improving -- this is
    /// what makes accounts outside the tracked clans survive an outage. Saves immediately; a failure
    /// here is logged and swallowed, because refreshing a cache must never fail the command that
    /// triggered it.
    /// </summary>
    private void CacheLinks(Dictionary<string, ulong> links)
    {
        if (links.Count == 0)
            return;

        try
        {
            foreach (var link in links)
                _botDb.AddOrUpdateDiscordLink(new DiscordLink { PlayerTag = link.Key, DiscordId = link.Value });
            _botDb.SaveChanges();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache {count} discord links", links.Count);
        }
    }
}
