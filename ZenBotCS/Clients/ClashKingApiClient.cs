using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RestSharp;
using ZenBotCS.Entities.Models.ClashKingApi;
using ZenBotCS.Entities.Models.ClashKingApi.PlayerStats;
using ZenBotCS.Entities.Models.ClashKingApi.PlayerWarHits;
using Legends = ZenBotCS.Entities.Models.ClashKingApi.Legends;

namespace ZenBotCS.Clients;

public class ClashKingApiClient
{
    private readonly RestClient _restApiClient;
    private readonly ILogger<ClashKingApiClient> _logger;
    private const string BaseUrl = "https://api.clashk.ing/";

    public ClashKingApiClient(ILogger<ClashKingApiClient> logger)
    {
        _restApiClient = new RestClient(BaseUrl);
        _logger = logger;
    }

    private RestRequest CreateRequest(string path, Method method, object? body = null)
    {
        var request = new RestRequest(path, method)
            .AddHeader("Accept", "application/json")
            .AddHeader("User-Agent", "ZenBot");

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

            _logger.LogError("Request failed: {StatusCode}", response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing request");
        }
        return default;
    }

    public async Task<ulong?> PostDiscordLinksAsync(string playerTag)
    {
        var dict = await PostDiscordLinksAsync([playerTag]);
        dict.TryGetValue(playerTag, out ulong? userId);
        return userId;
    }

    public async Task<Dictionary<string, ulong?>> PostDiscordLinksAsync(List<string> playerTags)
    {
        var request = CreateRequest("/discord_links", Method.Post, playerTags);
        var result = await ExecuteRequestAsync<Dictionary<string, ulong?>>(request);
        return result ?? [];
    }

    public async Task<List<string>> PostDiscordLinksAsync(ulong userId)
    {
        var request = CreateRequest("/discord_links", Method.Post, new List<string> { userId.ToString() });
        var result = await ExecuteRequestAsync<Dictionary<string, ulong?>>(request);
        return result?.Where(kvp => kvp.Value != null).Select(kvp => kvp.Key).ToList() ?? [];
    }

    public async Task<List<Entities.Models.ClashKingApi.WarData>?> GetClanWarHistory(string clanTag, int limit = 50)
    {
        var request = CreateRequest($"/war/{Uri.EscapeDataString(clanTag)}/previous?limit={limit}", Method.Get);
        var result = await ExecuteRequestAsync<WarDataResponse>(request);
        return result?.Items;
    }

    public async Task<PlayerWarhits?> GetPlayerWarAttacksAsync(string playerTag, uint limitDays)
    {
        var timespampStart = limitDays > 0
            ? DateTimeOffset.Now.AddDays(-limitDays).ToUnixTimeSeconds()
            : 0;

        var request = CreateRequest($"/player/{Uri.EscapeDataString(playerTag)}/warhits?timestamp_start={timespampStart}&timestamp_end={int.MaxValue}&limit={int.MaxValue}", Method.Get);
        var result = await ExecuteRequestAsync<PlayerWarhits>(request);
        return result;
    }

    public async Task<Player?> GetPlayerStatsAsync(string playerTag)
    {
        var request = CreateRequest($"/player/{Uri.EscapeDataString(playerTag)}/stats", Method.Get);
        var result = await ExecuteRequestAsync<Player>(request);
        return result;
    }

    public async Task<string?> GetCurrentSeason()
    {
        var request = CreateRequest("/list/seasons?last=0", Method.Get);
        var result = await ExecuteRequestAsync<string[]>(request);
        return result?.FirstOrDefault();
    }

    public string GetCurrentLegendDay()
    {
        DateTime utcNow = DateTime.UtcNow;
        DateTime switchTime = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, 5, 0, 0, DateTimeKind.Utc);

        if (utcNow < switchTime)
        {
            switchTime = switchTime.AddDays(-1);
        }

        return switchTime.ToString("yyyy-MM-dd");
    }

    public async Task<Legends.Player> GetPlayerLegends(string playerTag, string season)
    {
        var request = CreateRequest($"/player/{Uri.EscapeDataString(playerTag)}/legends?season={season}", Method.Get);
        var result = await ExecuteRequestAsync<Legends.Player>(request);
        return result ?? new Legends.Player();
    }
}

