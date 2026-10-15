using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZenBotCS.Entities.Models;

public class Strike
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>Short public ID used by /strike remove (e.g. "K7M2QX"). Globally unique, never reused.</summary>
    [MaxLength(8)]
    public string StrikeId { get; set; } = string.Empty;

    /// <summary>Owner player tag, canonical form: leading "#", uppercase.</summary>
    [MaxLength(12)]
    public string PlayerTag { get; set; } = string.Empty;

    /// <summary>Player name snapshot taken when the strike was added (shown in lists).</summary>
    [MaxLength(100)]
    public string PlayerName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    /// <summary>Severity; default 1.</summary>
    public int Weight { get; set; } = 1;

    /// <summary>Days the strike stays active; null = never rolls over.</summary>
    public int? RolloverDays { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ulong CreatedByDiscordId { get; set; }

    /// <summary>Set when leadership removes the strike (soft delete). Null = not removed.</summary>
    public DateTime? RemovedAt { get; set; }

    public ulong? RemovedByDiscordId { get; set; }

    /// <summary>Moment the strike deactivates for the tally; null if it never does. Not stored.</summary>
    [NotMapped]
    public DateTime? RollsOverAt => RolloverDays is null ? null : CreatedAt.AddDays(RolloverDays.Value);

    [NotMapped]
    public bool IsRemoved => RemovedAt is not null;

    /// <summary>Active = not removed and (no rollover, or now is strictly before the rollover moment).</summary>
    public bool IsActiveAt(DateTime utcNow) =>
        RemovedAt is null
        && (RolloverDays is null || utcNow < CreatedAt.AddDays(RolloverDays.Value));
}
