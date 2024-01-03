using CocApi.Cache;
using CocApi.Rest.Apis;
using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZenBotCS.Services
{
    public class CwlService(ClansClient clansClient)
    {
        private readonly ClansClient _clansClient = clansClient;

        public async Task<Embed> Data(string clantag, string? spreadsheetId)
        {
            var group = await _clansClient.GetOrFetchLeagueGroupOrDefaultAsync(clantag);
            var allWars = await _clansClient.GetOrFetchLeagueWarsAsync(group!);
            var wars = allWars.Where(w => w.Clans.ContainsKey(clantag));



            return new EmbedBuilder()
                        .WithTitle("Suuuper")
                        .WithColor(Color.DarkTeal)
                        .WithDescription(clantag)
                        .Build();
        }

    }
}
