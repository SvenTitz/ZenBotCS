namespace ZenBotCS.Models;

public class AttackSuccessModel
{
    public string PlayerName { get; set; }
    public string PlayerTag { get; set; }
    public int PlayerTh { get; set; }
    public int AttackCount { get; set; }
    public int SuccessCount { get; set; }

    public AttackSuccessModel(string playerName, string playerTag, int playerTh)
    {
        PlayerName = playerName;
        PlayerTag = playerTag;
        PlayerTh = playerTh;
    }

    public void AddMiss()
    {
        AttackCount++;
    }

    public void AddSuccess()
    {
        AttackCount++;
        SuccessCount++;
    }

    public double GetSuccessRate()
    {
        return (double)SuccessCount / AttackCount;
    }
}


