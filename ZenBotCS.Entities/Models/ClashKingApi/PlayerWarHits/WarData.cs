using Newtonsoft.Json;

namespace ZenBotCS.Entities.Models.ClashKingApi.PlayerWarHits;

public class WarData
{
    [JsonProperty("state")]
    public string State { get; set; } = default!;

    [JsonProperty("teamSize")]
    public int TeamSize { get; set; }

    [JsonProperty("attacksPerMember")]
    public int AttacksPerMember { get; set; }

    [JsonProperty("preparationStartTime")]
    public string PreparationStartTime { get; set; } = default!;

    [JsonProperty("startTime")]
    public string StartTime { get; set; } = default!;

    [JsonProperty("endTime")]
    public string EndTime { get; set; } = default!;

    [JsonProperty("clan")]
    public Clan Clan { get; set; } = default!;

    [JsonProperty("opponent")]
    public Opponent Opponent { get; set; } = default!;

    [JsonProperty("type")]
    public string Type { get; set; } = default!;
}
