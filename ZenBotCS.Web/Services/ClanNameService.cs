using Microsoft.Extensions.Caching.Memory;
using MySqlConnector;

namespace ZenBotCS.Web.Services;

/// <summary>
/// Resolves clan tag → clan name. Names aren't in the bot DB; they live in the CoC cache DB
/// (CocApiCacheConnectionString) inside each row's cached JSON (clan.RawContent.$.name).
/// Results are cached in memory since clan names rarely change.
/// </summary>
public class ClanNameService(IConfiguration config, IMemoryCache cache, ILogger<ClanNameService> logger)
{
    private const string CacheKey = "clan-names";

    /// <summary>Clan name for a tag, or the tag itself if not found.</summary>
    public async Task<string> GetNameOrTagAsync(string clanTag, CancellationToken ct = default)
    {
        var names = await GetAllAsync(ct);
        return names.TryGetValue(clanTag, out var name) ? name : clanTag;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);
            return await LoadAsync(ct);
        }) ?? new Dictionary<string, string>();
    }

    private async Task<Dictionary<string, string>> LoadAsync(CancellationToken ct)
    {
        var result = new Dictionary<string, string>();
        var connectionString = config["CocApiCacheConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.LogWarning("CocApiCacheConnectionString not configured; clan names will fall back to tags.");
            return result;
        }

        try
        {
            await using var conn = new MySqlConnection(connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT Tag, JSON_UNQUOTE(JSON_EXTRACT(RawContent, '$.name')) AS Name " +
                "FROM clan WHERE RawContent IS NOT NULL";
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                if (!reader.IsDBNull(1))
                    result[reader.GetString(0)] = reader.GetString(1);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read clan names from the cache DB; falling back to tags.");
        }

        return result;
    }
}
