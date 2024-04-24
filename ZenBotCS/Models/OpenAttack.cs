namespace ZenBotCS.Models;

public class OpenAttacks(string playerName, string clanName, int attackCount, DateTime warEndTime)
{
    public string PlayerName { get; set; } = playerName;
    public string ClanName { get; set; } = clanName;
    public int AttackCount { get; set; } = attackCount;
    public DateTime WarEndTime { get; set; } = warEndTime;
}
