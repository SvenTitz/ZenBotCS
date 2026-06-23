using ZenBotCS.Entities.Models.Enums;
using ZenBotCS.Services.SlashCommands;

namespace ZenBotCS.Tests;

public class OptOutParsingTests
{
    [Fact]
    public void Empty_ReturnsNone()
    {
        Assert.Equal(OptOutDays.None, CwlSignupWizardService.ConvertOptOutDataToEnum([]));
    }

    [Fact]
    public void SingleFlag_IsParsed()
    {
        Assert.Equal(OptOutDays.Day1, CwlSignupWizardService.ConvertOptOutDataToEnum(["1"]));
    }

    [Fact]
    public void MultipleFlags_AreCombined()
    {
        var result = CwlSignupWizardService.ConvertOptOutDataToEnum(["1", "2", "4"]);

        Assert.Equal(OptOutDays.Day1 | OptOutDays.Day2 | OptOutDays.Day3, result);
    }

    [Fact]
    public void DuplicateFlags_AreIdempotent()
    {
        Assert.Equal(OptOutDays.Day1, CwlSignupWizardService.ConvertOptOutDataToEnum(["1", "1"]));
    }

    [Fact]
    public void UnparseableValues_AreIgnored()
    {
        Assert.Equal(OptOutDays.Day7, CwlSignupWizardService.ConvertOptOutDataToEnum(["abc", "64"]));
    }

    [Fact]
    public void NonFlagNumericValues_AreIgnored()
    {
        // 3 is not a defined OptOutDays member; only exact flag values (0/1/2/4/8/...) are accepted.
        Assert.Equal(OptOutDays.None, CwlSignupWizardService.ConvertOptOutDataToEnum(["3"]));
    }
}
