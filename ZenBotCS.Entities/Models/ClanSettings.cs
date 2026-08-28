using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ZenBotCS.Entities.Models.Enums;

namespace ZenBotCS.Entities.Models;

public class ClanSettings
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public required string ClanTag { get; set; }

    public int Order { get; set; }

    public ClanType ClanType { get; set; }

    public ulong? MemberRoleId { get; set; }

    public ulong? ElderRoleId { get; set; }

    public ulong? LeaderRoleId { get; set; }

    public ulong? CwlRoleId { get; set; }

    [MaxLength(9)]
    public string? ColorHex { get; set; }

    public bool EnableCwlSignup { get; set; }

    public bool ChampStyleCwlRoster { get; set; }

    /// <summary>
    /// War size the clan's main roster is built for (15 or 30). Subrosters carry their own
    /// <see cref="SubRoster.TargetSize"/>; this is the equivalent for the roster with no row.
    /// </summary>
    public int CwlRosterTargetSize { get; set; } = 15;

    public bool CcGoldDump { get; set; }

    /// <summary>Where leadership-facing reminders are posted (CWL roster, war spin).</summary>
    public ulong? LeadershipChannelId { get; set; }

    public bool CwlRosterReminderEnabled { get; set; }

    public ulong? CwlRosterReminderPingRoleId { get; set; }

    public int CwlRosterReminderLeadHours { get; set; } = 4;

    public bool WarSpinReminderEnabled { get; set; }
}
