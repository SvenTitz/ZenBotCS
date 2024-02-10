namespace ZenBotCS.Entities.Models.ClashKingApi;

public class WarClan
{
    public string Tag { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public BadgeUrls BadgeUrls { get; set; } = new();
    public int ClanLevel { get; set; }
    public int Attacks { get; set; }
    public int Stars { get; set; }
    public double DestructionPercentage { get; set; }
    public List<WarMember> Members { get; set; } = [];
}
