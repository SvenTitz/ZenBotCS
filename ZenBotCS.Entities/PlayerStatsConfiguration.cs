using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newtonsoft.Json;
using ZenBotCS.Entities.Models;
using ZenBotCS.Entities.Models.ClashKingApi.PlayerStats;
using ZenBotCS.Entities.Models.ClashKingApi.PlayerWarHits;

namespace ZenBotCS.Entities;

public class PlayerStatsConfiguration : IEntityTypeConfiguration<PlayerStats>
{
    public void Configure(EntityTypeBuilder<PlayerStats> builder)
    {
        builder.Property(p => p.Player)
            .HasConversion(
                p => JsonConvert.SerializeObject(p, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }),
                p => JsonConvert.DeserializeObject<Player>(p, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }),
                new ValueComparer<Player>
                (
                    (p1, p2) => EqualsExpression(p1, p2),
                    p => p.GetHashCode(),
                    p => p
                ));

        builder.Property(p => p.PlayerWarhits)
            .HasConversion(
                p => JsonConvert.SerializeObject(p, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }),
                p => JsonConvert.DeserializeObject<PlayerWarhits>(p, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }),
                new ValueComparer<PlayerWarhits>
                (
                    (p1, p2) => EqualsExpression(p1, p2),
                    p => p.GetHashCode(),
                    p => p
                ));
    }

    private static bool EqualsExpression(Player? x, Player? y)
    {
        if (x is null)
            return y is null;
        if (y is null)
            return false;
        return x.Equals(y);
    }

    private static bool EqualsExpression(PlayerWarhits? x, PlayerWarhits? y)
    {
        if (x is null)
            return y is null;
        if (y is null)
            return false;
        return x.Equals(y);
    }

}
