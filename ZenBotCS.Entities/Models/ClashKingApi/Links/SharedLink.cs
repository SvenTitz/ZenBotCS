using Newtonsoft.Json;

namespace ZenBotCS.Entities.Models.ClashKingApi.Links;

/// <summary>One row of <c>POST /v2/links/shared</c>: a player tag and the Discord user it belongs to.</summary>
public class SharedLink
{
    /// <summary>Discord snowflake, sent as a string because it does not fit a JSON number safely.</summary>
    [JsonProperty("user_id")]
    public string? UserId { get; set; }

    [JsonProperty("player_tag")]
    public string? PlayerTag { get; set; }

    [JsonProperty("is_verified")]
    public bool IsVerified { get; set; }
}
