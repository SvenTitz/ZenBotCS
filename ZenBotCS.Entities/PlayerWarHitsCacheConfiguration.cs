using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newtonsoft.Json;
using ZenBotCS.Entities.Models;
using ZenBotCS.Entities.Models.ClashKingApi.PlayerWarHits;

namespace ZenBotCS.Entities;

public class PlayerWarHitsCacheConfiguration : IEntityTypeConfiguration<PlayerWarHitsCache>
{
    public void Configure(EntityTypeBuilder<PlayerWarHitsCache> builder)
    {
        // Same JSON-column treatment as WarHistory: the ClashKing payload is stored verbatim.
        builder.Property(c => c.WarHits)
            .HasConversion(
                w => JsonConvert.SerializeObject(w, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }),
                w => JsonConvert.DeserializeObject<PlayerWarhits>(w, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }),
                new ValueComparer<PlayerWarhits>
                (
                    (w1, w2) => EqualsExpression(w1, w2),
                    w => w.GetHashCode(),
                    w => w
                ));
    }

    private static bool EqualsExpression(PlayerWarhits? x, PlayerWarhits? y)
    {
        if (x is null)
            return y is null;
        if (y is null)
            return false;
        return ReferenceEquals(x, y) || x.Items.SequenceEqual(y.Items);
    }
}
