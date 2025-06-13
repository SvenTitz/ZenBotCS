namespace ZenBotCS.Entities.Models;

public class LeadershipLogUser
{
    public ulong Id { get; set; }

    public ICollection<LeadershipLogMessage> MessagesMentionedIn { get; set; } = [];
}
