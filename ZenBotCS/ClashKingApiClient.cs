using CocApi.Rest.Models;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore.Storage;
using Newtonsoft.Json;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using ZenBotCS.Models.ClashKingApi;

namespace ZenBotCS
{


    public class ClashKingApiClient
    {
        private readonly HttpClient _httpClient;

        private const string HostUrl = "api.clashking.xyz";

        public ClashKingApiClient() 
        {
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri("https://api.clashking.xyz/");
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ZenBot", null));
        }

        public async Task<IEnumerable<string>> PostDiscordLinksAsync(ulong userId)
        {
            UriBuilder uriBuilder = new UriBuilder();
            try
            {
                uriBuilder.Host = _httpClient.BaseAddress!.Host;
                uriBuilder.Scheme = _httpClient.BaseAddress.Scheme;
                uriBuilder.Port = _httpClient.BaseAddress.Port;
                uriBuilder.Path = $"/discord_links";
            }
            catch
            {
                // TODO
                Console.WriteLine();
            }
            return Array.Empty<string>();
        }

        public async Task GetClanWarHistory()
        {
            UriBuilder uriBuilder = new UriBuilder();
            try
            {
                uriBuilder.Scheme = "https";
                uriBuilder.Host = HostUrl;
                uriBuilder.Path = "/war/#2G2LJUYGV/previous";
                var query = HttpUtility.ParseQueryString(uriBuilder.Query);
                query["limit"] = "10";
                uriBuilder.Query = query.ToString();
                string url = uriBuilder.ToString();
                using HttpResponseMessage response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine(responseBody);
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine("\nException Caught!");
                Console.WriteLine("Message :{0} ", e.Message);
            }
        }

        public async Task<List<WarAttack>> GetPlayerWarAttacksAsync(string playerTag)
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
                var warApiResponse = JsonConvert.DeserializeObject<WarApiResponse>(resultJson);
                return warApiResponse?.Attacks ?? new List<WarAttack>();
            }
            catch 
            {
                // TODO
                Console.WriteLine();
                return new List<WarAttack>();
            }
        }

        public class WarApiResponse
        {
            public List<WarAttack> Attacks { get; set; }
        }


        public async Task<string> Test()
        {
            var requests = new string[]
            {
                "https://api.clashking.xyz/player/%23G28CPC2Y/warhits",
                "https://api.clashking.xyz/player/%23Y2VJ0YGC/warhits",
                "https://api.clashking.xyz/player/%23G8GQR0GY9/warhits",
            };
            var attackList = new List<WarAttack>();
            foreach (var request in requests)
            {
                var response = _httpClient.GetAsync(request).Result;
                var res = await response.Content.ReadAsStringAsync();
                attackList.AddRange(JsonConvert.DeserializeObject<List<WarAttack>>(res) ?? new List<WarAttack>());
            }

            List<Stat> statList = new();
            foreach(var warAttack in attackList.Where(a => a.WarType == "random"))
            {
                var stat = GetOrAdd(statList, warAttack.Townhall, warAttack.DefenderTownhall);
                stat.Count++;
                if (warAttack.Stars == 3)
                    stat.Success++;
            }

            statList = statList.OrderByDescending(s => s.ThFrom).ThenByDescending(s => s.ThTo).ToList();
            var sb = new StringBuilder("```\n");
            foreach(Stat stat in statList)
            {
                sb.AppendLine(stat.ToString());
            }
            sb.AppendLine("```");
            return sb.ToString();
        }

        private Stat GetOrAdd(List<Stat> list, int ThFrom, int ThTo)
        {
            var stat = list.FirstOrDefault(s => s.ThFrom == ThFrom && s.ThTo == ThTo);
            if(stat is null)
            {
                stat = new Stat(ThFrom, ThTo);
                list.Add(stat);
            }
            return stat;
        }

        private class Stat
        {
            public Stat(int thFrom, int thTo)
            {
                ThFrom = thFrom;
                ThTo = thTo;
            }

            public int ThFrom { get; set; }
            public int ThTo { get; set; }
            public int Success { get; set; }
            public int Count { get; set; }

            public override string ToString()
            {
                double percentage = (double)Success / Count;
                return $"{ThFrom}v{ThTo}:\t{Success}/{Count}\t{percentage.ToString("0%")}";
            }
        }
    }
    
    
}
