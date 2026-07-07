using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ZenBotCS.Entities.Models.Cwl;

namespace ZenBotCS.Entities.Models;

/// <summary>
/// A cached, computed CWL performance snapshot for one clan in one CWL instance. Keyed by
/// (ClanTag, Season, StartTime): <see cref="StartTime"/> disambiguates the rare case of two CWLs
/// in the same month. Finished CWLs are immutable and served straight from here; the live season
/// is recomputed. See <c>CwlHistoryConfiguration</c> for the JSON column mapping.
/// </summary>
public class CwlHistory
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string ClanTag { get; set; } = string.Empty;

    /// <summary>The CWL season in <c>yyyy-MM</c> form.</summary>
    [Required]
    [MaxLength(7)]
    public string Season { get; set; } = string.Empty;

    /// <summary>The first war's start time — part of the key so two CWLs in a month stay distinct.</summary>
    public DateTime StartTime { get; set; }

    public CwlSeasonPerformance? Performance { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
