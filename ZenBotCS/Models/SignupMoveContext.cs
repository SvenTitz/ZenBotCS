using CocApi.Rest.Models;

namespace ZenBotCS.Models
{
    public class SignupMoveContext
    {
        public required Clan ClanFrom { get; set; }
        public required Clan ClanTo { get; set; }

        public required List<Player> Players { get; set; }

        public List<Player> SelectedPlayers { get; set; } = [];

        public int PageCount { get; set; }

    }
}
