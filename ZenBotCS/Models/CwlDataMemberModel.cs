using CocApi.Rest.Models;

namespace ZenBotCS.Models
{
    public class CwlDataMemberModel(ClanWarMember member)
    {
        public ClanWarMember Member { get; set; } = member;

        public CwlDataMemberAttack?[] Attacks = new CwlDataMemberAttack?[7];
    }
}
