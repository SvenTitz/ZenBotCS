using Newtonsoft.Json;

namespace ZenBotCS.Entities.Models.ClashKingApi.PlayerWarHits;

/// <summary>
/// A base in a war, as <c>/v2/player/{tag}/war/stats</c> reports it. The same shape stands in for the
/// queried player, the defender of one of their attacks, and the attacker of one of their defenses --
/// the endpoint calls all three <c>player</c>.
/// </summary>
public class WarParticipant
{
    [JsonProperty("tag")]
    public string Tag { get; set; } = default!;

    [JsonProperty("name")]
    public string Name { get; set; } = default!;

    [JsonProperty("townhallLevel")]
    public int TownhallLevel { get; set; }

    [JsonProperty("mapPosition")]
    public int MapPosition { get; set; }
}
