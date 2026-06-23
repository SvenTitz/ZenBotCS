using Discord;
using Discord.Addons.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using ZenBotCS.Helper;
using ZenBotCS.Models.Enums;

namespace ZenBotCS.Tests;

public class EmbedHelperTests
{
    private static EmbedHelper CreateHelper() => new(NullLogger<DiscordClientService>.Instance);

    [Theory]
    [InlineData(7, "⁷")]      // ⁷
    [InlineData(0, "⁰")]      // ⁰
    [InlineData(10, "¹⁰")] // ¹⁰
    [InlineData(123, "¹²³")] // ¹²³
    public void ToSuperscript_ConvertsEachDigit(int input, string expected)
    {
        Assert.Equal(expected, CreateHelper().ToSuperscript(input));
    }

    [Fact]
    public void ErrorEmbed_SetsTitleDescriptionAndRedColor()
    {
        var embed = CreateHelper().ErrorEmbed("Oops", "Something broke");

        Assert.Equal("Oops", embed.Title);
        Assert.Equal("Something broke", embed.Description);
        Assert.Equal(Color.Red, embed.Color!.Value);
    }

    [Fact]
    public void FormatAsTable_LeftAlign_PadsColumnsToEqualWidth()
    {
        var data = new List<string[]>
        {
            new[] { "Name", "TH" },
            new[] { "Bob", "15" }
        };

        var lines = CreateHelper().FormatAsTable(data).Split('\n');

        Assert.Equal("Name  TH  ", lines[0]);
        Assert.Equal("Bob   15  ", lines[1]);
    }

    [Fact]
    public void FormatAsTable_RightAlign_PadsOnTheLeft()
    {
        var data = new List<string[]>
        {
            new[] { "X" },
            new[] { "YYY" }
        };

        var lines = CreateHelper().FormatAsTable(data, TextAlign.Right, TextAlign.Right).Split('\n');

        Assert.Equal("  X  ", lines[0]);
        Assert.Equal("YYY  ", lines[1]);
    }
}
