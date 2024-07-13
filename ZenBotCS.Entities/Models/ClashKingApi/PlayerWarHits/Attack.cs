using Newtonsoft.Json;

namespace ZenBotCS.Entities.Models.ClashKingApi.PlayerWarHits;

public class Attack
{
    [JsonProperty("attackerTag")]
    public string AttackerTag { get; set; } = default!;

    [JsonProperty("defenderTag")]
    public string DefenderTag { get; set; } = default!;

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

    [JsonProperty("defender")]
    public Defender Defender { get; set; } = default!;

    [JsonProperty("attack_order")]
    public int AttackOrder { get; set; }
}
