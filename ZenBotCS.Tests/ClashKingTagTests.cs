using ZenBotCS.Clients;

namespace ZenBotCS.Tests;

/// <summary>
/// /v2/links/shared rejects a whole batch of up to 100 tags with a 400 if any single tag is
/// malformed, so the client filters them out before sending. These pin the filter to what the API
/// actually accepts.
/// </summary>
public class ClashKingTagTests
{
    [Theory]
    [InlineData("#2PP", "#2PP")]
    [InlineData("2PP", "#2PP")]                     // the leading # is optional on input
    [InlineData("#89r0lp02", "#89R0LP02")]          // casing is normalised
    [InlineData("  #2PP  ", "#2PP")]                // surrounding whitespace is trimmed
    [InlineData("#O289", "#0289")]                  // the usual letter-O / zero mix-up
    public void ValidTags_AreCanonicalised(string raw, string expected)
    {
        Assert.Equal(expected, ClashKingApiClient.NormalizeTag(raw));
    }

    [Theory]
    [InlineData("#ABC")]        // outside the base-14 alphabet
    [InlineData("#NOTATAG")]
    [InlineData("#PP")]         // too short — the API wants at least three characters
    [InlineData("#")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void MalformedTags_AreRejected(string? raw)
    {
        Assert.Null(ClashKingApiClient.NormalizeTag(raw));
    }
}
