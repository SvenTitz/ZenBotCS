using Microsoft.Extensions.Caching.Memory;
using MySqlConnector;

namespace ZenBotCS.Web.Services;

/// <summary>
/// Player-tag suggestions for the "Add player" field, mirroring the bot's PlayerTagAutocompleteHandler:
/// every CoC-cached player (family members), matched by a case-insensitive substring of the name and
/// shown as "Name^TH (Tag)" with the tag as the value. The cached players live in the CoC cache DB
/// (same DB as <see cref="ClanNameService"/>); the list is loaded once and memory-cached as it changes slowly.
/// </summary>
public class PlayerSuggestionService(IConfiguration config, IMemoryCache cache, ILogger<PlayerSuggestionService> logger)
{
    private const string CacheKey = "cached-players";

    /// <summary>Up to 25 cached players whose name contains <paramref name="query"/> (case-insensitive).</summary>
    public async Task<IReadOnlyList<PlayerSuggestion>> SearchAsync(string? query, CancellationToken ct = default)
    {
        var all = await GetAllAsync(ct);
        var matches = string.IsNullOrWhiteSpace(query)
            ? all
            : all.Where(p => p.Name.Contains(query, StringComparison.InvariantCultureIgnoreCase));
        return matches.Take(25).ToList();
    }

    private async Task<IReadOnlyList<PlayerSuggestion>> GetAllAsync(CancellationToken ct)
    {
        return await cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);
            return (IReadOnlyList<PlayerSuggestion>)await LoadAsync(ct);
        }) ?? [];
    }

    private async Task<List<PlayerSuggestion>> LoadAsync(CancellationToken ct)
    {
        var byTag = new Dictionary<string, PlayerSuggestion>();
        var connectionString = config["CocApiCacheConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.LogWarning("CocApiCacheConnectionString not configured; player suggestions unavailable.");
            return [];
        }

        try
        {
            await using var conn = new MySqlConnection(connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT Tag, " +
                "JSON_UNQUOTE(JSON_EXTRACT(RawContent, '$.name')) AS Name, " +
                "JSON_UNQUOTE(JSON_EXTRACT(RawContent, '$.townHallLevel')) AS Th " +
                "FROM player WHERE RawContent IS NOT NULL";
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                if (reader.IsDBNull(1))
                    continue;
                var tag = reader.GetString(0);
                var name = reader.GetString(1);
                var th = !reader.IsDBNull(2) && int.TryParse(reader.GetString(2), out var t) ? t : 0;
                byTag[tag] = new PlayerSuggestion(tag, name, th); // dedupe by tag, last row wins
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read cached players from the cache DB.");
        }

        return byTag.Values
            .OrderBy(p => p.Name, StringComparer.InvariantCultureIgnoreCase)
            .ToList();
    }
}

/// <summary>A cached player offered as an "Add player" suggestion. <see cref="Tag"/> is the value used.</summary>
public record PlayerSuggestion(string Tag, string Name, int TownHall)
{
    /// <summary>"Name^TH (Tag)" — matches the Discord autocomplete label.</summary>
    public string Label => $"{Name}{Superscript(TownHall)} ({Tag})";

    private static string Superscript(int number)
    {
        const string digits = "⁰¹²³⁴⁵⁶⁷⁸⁹"; // ⁰¹²³⁴⁵⁶⁷⁸⁹
        return new string(number.ToString().Select(c => digits[c - '0']).ToArray());
    }
}
