using Newtonsoft.Json;

namespace ZenBotCS.Entities.Models.ClashKingApi.PlayerStats;

public class LegendsDay
{
    [JsonProperty("defenses")]
    public List<int> Defenses { get; set; } = [];

    [JsonProperty("new_defenses")]
    public List<NewDefense> NewDefenses { get; set; } = [];

    [JsonProperty("num_attacks")]
    public int NumAttacks { get; set; }

    [JsonProperty("attacks")]
    public List<int> Attacks { get; set; } = [];

    [JsonProperty("new_attacks")]
    public List<NewAttack> NewAttacks { get; set; } = [];
}
