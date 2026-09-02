using Microsoft.Extensions.Logging;
using ZenBotCS.Clients;
using ZenBotCS.Entities;
using ZenBotCS.Entities.Models;
using ZenBotCS.Entities.Models.ClashKingApi.PlayerWarHits;

namespace ZenBotCS.Services;

/// <summary>
/// Read-through cache over the player war hits ClashKing serves. Commands that walk a whole clan
/// (<c>/clan stats attacks</c>, the CWL roster hitrates) would otherwise make one API call per member
/// on every invocation. <see cref="Background.PlayerWarHitsUpdateService"/> keeps the table warm;
/// this fills gaps on demand for players the worker hasn't reached.
/// </summary>
public class ClashKingApiService(ClashKingApiClient _ckApiClient, BotDataContext _botDb, ILogger<ClashKingApiService> _logger)
{
    /// <summary>How long a cached entry is served before it's refetched.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromDays(1);

    /// <summary>The war-hit window the cache stores, in days. Matches the worker.</summary>
    public const uint LimitDays = 100;

    public async Task<PlayerWarhits?> GetOrFetchPlayerWarhitsAsync(string playerTag)
    {
        var cached = _botDb.PlayerWarHitsCaches.FirstOrDefault(c => c.PlayerTag == playerTag);
        if (cached?.WarHits is not null && cached.UpdatedAt >= DateTime.UtcNow - MaxAge)
            return cached.WarHits;

        var fetched = await _ckApiClient.GetPlayerWarAttacksAsync(playerTag, LimitDays);
        if (fetched is null)
            return cached?.WarHits; // stale beats nothing when the API is unreachable

        Store(cached, playerTag, fetched);
        return fetched;
    }

    // Write the fresh copy back so the next caller (and the next command) is a DB hit. A failure here
    // is logged and swallowed -- refreshing a cache must never fail the command that triggered it.
    private void Store(PlayerWarHitsCache? existing, string playerTag, PlayerWarhits warHits)
    {
        try
        {
            if (existing is null)
            {
                existing = new PlayerWarHitsCache { PlayerTag = playerTag };
                _botDb.PlayerWarHitsCaches.Add(existing);
            }
            existing.WarHits = warHits;
            existing.UpdatedAt = DateTime.UtcNow;
            _botDb.SaveChanges();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache war hits for {playerTag}", playerTag);
        }
    }
}
