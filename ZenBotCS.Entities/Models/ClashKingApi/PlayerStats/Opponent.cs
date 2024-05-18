using Newtonsoft.Json;

namespace ZenBotCS.Entities.Models.ClashKingApi.PlayerStats;

public class Opponent
{
    [JsonProperty("tag")]
    public string Tag { get; set; } = default!;

    [JsonProperty("name")]
    public string Name { get; set; } = default!;

    [JsonProperty("badgeUrls")]
    public BadgeUrls BadgeUrls { get; set; } = default!;

    [JsonProperty("clanLevel")]
    public int ClanLevel { get; set; }

    [JsonProperty("attacks")]
    public int Attacks { get; set; }

    [JsonProperty("stars")]
    public int Stars { get; set; }

    [JsonProperty("destructionPercentage")]
    public double DestructionPercentage { get; set; }
}
