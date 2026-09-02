using Microsoft.EntityFrameworkCore;
using ZenBotCS.Entities.Models;
using ZenBotCS.Entities.Models.Enums;

namespace ZenBotCS.Entities
{
    public class BotDataContext(DbContextOptions<BotDataContext> options) : DbContext(options)
    {
        public DbSet<DiscordLink> DiscordLinks { get; set; }
        public DbSet<WarHistory> WarHistories { get; set; }
        public DbSet<CwlSignup> CwlSignups { get; set; }
        public DbSet<PinnedRoster> PinnedRosters { get; set; }
        public DbSet<ReminderMisses> ReminderMisses { get; set; }
        public DbSet<ReminderState> ReminderStates { get; set; }
        public DbSet<PlayerWarHitsCache> PlayerWarHitsCaches { get; set; }
        public DbSet<CwlHistory> CwlHistories { get; set; }
        public DbSet<ClanSettings> ClanSettings { get; set; }
        public DbSet<SubRoster> SubRosters { get; set; }
        public DbSet<LeadershipLogMessage> LeadershipLogMessages { get; set; }
        public DbSet<LeadershipLogUser> LeadershipLogUsers { get; set; }
        public DbSet<LeadershipLogPlayerTag> LeadershipLogPlayerTags { get; set; }

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

            modelBuilder.Entity<ReminderState>()
                .HasIndex(x => new { x.ClanTag, x.Kind })
                .IsUnique();

            // A game clan hosts at most one subroster, so any game clan tag resolves to exactly one
            // roster (see CwlRosterSource.RosterFor). Everything else about subrosters leans on this.
            modelBuilder.Entity<SubRoster>()
                .HasIndex(x => x.GameClanTag)
                .IsUnique();

            modelBuilder.Entity<SubRoster>()
                .HasIndex(x => x.ClanTag);

            // SetNull, not Cascade: deleting a subroster (or resetting the season) must return its
            // players to the clan's main roster, never delete their signups.
            modelBuilder.Entity<CwlSignup>()
                .HasOne<SubRoster>()
                .WithMany()
                .HasForeignKey(s => s.SubRosterId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<PlayerWarHitsCache>()
                .HasIndex(x => x.PlayerTag)
                .IsUnique();

            modelBuilder.ApplyConfiguration(new WarHistoryConfiguration());

            modelBuilder.ApplyConfiguration(new PlayerWarHitsCacheConfiguration());

            modelBuilder.ApplyConfiguration(new CwlHistoryConfiguration());

        }

        /// <summary>True when this reminder kind has already been posted for that slot.</summary>
        public bool WasReminderSent(string clanTag, ReminderKind kind, DateTime slotUtc)
        {
            return ReminderStates
                .AsNoTracking()
                .Any(rs => rs.ClanTag == clanTag && rs.Kind == kind && rs.LastSlotUtc == slotUtc);
        }

        /// <summary>Records a reminder slot as handled, whether it was posted or deliberately skipped.</summary>
        public void MarkReminderSent(string clanTag, ReminderKind kind, DateTime slotUtc)
        {
            var existing = ReminderStates
                .FirstOrDefault(rs => rs.ClanTag == clanTag && rs.Kind == kind);

            if (existing is null)
                ReminderStates.Add(new ReminderState { ClanTag = clanTag, Kind = kind, LastSlotUtc = slotUtc });
            else
                existing.LastSlotUtc = slotUtc;

            SaveChanges();
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

