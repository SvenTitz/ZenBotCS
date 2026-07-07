using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newtonsoft.Json;
using ZenBotCS.Entities.Models;
using ZenBotCS.Entities.Models.Cwl;

namespace ZenBotCS.Entities;

public class CwlHistoryConfiguration : IEntityTypeConfiguration<CwlHistory>
{
    private static readonly JsonSerializerSettings Settings = new() { NullValueHandling = NullValueHandling.Ignore };

    public void Configure(EntityTypeBuilder<CwlHistory> builder)
    {
        builder.HasIndex(ch => new { ch.ClanTag, ch.Season, ch.StartTime })
            .IsUnique();

        // Store the whole computed performance object as a JSON column, like WarHistory/PlayerStats.
        builder.Property(ch => ch.Performance)
            .HasColumnType("longtext")
            .HasConversion(
                p => JsonConvert.SerializeObject(p, Settings),
                p => JsonConvert.DeserializeObject<CwlSeasonPerformance>(p, Settings),
                new ValueComparer<CwlSeasonPerformance?>
                (
                    (p1, p2) => JsonConvert.SerializeObject(p1, Settings) == JsonConvert.SerializeObject(p2, Settings),
                    p => p == null ? 0 : JsonConvert.SerializeObject(p, Settings).GetHashCode(),
                    p => p == null ? null : JsonConvert.DeserializeObject<CwlSeasonPerformance>(JsonConvert.SerializeObject(p, Settings), Settings)
                ));
    }
}
