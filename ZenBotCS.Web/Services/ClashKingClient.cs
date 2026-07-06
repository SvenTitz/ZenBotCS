using System.Net.Http.Json;
using System.Text.Json;

namespace ZenBotCS.Web.Services;

/// <summary>
/// Minimal client for the ClashKing API (https://api.clashk.ing) — only the Discord-link lookup that
/// "Add player" needs. Player name/TH comes from the official CoC API (<see cref="CocApiClient"/>);
/// ClashKing is the source for tag→Discord links, which the official API doesn't provide.
/// Stateless; registered as a typed <see cref="HttpClient"/> so the handler is pooled and thread-safe.
/// </summary>
public class ClashKingClient(HttpClient http, ILogger<ClashKingClient> logger)
{
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
