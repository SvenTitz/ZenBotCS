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
}
