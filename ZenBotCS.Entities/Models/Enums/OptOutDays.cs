namespace ZenBotCS.Entities.Models.Enums;

[Flags]
public enum OptOutDays
{
    None = 0,
    Day1 = 1,
    Day2 = 2,
    Day3 = 4,
    Day4 = 8,
    Day5 = 16,
    Day6 = 32,
    Day7 = 64,
}
