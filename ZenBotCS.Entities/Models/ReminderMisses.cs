using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZenBotCS.Entities.Models;

public class ReminderMisses
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public required string ClanTag { get; set; }

    [Required]
    public required ulong ChannelId { get; set; }

    public ulong? PingRoleId { get; set; }
}
