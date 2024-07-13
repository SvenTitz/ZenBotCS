using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ZenBotCS.Entities.Models.ClashKingApi.PlayerStats;
using ZenBotCS.Entities.Models.ClashKingApi.PlayerWarHits;

namespace ZenBotCS.Entities.Models;

public class PlayerStats
{

    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string PlayerTag { get; set; } = string.Empty;

    public Player? Player { get; set; }

    public PlayerWarhits? PlayerWarhits { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
