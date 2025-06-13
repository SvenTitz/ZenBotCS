using System.ComponentModel.DataAnnotations.Schema;

namespace ZenBotCS.Entities.Models;

public class LeadershipLogMessage
{
    public ulong Id { get; set; } // Message ID
    public ulong ChannelId { get; set; }
    public ulong GuildId { get; set; }
    public string FullContent { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }

    public ICollection<LeadershipLogUser> MentionedUsers { get; set; } = [];
    public ICollection<LeadershipLogPlayerTag> MentionedPlayerTags { get; set; } = [];

    [NotMapped]
    public string MessageLink => $"https://discord.com/channels/{GuildId}/{ChannelId}/{Id}";
}
