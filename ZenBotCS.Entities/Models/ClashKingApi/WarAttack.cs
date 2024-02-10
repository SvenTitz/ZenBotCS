using Newtonsoft.Json;

namespace ZenBotCS.Entities.Models.ClashKingApi
{
    public class WarAttack
    {
        [JsonProperty("tag")]
        public string Tag { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("townhall")]
        public int Townhall { get; set; }

        [JsonProperty("_time")]
        public int Time { get; set; }

        [JsonProperty("destruction")]
        public int Destruction { get; set; }

        [JsonProperty("stars")]
        public int Stars { get; set; }

        [JsonProperty("fresh")]
        public bool Fresh { get; set; }

        [JsonProperty("war_start")]
        public int WarStart { get; set; }

        [JsonProperty("defender_tag")]
        public string DefenderTag { get; set; } = string.Empty;

        [JsonProperty("defender_name")]
        public string DefenderName { get; set; } = string.Empty;

        [JsonProperty("defender_townhall")]
        public int DefenderTownhall { get; set; }

        [JsonProperty("war_type")]
        public string WarType { get; set; } = string.Empty;

        [JsonProperty("war_status")]
        public string WarStatus { get; set; } = string.Empty;

        [JsonProperty("attack_order")]
        public int AttackOrder { get; set; }

        [JsonProperty("map_position")]
        public int MapPosition { get; set; }

        [JsonProperty("war_size")]
        public int WarSize { get; set; }

        [JsonProperty("clan")]
        public string Clan { get; set; } = string.Empty;

        [JsonProperty("clan_name")]
        public string ClanName { get; set; } = string.Empty;

        [JsonProperty("defending_clan")]
        public string DefendingClan { get; set; } = string.Empty;

        [JsonProperty("defending_clan_name")]
        public string DefendingClanName { get; set; } = string.Empty;

        [JsonProperty("full_war")]
        public string FullWar { get; set; } = string.Empty;
    }
}
