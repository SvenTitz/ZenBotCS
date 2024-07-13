using Newtonsoft.Json;

namespace ZenBotCS.Entities.Models.ClashKingApi.PlayerStats;

public class Player
{
    [JsonProperty("name")]
    public required string Name { get; set; }

    [JsonProperty("tag")]
    public required string Tag { get; set; }

    [JsonProperty("townhall")]
    public int Townhall { get; set; }

    [JsonProperty("last_online")]
    public long LastOnline { get; set; }

    [JsonProperty("looted")]
    public Loot Looted { get; set; } = new();

    [JsonProperty("trophies")]
    public int Trophies { get; set; }

    [JsonProperty("warStars")]
    public int WarStars { get; set; }

    [JsonProperty("clanCapitalContributions")]
    public int ClanCapitalContributions { get; set; }

    [JsonProperty("donations")]
    public Dictionary<string, Donations>? Donations { get; set; } = [];

    [JsonProperty("capital")]
    public Dictionary<string, CapitalRaids>? CapitalRaids { get; set; } = [];
    [JsonProperty("clan_games")]
    public Dictionary<string, ClanGames>? ClanGames { get; set; } = [];

    [JsonProperty("season_pass")]
    public Dictionary<string, int>? SeasonPass { get; set; } = [];

    [JsonProperty("attack_wins")]
    public Dictionary<string, int>? AttackWins { get; set; } = [];// ----------------

    [JsonProperty("activity")]
    public Dictionary<string, int>? Activity { get; set; } = [];// ----------------

    [JsonProperty("legends")]
    public LegendsData LegendsData { get; set; } = new();

    [JsonProperty("clan_tag")]
    public string ClanTag { get; set; } = string.Empty;

    [JsonProperty("league")]
    public string League { get; set; } = string.Empty;
}
