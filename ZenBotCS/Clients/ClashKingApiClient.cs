using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RestSharp;
using ZenBotCS.Entities.Models.ClashKingApi;
using ZenBotCS.Entities.Models.ClashKingApi.Links;
using ZenBotCS.Entities.Models.ClashKingApi.PlayerWarHits;

namespace ZenBotCS.Clients;

public class ClashKingApiClient
{
    private readonly RestClient _restApiClient;
    private readonly ILogger<ClashKingApiClient> _logger;
    private readonly string? _developerToken;
    private const string BaseUrl = "https://api.clashk.ing/";

    /// <summary>The v2 war endpoints cap <c>limit</c> here.</summary>
    private const int MaxWarLimit = 500;

    /// <summary><c>/v2/links/shared</c> rejects the whole request past 100 identifiers, so batches are chunked.</summary>
    private const int LinkBatchSize = 100;

    // CoC tags are base-14. A tag outside this alphabet makes /v2/links/shared reject the entire
    // batch with a 400, so malformed tags are filtered out and answered locally instead.
    private const string TagAlphabet = "0289PYLQGRJCUV";

    public ClashKingApiClient(IConfiguration configuration, ILogger<ClashKingApiClient> logger)
    {
        _restApiClient = new RestClient(BaseUrl);
        _logger = logger;
        _developerToken = configuration["CkApiToken"];

        if (string.IsNullOrWhiteSpace(_developerToken))
            _logger.LogWarning("No CkApiToken configured -- ClashKing endpoints that need a developer token (Discord links) will return 401.");
    }

    private RestRequest CreateRequest(string path, Method method, object? body = null)
    {
        var request = new RestRequest(path, method)
            .AddHeader("Accept", "application/json")
            .AddHeader("User-Agent", "ZenBot");

        // The public v2 endpoints work without it; /v2/links/shared 401s without it.
        if (!string.IsNullOrWhiteSpace(_developerToken))
            request.AddHeader("Authorization", $"Bearer {_developerToken}");

        if (body != null)
        {
            var jsonPayload = JsonConvert.SerializeObject(body);
            request.AddJsonBody(jsonPayload);
        }

        return request;
    }

    private async Task<T?> ExecuteRequestAsync<T>(RestRequest request)
    {
        try
        {
            var response = await _restApiClient.ExecuteAsync(request);

            if (response.IsSuccessful && response.Content is not null)
            {
                return JsonConvert.DeserializeObject<T>(response.Content);
            }

            // v2 answers errors with {"code","message","request_id"} -- worth logging, because a 400
            // here means the request we built was wrong, not that the API is down.
            _logger.LogError("Request to {resource} failed: {StatusCode} {Content}",
                request.Resource, response.StatusCode, Truncate(response.Content));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing request to {resource}", request.Resource);
        }
        return default;
    }

    private static string Truncate(string? content)
        => string.IsNullOrEmpty(content) ? "" : content.Length <= 300 ? content : content[..300];

    /// <summary>
    /// Player tag -> Discord user id for a batch of tags, via <c>/v2/links/shared</c>. Returns null
    /// when nothing could be asked at all: "the endpoint is down" is not the same answer as "nobody
    /// is linked", and callers fall back to the bot's own link table on the former (see
    /// DiscordLinkSource). A tag present with a null value really is unlinked; a tag missing from the
    /// result belongs to a batch that failed, and counts as unanswered too.
    /// Keys are the tags exactly as they were passed in, so callers can look results up with their
    /// own strings.
    /// </summary>
    public async Task<Dictionary<string, ulong?>?> PostDiscordLinksAsync(List<string> playerTags)
    {
        var answers = new Dictionary<string, ulong?>(StringComparer.OrdinalIgnoreCase);
        // canonical tag -> the caller's original string
        var requested = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tag in playerTags)
        {
            var canonical = NormalizeTag(tag);
            if (canonical is null)
                answers[tag] = null; // cannot be a CoC tag, so it cannot be linked
            else
                requested.TryAdd(canonical, tag);
        }

        if (requested.Count == 0)
            return answers;

        var succeeded = 0;
        var failed = 0;

        foreach (var chunk in requested.Keys.Chunk(LinkBatchSize))
        {
            var request = CreateRequest("/v2/links/shared", Method.Post, new { player_tags = chunk });
            var response = await ExecuteRequestAsync<SharedLinksResponse>(request);

            if (response is null)
            {
                // Leave this chunk's tags out of the result entirely -- absent means "unanswered",
                // which sends the caller to its fallback instead of logging everyone as unlinked.
                failed++;
                continue;
            }

            succeeded++;
            foreach (var canonical in chunk)
                answers[requested[canonical]] = null; // answered: unlinked unless the response says otherwise

            // Unlinked tags are omitted from the response rather than returned with a null value.
            foreach (var link in response.Items)
            {
                if (requested.TryGetValue(link.PlayerTag ?? "", out var original)
                    && ulong.TryParse(link.UserId, out var discordId))
                {
                    answers[original] = discordId;
                }
            }
        }

        if (succeeded == 0)
            return null;

        if (failed > 0)
            _logger.LogWarning("{failed} of {total} discord link batches failed; those tags are reported as unanswered",
                failed, succeeded + failed);

        return answers;
    }

    /// <summary>
    /// The player tags linked to a Discord user, via <c>/v2/links/shared</c>. Returns null when the
    /// request failed; an empty list means the API answered and knows no accounts for that user.
    /// </summary>
    public async Task<List<string>?> PostDiscordLinksAsync(ulong userId)
    {
        var request = CreateRequest("/v2/links/shared", Method.Post, new { discord_ids = new[] { userId.ToString() } });
        var result = await ExecuteRequestAsync<SharedLinksResponse>(request);
        return result?.Items
            .Where(i => !string.IsNullOrEmpty(i.PlayerTag))
            .Select(i => i.PlayerTag!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// A clan's stored, ended wars from <c>/v2/clan/{tag}/wars</c>, newest first. CWL wars carry a
    /// <see cref="WarData.WarTag"/> and no <c>attacksPerMember</c>; regular wars are the other way
    /// round. <paramref name="limit"/> is clamped to the endpoint's maximum.
    /// </summary>
    public async Task<List<WarData>?> GetClanWarHistory(
        string clanTag, int limit = 50, DateTimeOffset? endedAfter = null, DateTimeOffset? endedBefore = null)
    {
        var path = $"/v2/clan/{Uri.EscapeDataString(clanTag)}/wars?limit={Math.Clamp(limit, 1, MaxWarLimit)}"
            + TimeWindowQuery(endedAfter, endedBefore);

        var result = await ExecuteRequestAsync<WarDataResponse>(CreateRequest(path, Method.Get));
        return result?.Items;
    }

    /// <summary>
    /// Every war the player appeared in, with their own attacks and defenses, from
    /// <c>/v2/player/{tag}/war/stats</c>. An empty attack list is a missed attack, which is what the
    /// miss and hitrate stats count. <paramref name="limitDays"/> of 0 means "no time limit".
    /// </summary>
    public async Task<PlayerWarhits?> GetPlayerWarAttacksAsync(string playerTag, uint limitDays)
    {
        var endedAfter = limitDays > 0 ? DateTimeOffset.UtcNow.AddDays(-limitDays) : (DateTimeOffset?)null;

        var path = $"/v2/player/{Uri.EscapeDataString(playerTag)}/war/stats?limit={MaxWarLimit}"
            + TimeWindowQuery(endedAfter, null);

        return await ExecuteRequestAsync<PlayerWarhits>(CreateRequest(path, Method.Get));
    }

    // v2 filters wars by ISO-8601 end time through bracketed query keys, which have to be
    // percent-encoded to survive as a literal query string.
    private static string TimeWindowQuery(DateTimeOffset? after, DateTimeOffset? before)
    {
        var query = "";
        if (after is not null)
            query += $"&time%5Bafter%5D={Uri.EscapeDataString(after.Value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ"))}";
        if (before is not null)
            query += $"&time%5Bbefore%5D={Uri.EscapeDataString(before.Value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ"))}";
        return query;
    }

    /// <summary>
    /// A tag in the form the API accepts, or null if it cannot be a CoC tag. Mirrors what
    /// <c>/v2/links/shared</c> validates: the base-14 alphabet (with the usual O -> 0 fix-up) and at
    /// least three characters.
    /// </summary>
    internal static string? NormalizeTag(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var body = raw.Trim().TrimStart('#').ToUpperInvariant().Replace('O', '0');
        if (body.Length is < 3 or > 15)
            return null;

        foreach (var c in body)
        {
            if (!TagAlphabet.Contains(c))
                return null;
        }

        return "#" + body;
    }
}
