using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZenBotCS.Entities.Models;

/// <summary>
/// A slice of one clan's CWL signups that plays its wars in a different clan — e.g. Reddit Zen's
/// "B Roster" hosted in an event clan. The clan's own main roster has no row: a signup with a null
/// <see cref="CwlSignup.SubRosterId"/> belongs to it and plays in <see cref="CwlSignup.ClanTag"/>.
///
/// <see cref="GameClanTag"/> is unique across all subrosters, so any game clan tag resolves to exactly
/// one roster. That's what lets the bot's per-clan features (day check, roles, missing-spin) keep their
/// existing "one clan tag in" signatures — see <c>CwlRosterSource.RosterFor</c>.
/// </summary>
public class SubRoster
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>The clan the signups belong to — where leadership manages them.</summary>
    [Required]
    [MaxLength(50)]
    public required string ClanTag { get; set; }

    /// <summary>
    /// The clan this roster actually plays CWL in. Must be an Event or Partner clan, must not be
    /// <see cref="ClanTag"/> itself, and must not already host another subroster.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public required string GameClanTag { get; set; }

    [Required]
    [MaxLength(50)]
    public required string Name { get; set; }

    /// <summary>Tab order on the roster page.</summary>
    public int Order { get; set; }

    /// <summary>War size this roster is built for (15 or 30).</summary>
    public int TargetSize { get; set; } = 15;
}
