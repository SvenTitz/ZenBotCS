using Newtonsoft.Json;

namespace ZenBotCS.Entities.Models.ClashKingApi.Legends;

public class Player
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("tag")]
    public string Tag { get; set; }

    [JsonProperty("townhall")]
    public int Townhall { get; set; }

    [JsonProperty("legends")]
    public Dictionary<string, LegendData> Legends { get; set; }
}
