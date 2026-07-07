using Newtonsoft.Json;

namespace ZenBotCS.Entities.Models.ClashKingApi;

public class WarData
{
    public string State { get; set; } = string.Empty;
    public int TeamSize { get; set; }
    public int AttacksPerMember { get; set; } = 1;
    public string PreparationStartTime { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public WarClan Clan { get; set; } = new();
    public WarClan Opponent { get; set; } = new();
    public int StatusCode { get; set; }
    public double Timestamp { get; set; }
    public int ResponseRetry { get; set; }

    /// <summary>
    /// The CWL round war tag. Present only for Clan War League wars — regular wars leave this null,
    /// so it's the discriminator used to pick out CWL wars from a <c>/war/{tag}/previous</c> history
    /// (where the field is named <c>tag</c>; the live-CWL endpoint calls the same thing <c>war_tag</c>).
    /// </summary>
    [JsonProperty("tag")]
    public string? WarTag { get; set; }

    /// <summary>The season this war belongs to (<c>yyyy-MM</c>), as reported by <c>/war/{tag}/previous</c>.</summary>
    [JsonProperty("season")]
    public string? Season { get; set; }
}
