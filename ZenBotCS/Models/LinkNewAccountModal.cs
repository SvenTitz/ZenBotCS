using Discord.Interactions;

namespace ZenBotCS.Models
{
    public class LinkNewAccountModal : IModal
    {
        public string Title => "Sign Up With a Player Tag";

        [InputLabel("Player Tag")]
        [ModalTextInput("input_player_tag", placeholder: "#ABC123DEF")]
        public string PlayerTag { get; set; } = string.Empty;
    }
}
