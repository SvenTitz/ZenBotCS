using CocApi.Rest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZenBotCS.Models
{
    public class CwlDataMemberAttack
    {
        public int DestructionPercentage { get; set; }

        public int Stars { get; set; }

        public int AttackerTownHall { get; set; }

        public int DefenderTownHall { get; set; }

        public bool isMissedAttack { get; set; }

        public CwlDataMemberAttack() { }

        public CwlDataMemberAttack(ClanWarAttack attack)
        {
            DestructionPercentage = attack.DestructionPercentage;
            Stars = attack.Stars;
            AttackerTownHall = attack.AttackerTownHall;
            DefenderTownHall = attack.DefenderTownHall;
            isMissedAttack = false;
        }
    }
}
