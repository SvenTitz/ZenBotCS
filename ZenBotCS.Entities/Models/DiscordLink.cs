using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZenBotCS.Entities.Models
{
    public class DiscordLink
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public required ulong DiscordId { get; set; }

        [Required]
        [MaxLength(12)]
        public required string PlayerTag { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
