using Newtonsoft.Json;

namespace ZenBotCS.Entities.Models.ClashKingApi.PlayerWarHits;

public class Defender
{
    [JsonProperty("tag")]
    public string Tag { get; set; } = default!;

    [JsonProperty("name")]
    public string Name { get; set; } = default!;

    [JsonProperty("townhallLevel")]
    public int TownhallLevel { get; set; }

    [JsonProperty("mapPosition")]
    public int MapPosition { get; set; }

    [JsonProperty("opponentAttacks")]
    public int OpponentAttacks { get; set; }
}
