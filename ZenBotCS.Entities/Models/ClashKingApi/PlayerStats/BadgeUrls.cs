using Newtonsoft.Json;

namespace ZenBotCS.Entities.Models.ClashKingApi.PlayerStats;

public class BadgeUrls
{
    [JsonProperty("small")]
    public string Small { get; set; } = default!;

    [JsonProperty("large")]
    public string Large { get; set; } = default!;

    [JsonProperty("medium")]
    public string Medium { get; set; } = default!;
}
