using Newtonsoft.Json;

namespace ZenBotCS.Entities.Models.ClashKingApi.PlayerStats;

public class Item
{
    [JsonProperty("war_data")]
    public WarData WarData { get; set; } = default!;

    [JsonProperty("member_data")]
    public MemberData MemberData { get; set; } = default!;

    [JsonProperty("attacks")]
    public List<Attack> Attacks { get; set; } = default!;

    [JsonProperty("defenses")]
    public List<Defense> Defenses { get; set; } = default!;
}
