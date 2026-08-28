using ZenBotCS.Entities.Models;
using ZenBotCS.Entities.Models.Enums;

namespace ZenBotCS.Entities;

/// <summary>
/// The rules a subroster has to satisfy before it can be created. Kept pure and out of the web layer
/// so the checks can be tested, and so the caller can show a reason instead of surfacing the unique
/// index on <see cref="SubRoster.GameClanTag"/> as a raw database error.
/// </summary>
public static class SubRosterRules
{
    /// <summary>Clan types allowed to host a subroster — never a clan that runs its own CWL.</summary>
    public static bool CanHost(ClanType type) => type is ClanType.Event or ClanType.Partner;

    /// <summary>
    /// Whether <paramref name="gameClanTag"/> may host a new subroster for <paramref name="clanTag"/>.
    /// </summary>
    /// <param name="hostClanType">
    /// The candidate host's type, or null when it isn't a managed clan at all.
    /// </param>
    /// <param name="existing">Every subroster that already exists, across all clans.</param>
    public static ValidationResult ValidateNew(
        string clanTag,
        string gameClanTag,
        string name,
        ClanType? hostClanType,
        IEnumerable<SubRoster> existing)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ValidationResult.Fail("Give the roster a name.");

        if (string.IsNullOrWhiteSpace(gameClanTag))
            return ValidationResult.Fail("Pick the clan this roster plays in.");

        if (string.Equals(gameClanTag, clanTag, StringComparison.OrdinalIgnoreCase))
            return ValidationResult.Fail("A roster can't play in the clan it belongs to — that's the main roster.");

        if (hostClanType is null)
            return ValidationResult.Fail("That clan isn't set up in the bot yet. Add it as an event or partner clan first.");

        if (!CanHost(hostClanType.Value))
            return ValidationResult.Fail("Only event and partner clans can host a roster, since war clans run their own CWL.");

        var taken = existing.FirstOrDefault(sr =>
            string.Equals(sr.GameClanTag, gameClanTag, StringComparison.OrdinalIgnoreCase));
        if (taken is not null)
            return ValidationResult.Fail($"That clan already hosts \"{taken.Name}\". A clan can only host one roster.");

        return ValidationResult.Success();
    }

    /// <summary>Outcome of a rule check: ok, or the reason to show the user.</summary>
    public readonly record struct ValidationResult(bool Ok, string? Error)
    {
        public static ValidationResult Success() => new(true, null);
        public static ValidationResult Fail(string error) => new(false, error);
    }
}
