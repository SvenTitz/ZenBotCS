using Newtonsoft.Json;

namespace ZenBotCS.Entities.Models.ClashKingApi.Legends;

public class NewAttack
{
    [JsonProperty("change")]
    public int Change { get; set; }

    [JsonProperty("time")]
    public long Time { get; set; }

    [JsonProperty("trophies")]
    public int Trophies { get; set; }

    [JsonProperty("hero_gear")]
    public List<HeroGear> HeroGear { get; set; }
}
