using Newtonsoft.Json;

namespace ZenBotCS.Entities.Models.ClashKingApi.PlayerStats;

public class Loot
{
    [JsonProperty("gold")]
    public Dictionary<string, long> Gold { get; set; } = [];

    [JsonProperty("elixir")]
    public Dictionary<string, long> Elixir { get; set; } = [];

    [JsonProperty("dark_elixir")]
    public Dictionary<string, long> DarkElixir { get; set; } = [];
}
