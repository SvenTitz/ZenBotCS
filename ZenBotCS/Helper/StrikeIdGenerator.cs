using System.Security.Cryptography;

namespace ZenBotCS.Helper;

public static class StrikeIdGenerator
{
    // No I, L, O, 0, 1 - avoids ambiguity when IDs are read from a screen.
    public const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    public const int Length = 6;

    public static string Generate() => RandomNumberGenerator.GetString(Alphabet, Length);
}
