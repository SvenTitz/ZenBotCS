using Newtonsoft.Json;

namespace ZenBotCS.Entities.Models.ClashKingApi.PlayerStats;

public class CapitalRaids
{
    [JsonProperty("donate")]
    public List<int> Donate { get; set; } = [];

    [JsonProperty("raided_clan")]
    public string? RaidedClanTag { get; set; }

    [JsonProperty("raid")]
    public List<int> Raid { get; set; } = [];

    [JsonProperty("limit_hits")]
    public int? LimitHits { get; set; }

    [JsonProperty("attack_count")]
    public int? AttackCount { get; set; }
}
