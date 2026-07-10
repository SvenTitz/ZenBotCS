using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace ZenBotCS.Web.Services;

/// <summary>
/// Fetches the guild's roles and channels from the Discord REST API (using the bot token, exactly like
/// the login role check in Program.cs) so the clan-settings page can suggest real roles/channels instead
/// of asking for raw snowflake ids. Results are cached briefly in memory — a guild's roles/channels change
/// rarely, but not never, so the cache is short-lived and the page offers a manual refresh.
/// </summary>
public class DiscordGuildService(IConfiguration config, IHttpClientFactory httpFactory, IMemoryCache cache, ILogger<DiscordGuildService> logger)
{
    private const string RolesCacheKey = "discord-guild-roles";
    private const string ChannelsCacheKey = "discord-guild-channels";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    // Discord channel types we treat as "text channels a reminder can be posted to".
    // 0 = GuildText, 5 = GuildAnnouncement. https://discord.com/developers/docs/resources/channel#channel-object-channel-types
    private static readonly HashSet<int> TextChannelTypes = [0, 5];
    private const int CategoryChannelType = 4;

    /// <summary>Assignable-looking guild roles, highest first. Empty if the bot token/guild id aren't configured or the call fails.</summary>
    public async Task<IReadOnlyList<DiscordRole>> GetRolesAsync(bool forceRefresh = false, CancellationToken ct = default)
    {
        if (forceRefresh)
            cache.Remove(RolesCacheKey);

        return await cache.GetOrCreateAsync(RolesCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await LoadRolesAsync(ct);
        }) ?? [];
    }

    /// <summary>Text channels of the guild, grouped in listing order. Empty if unconfigured or the call fails.</summary>
    public async Task<IReadOnlyList<DiscordChannel>> GetTextChannelsAsync(bool forceRefresh = false, CancellationToken ct = default)
    {
        if (forceRefresh)
            cache.Remove(ChannelsCacheKey);

        return await cache.GetOrCreateAsync(ChannelsCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await LoadChannelsAsync(ct);
        }) ?? [];
    }

    /// <summary>Clears the cached roles and channels so the next fetch hits Discord again.</summary>
    public void Invalidate()
    {
        cache.Remove(RolesCacheKey);
        cache.Remove(ChannelsCacheKey);
    }

    private async Task<List<DiscordRole>> LoadRolesAsync(CancellationToken ct)
    {
        var json = await GetGuildResourceAsync("roles", ct);
        if (json is null)
            return [];

        var roles = new List<DiscordRole>();
        foreach (var el in json.RootElement.EnumerateArray())
        {
            var id = ParseId(el, "id");
            var name = el.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            // Skip @everyone (its id equals the guild id) — it's never a meaningful choice here.
            if (id is null || id.ToString() == config["Discord:GuildId"])
                continue;

            var color = el.TryGetProperty("color", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetInt32() : 0;
            var position = el.TryGetProperty("position", out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : 0;
            roles.Add(new DiscordRole(id.Value, name, color, position));
        }

        // Highest role first, matching how Discord displays them.
        return roles.OrderByDescending(r => r.Position).ThenBy(r => r.Name).ToList();
    }

    private async Task<List<DiscordChannel>> LoadChannelsAsync(CancellationToken ct)
    {
        var json = await GetGuildResourceAsync("channels", ct);
        if (json is null)
            return [];

        // First pass: collect category names so text channels can show "Category / #channel".
        var categories = new Dictionary<ulong, string>();
        foreach (var el in json.RootElement.EnumerateArray())
        {
            if (el.TryGetProperty("type", out var t) && t.GetInt32() == CategoryChannelType)
            {
                var catId = ParseId(el, "id");
                if (catId is not null)
                    categories[catId.Value] = el.TryGetProperty("name", out var cn) ? cn.GetString() ?? "" : "";
            }
        }

        var channels = new List<DiscordChannel>();
        foreach (var el in json.RootElement.EnumerateArray())
        {
            if (!el.TryGetProperty("type", out var t) || !TextChannelTypes.Contains(t.GetInt32()))
                continue;

            var id = ParseId(el, "id");
            if (id is null)
                continue;

            var name = el.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            var position = el.TryGetProperty("position", out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : 0;
            string? category = null;
            if (el.TryGetProperty("parent_id", out var parent) && parent.ValueKind == JsonValueKind.String
                && ulong.TryParse(parent.GetString(), out var parentId))
                categories.TryGetValue(parentId, out category);

            channels.Add(new DiscordChannel(id.Value, name, category, position));
        }

        // Group by category (uncategorised last), then by the channel's own position — roughly the
        // order Discord shows them in the sidebar.
        return channels
            .OrderBy(c => c.Category ?? "￿")
            .ThenBy(c => c.Position)
            .ToList();
    }

    /// <summary>GETs /guilds/{guildId}/{resource} with the bot token; null on any misconfig/error.</summary>
    private async Task<JsonDocument?> GetGuildResourceAsync(string resource, CancellationToken ct)
    {
        var guildId = config["Discord:GuildId"];
        var botToken = config["Discord:BotToken"];
        if (string.IsNullOrWhiteSpace(guildId) || string.IsNullOrWhiteSpace(botToken))
        {
            logger.LogWarning("Discord GuildId/BotToken not configured; role/channel suggestions unavailable.");
            return null;
        }

        try
        {
            using var client = httpFactory.CreateClient();
            using var request = new HttpRequestMessage(
                HttpMethod.Get, $"https://discord.com/api/v10/guilds/{guildId}/{resource}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bot", botToken);
            // Discord (behind Cloudflare) 403s requests without a recognised User-Agent; see Program.cs.
            request.Headers.UserAgent.ParseAdd("DiscordBot (https://github.com/ZenBotCS, 1.0)");

            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Discord {Resource} lookup failed with {Status}.", resource, response.StatusCode);
                return null;
            }

            return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Discord {Resource} lookup threw.", resource);
            return null;
        }
    }

    private static ulong? ParseId(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
           && ulong.TryParse(v.GetString(), out var id) ? id : null;
}

/// <summary>A guild role offered as a setting choice. <see cref="Color"/> is Discord's decimal RGB (0 = no colour).</summary>
public record DiscordRole(ulong Id, string Name, int Color, int Position)
{
    /// <summary>CSS hex colour for a swatch, or null when the role has the default (no) colour.</summary>
    public string? ColorHex => Color == 0 ? null : $"#{Color & 0xFFFFFF:X6}";
}

/// <summary>A text channel offered as a setting choice, with its category (if any) for display.</summary>
public record DiscordChannel(ulong Id, string Name, string? Category, int Position)
{
    public string Display => Category is null ? $"#{Name}" : $"{Category} / #{Name}";
}
