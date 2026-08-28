using ZenBotCS.Entities.Models.Enums;

namespace ZenBotCS.Web.Components;

/// <summary>
/// Which clans get a roster page. Event and partner clans don't: their CWL players signed up under a
/// war clan and were moved into one of its sub-rosters, so the roster page would render empty. Their
/// CWL history and stats are still their own, and stay reachable.
/// </summary>
public static class RosterTabs
{
    public static bool HasRoster(ClanType type) => type is not (ClanType.Event or ClanType.Partner);
}
