using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ZenBotCS.Entities.Models.ClashKingApi.PlayerWarHits;

namespace ZenBotCS.Entities.Models;

/// <summary>
/// A player's war hits as ClashKing last reported them, so the hitrate and attack-breakdown commands
/// don't re-fetch every player on every invocation (a 50-member clan is 50 API calls otherwise).
/// Filled by <c>PlayerWarHitsUpdateService</c> and read through <c>ClashKingApiService</c>, which
/// refetches anything older than a day.
/// </summary>
public class PlayerWarHitsCache
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string PlayerTag { get; set; } = string.Empty;

    /// <summary>Stored as a JSON column, like <see cref="WarHistory.WarData"/>.</summary>
    public PlayerWarhits? WarHits { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
