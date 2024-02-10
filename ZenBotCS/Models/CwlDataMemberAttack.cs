using CocApi.Rest.Models;

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
