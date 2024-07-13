using Newtonsoft.Json;

namespace ZenBotCS.Entities.Models.ClashKingApi.PlayerStats;

[JsonConverter(typeof(LegendsDataConverter))]
public class LegendsData
{
    public int? GlobalRank { get; set; }

    public int? LocalRank { get; set; }

    public Dictionary<string, LegendsDay> LegendsDays { get; set; } = [];
}
