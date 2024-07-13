using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newtonsoft.Json;
using ZenBotCS.Entities.Models;
using ZenBotCS.Entities.Models.ClashKingApi;

namespace ZenBotCS.Entities;

public class WarHistoryConfiguration : IEntityTypeConfiguration<WarHistory>
{
    public void Configure(EntityTypeBuilder<WarHistory> builder)
    {
        // This Converter will perform the conversion to and from Json to the desired type
        builder.Property(wh => wh.WarData)
            .HasConversion(
                wd => JsonConvert.SerializeObject(wd, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }),
                wd => JsonConvert.DeserializeObject<List<WarData>>(wd, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }),
                new ValueComparer<List<WarData>>
                (
                    (wd1, wd2) => EqualsExpression(wd1, wd2),
                    wd => wd.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    wd => wd.ToList()
                ));

    }

    private static bool EqualsExpression(List<WarData>? x, List<WarData>? y)
    {
        if (x is null)
            return y is null;
        if (y is null)
            return false;
        return x.SequenceEqual(y);
    }
}
