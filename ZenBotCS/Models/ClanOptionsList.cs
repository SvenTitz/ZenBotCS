namespace ZenBotCS.Models;

public class ClanOptionsList
{
    public static string String => "ClanOptionsList";
    public List<ClanOptions> ClanOptions { get; set; } = new();
}
