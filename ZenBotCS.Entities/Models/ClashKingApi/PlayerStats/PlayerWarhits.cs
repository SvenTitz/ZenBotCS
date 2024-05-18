using Newtonsoft.Json;

namespace ZenBotCS.Entities.Models.ClashKingApi.PlayerStats;

public class PlayerWarhits
{
    [JsonProperty("items")]
    public List<Item> Items { get; set; } = [];
}
