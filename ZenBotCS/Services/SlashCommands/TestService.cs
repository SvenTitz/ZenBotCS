using CocApi.Cache;
using CocApi.Rest.Apis;
using Discord;
using ZenBotCS.Helper;
using ZenBotCS.Models;

namespace ZenBotCS.Services.SlashCommands;

public class TestService
{
    private readonly IClansApi _clansApi;
    private readonly ClansClient _clansClient;
    private readonly ClashKingApiClient _clansApiClient;
    private readonly EmbedHelper _embedHelper;

    public TestService(IClansApi clansApi, ClansClient clansClient, ClashKingApiClient clansApiClient, EmbedHelper embedHelper)
    {
        _clansApi = clansApi;
        _clansClient = clansClient;
        _clansApiClient = clansApiClient;
        _embedHelper = embedHelper;
    }

    public async Task<Embed> Cwl_Data_Test(string playerTag)
    {
        var attackData = await _clansApiClient.GetPlayerWarAttacksAsync(playerTag);

        List<MatchupStarDistribution> stats = new();
        foreach (var warAttack in attackData.Where(a => a.WarType.ToLower() == "cwl"))
        {
            var stat = GetOrAdd(stats, warAttack.Townhall, warAttack.DefenderTownhall);
            switch (warAttack.Stars)
            {
                case 0:
                    stat.ZeroStar++;
                    break;
                case 1:
                    stat.OneStar++;
                    break;
                case 2:
                    stat.TwoStar++;
                    break;
                case 3:
                    stat.ThreeStar++;
                    break;
            }
        }

        var tableData = new List<string[]>
        {
            new[] { "Matchup", "0*", "1*", "2*", "3*" }
        };

        foreach (var stat in stats.OrderByDescending(s => s.ThFrom).ThenByDescending(s => s.ThTo).ToList())
        {
            tableData.Add(new[] {
                stat.MatchupString,
                $"{stat.ZeroStar}/{stat.SumAttacks}",
                $"{stat.OneStar}/{stat.SumAttacks}",
                $"{stat.TwoStar}/{stat.SumAttacks}",
                $"{stat.ThreeStar}/{stat.SumAttacks}",
            });
        }


        var text = _embedHelper.FormatAsTable(tableData, 9);

        return new EmbedBuilder()
            .WithTitle($"__CWL__ Hitrate by Matchup of *{attackData.First().Name}*")
            .WithColor(Color.Purple)
            .WithDescription(text)
        .Build();
    }

    private MatchupStarDistribution GetOrAdd(List<MatchupStarDistribution> list, int ThFrom, int ThTo)
    {
        var item = list.FirstOrDefault(s => s.ThFrom == ThFrom && s.ThTo == ThTo);
        if (item is null)
        {
            item = new MatchupStarDistribution(ThFrom, ThTo);
            list.Add(item);
        }
        return item;
    }

}
