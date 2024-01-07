using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZenBotCS.Models.ClashKingApi;

public class WarMemberAttack
{
    public string AttackerTag { get; set; } = string.Empty;
    public string DefenderTag { get; set; } = string.Empty;
    public int Stars { get; set; }
    public double DestructionPercentage { get; set; }
    public int Order { get; set; }
    public int Duration { get; set; }
}
