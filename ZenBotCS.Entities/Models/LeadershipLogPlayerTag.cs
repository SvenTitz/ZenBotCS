using Microsoft.EntityFrameworkCore;

namespace ZenBotCS.Entities.Models;

[Index(nameof(Tag), IsUnique = true)]
public class LeadershipLogPlayerTag
{
    public int Id { get; set; }
    public string Tag { get; set; } = string.Empty;

    public ICollection<LeadershipLogMessage> MessagesMentionedIn { get; set; } = [];
}
