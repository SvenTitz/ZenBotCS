namespace ZenBotCS.Models;

public class OpenAttacks(string playerName, int playerTh, string clanName, int attackCount, DateTime warEndTime, DateTime warStartTime)
{
    public string PlayerName { get; set; } = playerName;
    public int PlayerTh { get; set; } = playerTh;
    public string ClanName { get; set; } = clanName;
    public int AttackCount { get; set; } = attackCount;
    public DateTime WarEndTime { get; set; } = warEndTime;
    public DateTime WarStartTime { get; set; } = warStartTime;
}
