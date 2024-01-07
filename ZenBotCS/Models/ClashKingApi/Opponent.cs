using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZenBotCS.Models.ClashKingApi;

public class Opponent
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
