using Newtonsoft.Json;

namespace ZenBotCS.Entities.Models.ClashKingApi.PlayerStats;

public class NewAttack
{
    [JsonProperty("change")]
    public int Change { get; set; }

    [JsonProperty("time")]
    public long Time { get; set; }

    [JsonProperty("trophies")]
    public int Trophies { get; set; }

    [JsonProperty("hero_gear")]
    [JsonConverter(typeof(HeroGearConverter))]
    public List<HeroGear> HeroGear { get; set; } = [];
}
