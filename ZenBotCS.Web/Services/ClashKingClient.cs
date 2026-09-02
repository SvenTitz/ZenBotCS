using System.Net.Http.Json;
using Newtonsoft.Json;
using ZenBotCS.Entities.Models.ClashKingApi;
using ZenBotCS.Entities.Models.ClashKingApi.Links;

namespace ZenBotCS.Web.Services;

/// <summary>
/// Minimal client for the ClashKing API (https://api.clashk.ing) — only the Discord-link lookup that
/// "Add player" needs, plus the war history the CWL pages read. Player name/TH comes from the
/// official CoC API (<see cref="CocApiClient"/>); ClashKing is the source for tag→Discord links,
/// which the official API doesn't provide.
/// Stateless; registered as a typed <see cref="HttpClient"/> so the handler is pooled and thread-safe.
/// The developer token (<c>CkApiToken</c>) is set as a default header at registration —
/// <c>/v2/links/shared</c> 401s without it.
/// </summary>
public class ClashKingClient(HttpClient http, ILogger<ClashKingClient> logger)
{
    /// <summary>
    /// The Discord user id linked to a player tag, or null if unlinked / the API errors. Callers
    /// that need an answer either way fall back to the bot's own DiscordLinks table.
    /// </summary>
    public async Task<ulong?> GetDiscordUserIdAsync(string playerTag, CancellationToken ct = default)
    {
        try
        {
            // POST /v2/links/shared with a list of tags → { "items": [ { user_id, player_tag, is_verified } ] }.
            // An unlinked tag is omitted from items rather than returned with a null value.
            using var resp = await http.PostAsJsonAsync("v2/links/shared", new { player_tags = new[] { playerTag } }, ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("ClashKing discord-link lookup for {tag} returned {status}", playerTag, resp.StatusCode);
                return null;
            }

            var json = await resp.Content.ReadAsStringAsync(ct);
            var link = JsonConvert.DeserializeObject<SharedLinksResponse>(json)?.Items.FirstOrDefault();

            return ulong.TryParse(link?.UserId, out var id) ? id : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ClashKing discord-link lookup failed for {tag}", playerTag);
            return null;
        }
    }

    /// <summary>
    /// A clan's ended-war history from <c>/v2/clan/{tag}/wars</c> (CWL wars carry a <c>tag</c>;
    /// regular wars don't). <paramref name="limit"/> is capped at 500 by the endpoint. Deserialised
    /// with Newtonsoft so the <see cref="WarData"/> <c>[JsonProperty]</c> maps apply.
    /// Returns null on error.
    /// </summary>
    public async Task<List<WarData>?> GetClanWarHistoryAsync(
        string clanTag, int limit = 200, DateTimeOffset? endedAfter = null, DateTimeOffset? endedBefore = null,
        CancellationToken ct = default)
    {
        try
        {
            var url = $"v2/clan/{Uri.EscapeDataString(clanTag)}/wars?limit={Math.Clamp(limit, 1, 500)}"
                + TimeWindowQuery(endedAfter, endedBefore);

            using var resp = await http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("ClashKing war-history lookup for {tag} returned {status}", clanTag, resp.StatusCode);
                return null;
            }

            var json = await resp.Content.ReadAsStringAsync(ct);
            return JsonConvert.DeserializeObject<WarDataResponse>(json)?.Items;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ClashKing war-history lookup failed for {tag}", clanTag);
            return null;
        }
    }

    // v2 filters wars by ISO-8601 end time through bracketed query keys, percent-encoded so they
    // survive as a literal query string.
    private static string TimeWindowQuery(DateTimeOffset? after, DateTimeOffset? before)
    {
        var query = "";
        if (after is not null)
            query += $"&time%5Bafter%5D={Uri.EscapeDataString(after.Value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ"))}";
        if (before is not null)
            query += $"&time%5Bbefore%5D={Uri.EscapeDataString(before.Value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ"))}";
        return query;
    }
}
