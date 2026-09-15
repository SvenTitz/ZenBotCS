using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ZenBotCS.Entities.Models.Cwl;

namespace ZenBotCS.Entities.Models;

/// <summary>
/// A cached, computed CWL performance snapshot for one clan in one CWL instance. Stored one row per
/// CWL slot (see <c>CwlHistoryStore</c>), which is what keeps the rare two-CWLs-in-a-month case apart
/// without letting a partial fetch invent a third. Finished CWLs are served straight from here; the
/// live one is recomputed. See <c>CwlHistoryConfiguration</c> for the JSON column mapping.
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

    /// <summary>The earliest known war start of this CWL — part of the unique key, and what the UI labels.</summary>
    public DateTime StartTime { get; set; }

    public CwlSeasonPerformance? Performance { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
