using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Text;
using ZenBotCS.Entities.Models.ClashKingApi;

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

        public async Task<List<WarData>> GetClanWarHistory(string clanTag, int limit = 50)
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
                List<WarData>? warDataList = null;
                if (parsedResponse != null && parsedResponse is JArray freshResponse)
                {
                    warDataList = freshResponse.ToObject<List<WarData>>();
                }
                else if (parsedResponse != null && parsedResponse is JArray cachedResponse)
                {
                    string cachedBody = (string)cachedResponse["body"]!;
                    warDataList = JsonConvert.DeserializeObject<List<WarData>>(cachedBody);
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

        public async Task<IEnumerable<WarAttack>> GetPlayerWarAttacksAsync(string playerTag)
        {
            UriBuilder uriBuilder = new UriBuilder();
            try
            {
                uriBuilder.Host = _httpClient.BaseAddress!.Host;
                uriBuilder.Scheme = _httpClient.BaseAddress.Scheme;
                uriBuilder.Port = _httpClient.BaseAddress.Port;
                uriBuilder.Path = $"/player/{Uri.EscapeDataString(playerTag)}/warhits";

                var response = await _httpClient.GetAsync(uriBuilder.Uri);
                response.EnsureSuccessStatusCode();
                var resultJson = await response.Content.ReadAsStringAsync();
                var parsedResponse = JObject.Parse(resultJson);
                WarApiResponse? warApiResponse = null;
                if (parsedResponse != null && parsedResponse.ContainsKey("attacks"))
                {
                    warApiResponse = parsedResponse.ToObject<WarApiResponse>();
                }
                else if (parsedResponse != null && parsedResponse.ContainsKey("body"))
                {
                    string cachedBody = (string)parsedResponse["body"]!;
                    warApiResponse = JsonConvert.DeserializeObject<WarApiResponse>(cachedBody);
                }
                return warApiResponse?.Attacks ?? [];
            }
            catch (Exception ex)
            {
                // TODO
                _logger.LogError(ex, "Error in GetPlayerWarAttacksAsync");
            }
            return [];
        }

        public class WarApiResponse
        {
            public List<WarAttack> Attacks { get; set; } = null!;
        }

    }


}
