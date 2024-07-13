using Newtonsoft.Json;

namespace ZenBotCS.Entities.Models.ClashKingApi.PlayerStats;

public class Donations
{
    [JsonProperty("donated")]
    public int Donated { get; set; }

    [JsonProperty("received")]
    public int Received { get; set; }
}
