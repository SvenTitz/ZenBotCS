using Newtonsoft.Json;

namespace ZenBotCS.Entities.Models.ClashKingApi.PlayerWarHits;

public class PlayerWarhits
{
    [JsonProperty("items")]
    public List<Item> Items { get; set; } = [];
}
