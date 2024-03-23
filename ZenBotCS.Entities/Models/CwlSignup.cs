using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ZenBotCS.Entities.Models.Enums;

namespace ZenBotCS.Entities.Models;

public class CwlSignup
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [MaxLength(12)]
    public string PlayerTag { get; set; } = string.Empty;

    public string PlayerName { get; set; } = string.Empty;

    public int PlayerThLevel { get; set; }

    [MaxLength(12)]
    public string ClanTag { get; set; } = string.Empty;

    public required ulong DiscordId { get; set; }

    public OptOutDays OptOutDays { get; set; }

    public WarPreference WarPreference { get; set; }

    public bool Bonus { get; set; }

    public bool WarGeneral { get; set; }

    public bool Archieved { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
