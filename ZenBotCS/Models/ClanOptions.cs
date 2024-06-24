namespace ZenBotCS.Models;

public class ClanOptions
{
    public static string String => "ClanOptions";
    public string ClanTag { get; set; } = default!;
    public string ClanAlias { get; set; } = default!;
    public string ColorHex { get; set; } = default!;
    public ulong CwlRoleId { get; set; }
    public bool IsFWA { get; set; }
    public bool DisableCwlSignup { get; set; }
    public bool ChampStyleCwlSignup { get; set; }
}
