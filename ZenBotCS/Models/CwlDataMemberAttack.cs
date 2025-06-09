using CocApi.Rest.Models;

namespace ZenBotCS.Models
{
    public class CwlDataMemberAttack
    {
        public int DestructionPercentage { get; set; }

        public int Stars { get; set; }

        public int AttackerTownHall { get; set; }

        public int DefenderTownHall { get; set; }

        public int DefenderRushScore { get; set; }

        public bool isMissedAttack { get; set; }

        public CwlDataMemberAttack() { }

        public CwlDataMemberAttack(ClanWarAttack attack, WarClan opponentClan, Dictionary<int, int> thMinIndexMap)
        {
            DestructionPercentage = attack.DestructionPercentage;
            Stars = attack.Stars;
            AttackerTownHall = attack.AttackerTownHall;
            DefenderTownHall = attack.DefenderTownHall;
            DefenderRushScore = GetTownhallLevelDifferenceFromMap(attack, opponentClan, thMinIndexMap);
            isMissedAttack = false;
        }

        private int GetTownhallLevelDifferenceFromMap(ClanWarAttack attack, WarClan opponentClan, Dictionary<int, int> thMinIndexMap)
        {
            var currentTH = attack.DefenderTownHall;
            var currentMP = opponentClan.Members.FirstOrDefault(m => m.Tag == attack.DefenderTag)?.MapPosition ?? 0;

            var lowerTH = thMinIndexMap
                .Where(kv => kv.Value < currentMP)
                .Select(kv => kv.Key)
                .DefaultIfEmpty(currentTH)
                .Min();

            return currentTH - lowerTH;
        }

        public string GetSheetFormattedThLevel()
        {
            return (DefenderTownHall - DefenderRushScore).ToString() + (DefenderRushScore > 0 ? "*" : "");
        }
    }
}
