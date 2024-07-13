using Newtonsoft.Json;

namespace ZenBotCS.Entities.Models.ClashKingApi.PlayerStats;

public class ClanGames
{
    [JsonProperty("points")]
    public int Points { get; set; }

    [JsonProperty("clan")]
    public string ClanTag { get; set; } = string.Empty;
}
