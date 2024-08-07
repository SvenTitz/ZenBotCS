﻿using Newtonsoft.Json;

namespace ZenBotCS.Entities.Models.ClashKingApi.Legends;

public class HeroGear
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("level")]
    public int Level { get; set; }
}
