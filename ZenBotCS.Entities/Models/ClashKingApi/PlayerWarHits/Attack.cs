using Newtonsoft.Json;

namespace ZenBotCS.Entities.Models.ClashKingApi.PlayerWarHits;

/// <summary>An attack the queried player made. v2 names the defending base <c>player</c>.</summary>
public class Attack
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
    public WarParticipant Defender { get; set; } = default!;
}
