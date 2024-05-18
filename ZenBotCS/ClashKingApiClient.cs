using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Text;
using ZenBotCS.Entities.Models.ClashKingApi;
using ZenBotCS.Entities.Models.ClashKingApi.PlayerStats;

namespace ZenBotCS
{


    public class ClashKingApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ClashKingApiClient> _logger;

        private const string HostUrl = "api.clashking.xyz";

        public ClashKingApiClient(ILogger<ClashKingApiClient> logger)
        {
            _logger = logger;
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri("https://api.clashking.xyz/");
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ZenBot", null));
        }

        public async Task<ulong?> PostDiscordLinksAsync(string playerTag)
        {
            var dict = await PostDiscordLinksAsync(new List<string> { playerTag });
            dict.TryGetValue(playerTag, out ulong? userId);
            return userId;
        }

        public async Task<Dictionary<string, ulong?>> PostDiscordLinksAsync(List<string> playerTags)
        {
            var uriBuilder = new UriBuilder
            {
                Host = _httpClient.BaseAddress!.Host,
                Scheme = _httpClient.BaseAddress.Scheme,
                Port = _httpClient.BaseAddress.Port,
                Path = $"/discord_links",
            };

            try
            {
                var jsonPayload = JsonConvert.SerializeObject(playerTags);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(uriBuilder.Uri, content);

                if (response.IsSuccessStatusCode)
                {
                    var resultJson = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<Dictionary<string, ulong?>>(resultJson);
                    return result ?? [];
                }

                return [];
            }
            catch (Exception ex)
            {
                // TODO
                _logger.LogError(ex, "Error in PostDiscordLinksAsync");
            }
            return [];
        }

        public async Task<List<Entities.Models.ClashKingApi.WarData>> GetClanWarHistory(string clanTag, int limit = 50)
        {
            UriBuilder uriBuilder = new();
            try
            {
                uriBuilder.Host = _httpClient.BaseAddress!.Host;
                uriBuilder.Scheme = _httpClient.BaseAddress.Scheme;
                uriBuilder.Port = _httpClient.BaseAddress.Port;
                uriBuilder.Path = $"/war/{Uri.EscapeDataString(clanTag)}/previous";
                uriBuilder.Query = $"?limit={limit}";

                var response = await _httpClient.GetAsync(uriBuilder.Uri);
                response.EnsureSuccessStatusCode();
                var resultJson = await response.Content.ReadAsStringAsync();
                var parsedResponse = JToken.Parse(resultJson);
                List<Entities.Models.ClashKingApi.WarData>? warDataList = null;
                if (parsedResponse != null && parsedResponse is JArray freshResponse)
                {
                    warDataList = freshResponse.ToObject<List<Entities.Models.ClashKingApi.WarData>>();
                }
                else if (parsedResponse != null && parsedResponse is JArray cachedResponse)
                {
                    string cachedBody = (string)cachedResponse["body"]!;
                    warDataList = JsonConvert.DeserializeObject<List<Entities.Models.ClashKingApi.WarData>>(cachedBody);
                }
                return warDataList ?? [];
            }
            catch (Exception ex)
            {
                // TODO
                _logger.LogError(ex, "Error in GetClanWarHistory");
            }
            return [];
        }

        public async Task<PlayerWarhits> GetPlayerWarAttacksAsync(string playerTag, uint limitDays)
        {
            UriBuilder uriBuilder = new UriBuilder();
            try
            {
                var timespampStart = limitDays > 0
                    ? DateTimeOffset.Now.AddDays(-limitDays).ToUnixTimeSeconds()
                    : 0;

                uriBuilder.Host = _httpClient.BaseAddress!.Host;
                uriBuilder.Scheme = _httpClient.BaseAddress.Scheme;
                uriBuilder.Port = _httpClient.BaseAddress.Port;
                uriBuilder.Path = $"/player/{Uri.EscapeDataString(playerTag)}/warhits";
                uriBuilder.Query = $"?timestamp_start={timespampStart}&timestamp_end={int.MaxValue}&limit={int.MaxValue}";

                var response = await _httpClient.GetAsync(uriBuilder.Uri);
                response.EnsureSuccessStatusCode();
                var resultJson = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<PlayerWarhits>(resultJson);
                return result ?? new();
            }
            catch (Exception ex)
            {
                // TODO
                _logger.LogError(ex, "Error in GetPlayerWarAttacksAsync");
            }
            return new();
        }

        public class WarApiResponse
        {
            public List<WarAttack> Attacks { get; set; } = null!;
        }

    }


}
