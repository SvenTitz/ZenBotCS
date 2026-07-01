using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ZenBotCS.Entities.Models.Enums;

namespace ZenBotCS.Entities.Models;

public class CwlSignup
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [MaxLength(12)]
    public string PlayerTag { get; set; } = string.Empty;

    public string PlayerName { get; set; } = string.Empty;

    public int PlayerThLevel { get; set; }

    [MaxLength(12)]
    public string ClanTag { get; set; } = string.Empty;

    public required ulong DiscordId { get; set; }

    public OptOutDays OptOutDays { get; set; }

    /// <summary>
    /// The leader-edited CWL lineup (which days this player actually plays), set via the roster site.
    /// Null until first edited — until then <see cref="EffectiveRosterDays"/> falls back to availability.
    /// </summary>
    public RosterDays? RosterDays { get; set; }

    public WarPreference WarPreference { get; set; }

    public bool Bonus { get; set; }

    public bool WarGeneral { get; set; }

    public bool MaxDefeneses { get; set; }

    public bool Archieved { get; set; }

    /// <summary>
    /// Leadership-hidden on the roster site: excluded from the web grid, day totals, and the generated
    /// image, and treated as opted-out of every day by the bot's per-day features (pre-war reminder,
    /// missing-day check). Role assignment still includes hidden players. The signup row is kept.
    /// </summary>
    public bool Hidden { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The lineup to display/use: the leader-edited <see cref="RosterDays"/> if set, otherwise the
    /// player's availability (all days except <see cref="OptOutDays"/>). Not stored.
    /// </summary>
    [NotMapped]
    public RosterDays EffectiveRosterDays =>
        RosterDays ?? (Enums.RosterDays)(~(int)OptOutDays & AllDaysMask);

    private const int AllDaysMask = 0b111_1111; // Day1..Day7
}
