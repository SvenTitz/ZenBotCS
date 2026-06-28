namespace ZenBotCS.Web;

/// <summary>Role claim names used for authorization on the roster site.</summary>
public static class AuthRoles
{
    /// <summary>Granted when the signed-in user holds the configured Discord role (Discord:RequiredRoleId).</summary>
    public const string RosterAccess = "RosterAccess";
}
