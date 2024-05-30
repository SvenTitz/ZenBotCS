using Newtonsoft.Json;

namespace ZenBotCS.Entities.Models.ClashKingApi.Legends;

public class HeroGear
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("level")]
    public int Level { get; set; }
}
