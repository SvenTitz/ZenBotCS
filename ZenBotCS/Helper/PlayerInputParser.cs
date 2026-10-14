using System.Text.RegularExpressions;

namespace ZenBotCS.Helper;

public static partial class PlayerInputParser
{
    // Clash of Clans tags use only these characters.
    [GeneratedRegex("^[0289PYLQGRJCUV]{5,12}$", RegexOptions.IgnoreCase)]
    private static partial Regex TagRegex();

    public static string NormalizeTag(string raw) =>
        "#" + raw.Trim().TrimStart('#').ToUpperInvariant();

    public static bool LooksLikeTag(string raw) =>
        TagRegex().IsMatch(raw.Trim().TrimStart('#'));
}
