using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZenBotCS.Entities.Models;

public class PinnedRoster
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string ClanTag { get; set; } = string.Empty;

    public string SpreadsheetId { get; set; } = string.Empty;

    public string Gid { get; set; } = string.Empty;
}
