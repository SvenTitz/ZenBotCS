using ZenBotCS.Entities.Models.ClashKingApi.PlayerWarHits;
using ZenBotCS.Services.SlashCommands;

namespace ZenBotCS.Tests;

public class AttackStatRowTests
{
    private static Attack Hit(int stars) => new() { Stars = stars };

    [Fact]
    public void CountsEachStarBucketAndSuccessRate()
    {
        var hits = new[] { Hit(0), Hit(3), Hit(3) };

        var row = PlayerService.BuildAttackStatRow("9Rea", hits, stars => stars == 3);

        // label, 0*, 1*, 2*, 3*, success% — success here = two of three 3-star hits
        Assert.Equal(new[] { "9Rea", "1/3", "0/3", "0/3", "2/3", "67%" }, row);
    }

    [Fact]
    public void EmptyHits_ProduceZeroCellsAndDash()
    {
        var row = PlayerService.BuildAttackStatRow("9v9", [], stars => stars >= 2);

        Assert.Equal(new[] { "9v9", "0/0", "0/0", "0/0", "0/0", " - " }, row);
    }

    [Fact]
    public void SuccessPredicate_DecidesWhatCounts()
    {
        var hits = new[] { Hit(2), Hit(2), Hit(3), Hit(0) };

        // reach-style success counts 2+ stars: 3 of 4 = 75%
        var row = PlayerService.BuildAttackStatRow("10Rea", hits, stars => stars >= 2);

        Assert.Equal("75%", row[5]);
    }
}
