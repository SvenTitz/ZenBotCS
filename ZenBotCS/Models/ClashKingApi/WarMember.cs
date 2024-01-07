using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZenBotCS.Models.ClashKingApi;

public class WarMember
{
    public string Tag { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int TownhallLevel { get; set; }
    public int MapPosition { get; set; }
    public List<WarMemberAttack> Attacks { get; set; } = [];
    public int OpponentAttacks { get; set; }
}
