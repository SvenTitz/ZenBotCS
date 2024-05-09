using CocApi.Cache;
using CocApi.Rest.Apis;
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
