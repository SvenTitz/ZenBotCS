using ZenBotCS.Models.Enums;
using ZenBotCS.Services;

namespace ZenBotCS.Tests;

public class TimeZoneMapperTests
{
    [Fact]
    public void GetTimeZoneInfo_Utc_HasZeroOffset()
    {
        var tz = TimeZoneMapper.GetTimeZoneInfo(TimeZoneEnum.UTC);

        Assert.Equal(TimeSpan.Zero, tz.BaseUtcOffset);
    }

    [Fact]
    public void GetTimeZoneInfo_Cet_HasOneHourOffset()
    {
        var tz = TimeZoneMapper.GetTimeZoneInfo(TimeZoneEnum.CET);

        Assert.Equal(TimeSpan.FromHours(1), tz.BaseUtcOffset);
    }

    [Theory]
    [InlineData(TimeZoneEnum.UTC)]
    [InlineData(TimeZoneEnum.CET)]
    [InlineData(TimeZoneEnum.EST)]
    [InlineData(TimeZoneEnum.PST)]
    [InlineData(TimeZoneEnum.MST)]
    [InlineData(TimeZoneEnum.CST)]
    [InlineData(TimeZoneEnum.IST)]
    [InlineData(TimeZoneEnum.JST)]
    [InlineData(TimeZoneEnum.AEST)]
    [InlineData(TimeZoneEnum.GMT)]
    public void GetTimeZoneInfo_ResolvesEveryMappedZone(TimeZoneEnum zone)
    {
        var tz = TimeZoneMapper.GetTimeZoneInfo(zone);

        Assert.NotNull(tz);
    }
}
