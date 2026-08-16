using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ZenBotCS.Entities.Models.Enums;

namespace ZenBotCS.Entities.Models;

/// <summary>
/// Records the last slot a recurring reminder was posted for, so a bot restart can't repost it.
/// The slot is whatever identifies the occurrence for that reminder kind — the war's start time
/// for CWL roster reminders, the scheduled spin time for war spin reminders.
/// </summary>
public class ReminderState
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public required string ClanTag { get; set; }

    [Required]
    public required ReminderKind Kind { get; set; }

    [Required]
    public required DateTime LastSlotUtc { get; set; }
}
