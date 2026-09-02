using Newtonsoft.Json;

namespace ZenBotCS.Entities.Models.ClashKingApi.PlayerWarHits;

/// <summary>An attack against the queried player. v2 names the attacking base <c>player</c>.</summary>
public class Defense
{
    [JsonProperty("stars")]
    public int Stars { get; set; }

    [JsonProperty("destructionPercentage")]
    public int DestructionPercentage { get; set; }

    [JsonProperty("order")]
    public int Order { get; set; }

    [JsonProperty("duration")]
    public int Duration { get; set; }

    [JsonProperty("fresh")]
    public bool Fresh { get; set; }

    [JsonProperty("player")]
    public WarParticipant Attacker { get; set; } = default!;
}
