using Newtonsoft.Json;

namespace ZenBotCS.Entities.Models.ClashKingApi.PlayerWarHits;

/// <summary>
/// <c>GET /v2/player/{tag}/war/stats</c> -- every war the player appeared in, newest first, with
/// both sides and every attack involving them.
/// </summary>
public class PlayerWarhits
{
    [JsonProperty("items")]
    public List<Item> Items { get; set; } = [];
}
