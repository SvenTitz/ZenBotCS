using Newtonsoft.Json;

namespace ZenBotCS.Entities.Models.ClashKingApi.PlayerStats;

public class NewDefense
{
    [JsonProperty("change")]
    public int Change { get; set; }

    [JsonProperty("time")]
    public long Time { get; set; }

    [JsonProperty("trophies")]
    public int Trophies { get; set; }
}
