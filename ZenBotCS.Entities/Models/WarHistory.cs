using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ZenBotCS.Entities.Models.ClashKingApi;

namespace ZenBotCS.Entities.Models;

public class WarHistory
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string ClanTag { get; set; } = string.Empty;

    public List<WarData>? WarData { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
