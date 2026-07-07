using MySqlConnector;
using Newtonsoft.Json;
using ZenBotCS.Entities.Models.ClashKingApi;

namespace ZenBotCS.Web.Services;

/// <summary>
/// Reads the <b>current</b> CWL's wars for a clan straight from the CoC cache DB
/// (CocApiCacheConnectionString) — the bot fills it live, so it has every round (including the
/// in-progress one) with correct orientation, unlike ClashKing's <c>/war/previous</c> which only
/// exposes finished wars piecemeal. The cached <c>war.RawContent</c> is the raw CoC war JSON (same
/// shape as <see cref="WarData"/>); the war tag lives in the separate <c>WarTag</c> column, so it's
/// injected after deserialisation. Mirrors <see cref="ClanNameService"/>'s cache-DB access.
/// </summary>
public class CocCacheCwlService(IConfiguration config, ILogger<CocCacheCwlService> logger)
{
    // State column: 2 = preparation, 3 = inWar, 4 = warEnded. >=3 means the war has started.
    private const int StateInWar = 3;

    /// <summary>
    /// The started wars (in-war or ended) of the clan's current CWL — those whose preparation began
    /// within <paramref name="windowDays"/> days. Empty when no CWL is active or the cache is unset.
    /// </summary>
    public async Task<List<WarData>> GetCurrentCwlWarsAsync(string clanTag, int windowDays = 9, CancellationToken ct = default)
    {
        var result = new List<WarData>();
        var connectionString = config["CocApiCacheConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
            return result;

        var since = DateTime.UtcNow.AddDays(-windowDays);

        try
        {
            await using var conn = new MySqlConnection(connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT WarTag, RawContent FROM war " +
                "WHERE Type = 3 AND State >= @state AND PreparationStartTime >= @since " +
                "AND (JSON_UNQUOTE(JSON_EXTRACT(RawContent, '$.clan.tag')) = @tag " +
                "  OR JSON_UNQUOTE(JSON_EXTRACT(RawContent, '$.opponent.tag')) = @tag)";
            cmd.Parameters.AddWithValue("@state", StateInWar);
            cmd.Parameters.AddWithValue("@since", since);
            cmd.Parameters.AddWithValue("@tag", clanTag);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var warTag = reader.IsDBNull(0) ? null : reader.GetString(0);
                var raw = reader.IsDBNull(1) ? null : reader.GetString(1);
                if (string.IsNullOrEmpty(raw))
                    continue;

                var war = JsonConvert.DeserializeObject<WarData>(raw);
                if (war is null)
                    continue;

                war.WarTag = warTag; // RawContent has no tag; carry the CWL tag from its column
                result.Add(war);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read current CWL wars from the cache DB for {tag}", clanTag);
        }

        return result;
    }
}
