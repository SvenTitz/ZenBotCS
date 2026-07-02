using System.Net.Http.Json;
using System.Text.Json;

namespace ZenBotCS.Web.Services;

/// <summary>
/// Minimal client for the ClashKing API (https://api.clashk.ing) — only the two lookups the roster
/// site's "Add player" needs: a player's name/town hall by tag, and their linked Discord user id.
/// Stateless; registered as a typed <see cref="HttpClient"/> so the handler is pooled and thread-safe.
/// </summary>
public class ClashKingClient(HttpClient http, ILogger<ClashKingClient> logger)
{
    /// <summary>Player name + town hall for a tag, or null if the tag isn't found / the API errors.</summary>
    public async Task<(string Name, int TownHall)?> GetPlayerAsync(string playerTag, CancellationToken ct = default)
    {
        try
        {
            using var resp = await http.GetAsync($"player/{Uri.EscapeDataString(playerTag)}/stats", ct);
            if (!resp.IsSuccessStatusCode)
                return null;

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;
            if (!root.TryGetProperty("name", out var nameEl) || nameEl.ValueKind != JsonValueKind.String)
                return null;

            var th = root.TryGetProperty("townhall", out var thEl) && thEl.TryGetInt32(out var t) ? t : 0;
            return (nameEl.GetString()!, th);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ClashKing player lookup failed for {tag}", playerTag);
            return null;
        }
    }

    /// <summary>The Discord user id linked to a player tag, or null if unlinked / the API errors.</summary>
    public async Task<ulong?> GetDiscordUserIdAsync(string playerTag, CancellationToken ct = default)
    {
        try
        {
            // POST /discord_links with a list of tags → { "#TAG": <discord id or null>, ... }
            using var resp = await http.PostAsJsonAsync("discord_links", new[] { playerTag }, ct);
            if (!resp.IsSuccessStatusCode)
                return null;

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty(playerTag, out var idEl))
                return null;

            return idEl.ValueKind switch
            {
                JsonValueKind.Number when idEl.TryGetUInt64(out var id) => id,
                JsonValueKind.String when ulong.TryParse(idEl.GetString(), out var id) => id,
                _ => null,
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ClashKing discord-link lookup failed for {tag}", playerTag);
            return null;
        }
    }
}
