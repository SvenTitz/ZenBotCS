using CocApi.Rest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZenBotCS.Models
{
    public class CwlDataMemberModel(ClanWarMember member)
    {
        public ClanWarMember Member { get; set; } = member;

        public CwlDataMemberAttack?[] Attacks = new CwlDataMemberAttack?[7];
    }
}
