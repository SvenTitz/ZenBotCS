using Microsoft.EntityFrameworkCore;
using ZenBotCS.Entities.Models;

namespace ZenBotCS.Entities
{
    public class BotDataContext(DbContextOptions<BotDataContext> options) : DbContext(options)
    {
        public DbSet<DiscordLink> DiscordLinks { get; set; }
        public DbSet<WarHistory> WarHistories { get; set; }
        public DbSet<CwlSignup> CwlSignups { get; set; }
        public DbSet<PinnedRoster> PinnedRosters { get; set; }
        public DbSet<ReminderMisses> ReminderMisses { get; set; }
        public DbSet<PlayerStats> PlayerStats { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DiscordLink>()
                .HasIndex(x => x.PlayerTag)
                .IsUnique();

            modelBuilder.Entity<DiscordLink>()
                .HasIndex(x => new { x.PlayerTag, x.DiscordId })
                .IsUnique();

            modelBuilder.Entity<PinnedRoster>()
                .HasIndex(x => x.ClanTag)
                .IsUnique();

            modelBuilder.Entity<ReminderMisses>()
                .HasIndex(x => new { x.ChannelId, x.ClanTag })
                .IsUnique();

            modelBuilder.ApplyConfiguration(new WarHistoryConfiguration());

            modelBuilder.ApplyConfiguration(new PlayerStatsConfiguration());

        }

        public void AddOrUpdateDiscordLink(DiscordLink discordLink)
        {
            var existingModel = DiscordLinks
                .FirstOrDefault(dl => dl.PlayerTag == discordLink.PlayerTag);

            if (existingModel != null)
            {
                if (existingModel.DiscordId != discordLink.DiscordId)
                {
                    DiscordLinks.Remove(existingModel);
                    DiscordLinks.Add(discordLink);
                }
                else
                {
                    existingModel.UpdatedAt = DateTime.UtcNow;
                }
            }
            else
            {
                DiscordLinks.Add(discordLink);
            }
        }
    }

}

