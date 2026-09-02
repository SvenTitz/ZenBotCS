using Newtonsoft.Json;

namespace ZenBotCS.Entities.Models.ClashKingApi.PlayerWarHits;

/// <summary>
/// One war the queried player appeared in (<c>/v2/player/{tag}/war/stats</c>). v2 flattens the war
/// itself into this item -- there is no separate <c>war_data</c> wrapper any more. An empty
/// <see cref="Attacks"/> list is how a missed attack shows up.
/// </summary>
public class Item
{
    [JsonProperty("teamSize")]
    public int TeamSize { get; set; }

    /// <summary>1 in CWL, 2 in a regular war.</summary>
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

    /// <summary><c>cwl</c>, <c>random</c> or <c>friendly</c>.</summary>
    [JsonProperty("type")]
    public string Type { get; set; } = default!;

    /// <summary>The queried player's own lineup entry in this war.</summary>
    [JsonProperty("player")]
    public WarParticipant Player { get; set; } = default!;

    [JsonProperty("attacks")]
    public List<Attack> Attacks { get; set; } = [];

    [JsonProperty("defenses")]
    public List<Defense> Defenses { get; set; } = [];
}
