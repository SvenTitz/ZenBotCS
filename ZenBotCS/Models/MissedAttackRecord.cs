namespace ZenBotCS.Models
{
    public record MissedAttackRecord
    {
        public string ClanTag { get; set; } = string.Empty;

        public string ClanName { get; set; } = string.Empty;

        public string PlayerTag { get; set; } = string.Empty;

        public string PlayerName { get; set; } = string.Empty;

        public DateTime Date { get; set; }

        public int Attacks { get; set; }

        public int Misses { get; set; }
    }
}
