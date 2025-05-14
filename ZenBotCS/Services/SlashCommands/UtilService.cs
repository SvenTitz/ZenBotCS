using System.Globalization;
using ZenBotCS.Models.Enums;

namespace ZenBotCS.Services.SlashCommands
{
    public class UtilService
    {
        const string timestampTemplate = "`<t:{0}>`   <t:{0}>\n"
                                        + "`<t:{0}:t>`   <t:{0}:t>\n"
                                        + "`<t:{0}:T>`   <t:{0}:T>\n"
                                        + "`<t:{0}:d>`   <t:{0}:d>\n"
                                        + "`<t:{0}:D>`   <t:{0}:D>\n"
                                        + "`<t:{0}:f>`   <t:{0}:f>\n"
                                        + "`<t:{0}:F>`   <t:{0}:F>\n"
                                        + "`<t:{0}:R>`   <t:{0}:R>\n";
        public string Timestamp(TimeZoneEnum timeZoneEnum, string timeInput, int? day, int? month, int? year)
        {
            // 1. Parse time string
            if (!TryParseTime(timeInput, out var time))
                return "Invalid time format. Use HH:mm or HH:mmtt (e.g., 10:30am or 17:28).";

            // 2. Resolve full date components
            var now = DateTime.UtcNow;

            int finalYear = year ?? now.Year;
            int finalMonth = month ?? now.Month;
            int finalDay = day ?? now.Day;

            DateTime dateWithTime;
            try
            {
                dateWithTime = new DateTime(finalYear, finalMonth, finalDay,
                    time.Hour, time.Minute, time.Second);
            }
            catch
            {
                return "Invalid date.";
            }


            // 3. Get TimeZoneInfo
            var tzInfo = TimeZoneMapper.GetTimeZoneInfo(timeZoneEnum);

            // 4. Convert to UTC using time zone (handles DST automatically)
            var utcDateTime = TimeZoneInfo.ConvertTimeToUtc(dateWithTime, tzInfo);

            var timestamp = new DateTimeOffset(utcDateTime).ToUnixTimeSeconds();

            return string.Format(timestampTemplate, timestamp);
        }

        private static bool TryParseTime(string input, out DateTime time)
        {
            var formats = new[] { "h:mmtt", "hh:mmtt", "H:mm", "HH:mm" };
            return DateTime.TryParseExact(
                input,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out time
            );
        }
    }
}
