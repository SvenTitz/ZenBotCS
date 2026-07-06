using CocApi.Rest.Apis;

namespace ZenBotCS.Web.Services;

/// <summary>
/// Thin wrapper over the <b>official</b> Clash of Clans API via the CocApi.Rest client (the same
/// library the bot uses) — just the player lookup "Add player" needs. Official data is authoritative
/// and current, unlike the ClashKing cache. Uses only CocApi.Rest (the REST client), NOT CocApi.Cache,
/// so the site fetches live data without any of the cache's background download workers.
/// The API key is IP-locked; set <c>CocApiToken</c> to a key whitelisted for the server IP.
/// </summary>
public class CocApiClient(IPlayersApi playersApi, ILogger<CocApiClient> logger)
{
    /// <summary>Current player name + town hall for a tag, or null if not found / the API errors.</summary>
    public async Task<(string Name, int TownHall)?> GetPlayerAsync(string playerTag, CancellationToken ct = default)
    {
        try
        {
            var response = await playersApi.FetchPlayerOrDefaultAsync(playerTag, ct);
            if (response is null || !response.IsOk)
            {
                // 404 = no such tag (expected). Anything else (esp. 403) usually means the token is
                // missing/invalid or the request IP isn't on the key's whitelist — surface it.
                if (response is not null && !response.IsNotFound)
                    logger.LogWarning("CoC API player lookup for {Tag} returned {Status}", playerTag, response.StatusCode);
                return null;
            }

            var player = response.Ok();
            return player is null ? null : (player.Name, player.TownHallLevel);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "CoC API player lookup failed for {Tag}", playerTag);
            return null;
        }
    }
}
