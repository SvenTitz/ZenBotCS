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
}
