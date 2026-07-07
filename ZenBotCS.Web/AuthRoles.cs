namespace ZenBotCS.Web;

/// <summary>
/// Role claim names used for authorization on the site, in increasing order of privilege.
/// Higher tiers also receive the lower claims (see the OAuth ticket handler in Program.cs), so a
/// gatekeeper is also a leader, and an admin is also a gatekeeper and leader.
/// </summary>
public static class AuthRoles
{
    /// <summary>Leadership: rosters, CWL history, stats, player profiles. (Discord:RequiredRoleId)</summary>
    public const string RosterAccess = "RosterAccess";

    /// <summary>Gatekeepers: everything leadership can do, plus per-clan settings. (Discord:GatekeeperRoleId)</summary>
    public const string Gatekeeper = "Gatekeeper";

    /// <summary>Admins: everything, plus bot settings. (Discord:AdminUserIds allowlist)</summary>
    public const string Admin = "Admin";
}
