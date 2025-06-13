using System.Globalization;
using System.Text;
using Discord;
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

        public Embed SpinTimes()
        {
            List<long> spinTimestamps =
            [
                GetNextTimestamp(DayOfWeek.Sunday, 19, 0, 9),
                GetNextTimestamp(DayOfWeek.Tuesday, 21, 0, 9),
                GetNextTimestamp(DayOfWeek.Thursday, 23, 0, 9)
            ];
            spinTimestamps.Sort();

            List<long> mandoTimes = GetNextMandos();

            var stringBuilder = new StringBuilder();

            stringBuilder.AppendLine("Next war spin times: ");
            foreach (var timestamp in spinTimestamps)
            {
                stringBuilder.AppendLine($"- <t:{timestamp}:F>");
            }
            stringBuilder.AppendLine();

            stringBuilder.AppendLine("Next **mandatory** war spin dates*: ");
            foreach (var timestamp in mandoTimes)
            {
                stringBuilder.AppendLine($"- <t:{timestamp}:D>");
            }


            var embedBuilder = new EmbedBuilder()
                .WithColor(Color.Purple)
                .WithTitle("Spin Times")
                .WithDescription(stringBuilder.ToString())
                .WithFooter("* mandatory war spin dates might change. Keep an eye out for annoucements.");

            return embedBuilder.Build();
        }

        private static long GetNextTimestamp(DayOfWeek targetDay, int hour, int minute, int daysSkippedAtBeginningOfMonth)
        {
            DateTime utcNow = DateTime.UtcNow;

            for (int i = 0; i < 21; i++) // Look up to 3 weeks ahead
            {
                DateTime candidate = utcNow.Date.AddDays(i);

                if (candidate.Day <= daysSkippedAtBeginningOfMonth)
                    continue;
                if (candidate.DayOfWeek != targetDay)
                    continue;

                var targetDateTimeUtc = new DateTime(candidate.Year, candidate.Month, candidate.Day, hour, minute, 0, DateTimeKind.Utc);
                if (targetDateTimeUtc > utcNow)
                {
                    return new DateTimeOffset(targetDateTimeUtc).ToUnixTimeSeconds();
                }
            }

            throw new Exception("No valid UTC timestamp found");
        }

        private List<long> GetNextMandos()
        {
            List<long> validThursdays = [];
            DateTime current = DateTime.UtcNow;
            int year = current.Year;
            int month = current.Month;

            while (validThursdays.Count < 2)
            {
                if (current.DayOfWeek == DayOfWeek.Thursday
                    && (InDayRange(current, 10, 16) || InDayRange(current, 24, 30)))
                {
                    validThursdays.Add(new DateTimeOffset(current).ToUnixTimeSeconds());
                }
                current = current.AddDays(1);
            }

            return validThursdays;
        }

        private bool InDayRange(DateTime dateTime, int minDay, int maxDay)
        {
            return dateTime.Day >= minDay && dateTime.Day <= maxDay;
        }
    }
}
