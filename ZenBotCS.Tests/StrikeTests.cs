using ZenBotCS.Entities.Models;
using ZenBotCS.Helper;

namespace ZenBotCS.Tests;

public class StrikeTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    // --- StrikeIdGenerator ---

    [Fact]
    public void GeneratedId_HasExpectedLengthAndAlphabet()
    {
        for (var i = 0; i < 100; i++)
        {
            var id = StrikeIdGenerator.Generate();

            Assert.Equal(StrikeIdGenerator.Length, id.Length);
            Assert.All(id, c => Assert.Contains(c, StrikeIdGenerator.Alphabet));
        }
    }

    [Fact]
    public void GeneratedIds_AreUnique()
    {
        var ids = Enumerable.Range(0, 1_000).Select(_ => StrikeIdGenerator.Generate()).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void Alphabet_ExcludesAmbiguousCharacters()
    {
        Assert.DoesNotContain('I', StrikeIdGenerator.Alphabet);
        Assert.DoesNotContain('L', StrikeIdGenerator.Alphabet);
        Assert.DoesNotContain('O', StrikeIdGenerator.Alphabet);
        Assert.DoesNotContain('0', StrikeIdGenerator.Alphabet);
        Assert.DoesNotContain('1', StrikeIdGenerator.Alphabet);
    }

    // --- Strike.IsActiveAt ---

    [Fact]
    public void NoRollover_IsAlwaysActive()
    {
        var strike = new Strike { CreatedAt = Now.AddYears(-1) };

        Assert.True(strike.IsActiveAt(Now));
    }

    [Fact]
    public void WithinRolloverWindow_IsActive()
    {
        var strike = new Strike { CreatedAt = Now.AddDays(-7), RolloverDays = 30 };

        Assert.True(strike.IsActiveAt(Now));
    }

    [Fact]
    public void ExactlyAtRolloverMoment_IsInactive()
    {
        var strike = new Strike { CreatedAt = Now.AddDays(-30), RolloverDays = 30 };

        Assert.False(strike.IsActiveAt(Now));
    }

    [Fact]
    public void AfterRolloverMoment_IsInactive()
    {
        var strike = new Strike { CreatedAt = Now.AddDays(-31), RolloverDays = 30 };

        Assert.False(strike.IsActiveAt(Now));
    }

    [Fact]
    public void Removed_IsInactive()
    {
        var strike = new Strike { CreatedAt = Now.AddDays(-1), RemovedAt = Now.AddHours(-1) };

        Assert.False(strike.IsActiveAt(Now));
    }

    [Fact]
    public void RollsOverAt_IsNullWithoutRollover()
    {
        var strike = new Strike { CreatedAt = Now };

        Assert.Null(strike.RollsOverAt);
    }

    // --- PlayerInputParser ---

    [Theory]
    [InlineData("#abc123", "#ABC123")]
    [InlineData("abc123", "#ABC123")]
    [InlineData(" #AbC123 ", "#ABC123")]
    public void NormalizeTag_ProducesCanonicalForm(string input, string expected)
    {
        Assert.Equal(expected, PlayerInputParser.NormalizeTag(input));
    }

    [Theory]
    [InlineData("#22J8ULLL", true)]
    [InlineData("22J8ULLL", true)]
    [InlineData("BigKing123", false)]
    [InlineData("CLASH", false)]
    public void LooksLikeTag_DetectsTags(string input, bool expected)
    {
        Assert.Equal(expected, PlayerInputParser.LooksLikeTag(input));
    }
}
