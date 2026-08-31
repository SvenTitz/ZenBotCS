using System.Text;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZenBotCS.Entities;

namespace ZenBotCS.Services.SlashCommands
{
    public class CwlRolesService(
        BotDataContext _botDb,
        GspreadService _gspreadService,
        CwlRosterSource _rosterSource,
        DiscordLinkSource _discordLinkSource,
        ILogger<CwlRolesService> _logger)
    {
        public async Task<string> RolesAssign(SocketInteractionContext context, SocketRole role, string? spreadsheetUrl, string? clantag)
        {
            // Explicit sheet url -> read that sheet. Otherwise use the clan's roster source (DB by default,
            // pinned sheet as backup).
            List<string>? playerTags;
            if (spreadsheetUrl is not null)
                playerTags = await _gspreadService.GetPlayerTags(spreadsheetUrl);
            else if (clantag is not null)
                playerTags = await _rosterSource.GetRosterPlayerTagsAsync(clantag);
            else
                return "Please provide either a spreadsheet-url or select a clan with a roster.";

            if (playerTags is null || playerTags.Count == 0)
                return "No roster found. Provide a spreadsheet-url or select a clan with a roster.";

            var discordLinks = await _discordLinkSource.GetDiscordIdsAsync(playerTags);

            // Every drop between "player is on the roster" and "user has the role" used to be
            // silent, which made a half-finished assign impossible to explain. Count each reason
            // separately and report it back instead of always answering "done".
            var unlinkedTags = playerTags.Where(tag => !discordLinks.ContainsKey(tag)).ToList();
            var userIds = discordLinks.Values.Distinct().ToList();

            // One user can own several accounts on the roster, so player count > user count is
            // normal, not a failure. Keep the mapping to name the players in the summary.
            var tagsByUserId = discordLinks
                .GroupBy(link => link.Value)
                .ToDictionary(g => g.Key, g => string.Join("/", g.Select(link => link.Key)));

            var assigned = new List<ulong>();
            var alreadyHad = new List<ulong>();
            var notInGuild = new List<ulong>();
            var cacheMisses = new List<ulong>();
            var failed = new List<ulong>();

            foreach (var userId in userIds)
            {
                IGuildUser? user = context.Guild.GetUser(userId);

                if (user is null)
                {
                    // GetUser only reads the socket cache. An incomplete cache looks exactly like
                    // "this user left the server", so ask the API before writing anyone off.
                    try
                    {
                        user = await context.Client.Rest.GetGuildUserAsync(context.Guild.Id, userId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to fetch guild user {userId}", userId);
                    }

                    if (user is not null)
                        cacheMisses.Add(userId);
                }

                if (user is null)
                {
                    notInGuild.Add(userId);
                    continue;
                }

                if (user.RoleIds.Contains(role.Id))
                {
                    alreadyHad.Add(userId);
                    continue;
                }

                try
                {
                    await user.AddRoleAsync(role);
                    assigned.Add(userId);
                }
                catch (Exception ex)
                {
                    failed.Add(userId);
                    _logger.LogError(ex, "Failed to assign role {roleId} to user {userId}", role.Id, userId);
                }
            }

            _logger.LogInformation(
                "Cwl roles assign ({source}): {tagCount} roster tags -> {linkCount} links -> {userCount} distinct users. " +
                "Assigned {assignedCount}, already had the role {alreadyHadCount}, not in the guild {notInGuildCount}, " +
                "failed {failedCount}, resolved via REST after a cache miss {cacheMissCount}. " +
                "Roster tags with no link: {unlinkedTags}. Linked users not in the guild: {notInGuildIds}.",
                spreadsheetUrl ?? clantag, playerTags.Count, discordLinks.Count, userIds.Count,
                assigned.Count, alreadyHad.Count, notInGuild.Count, failed.Count, cacheMisses.Count,
                string.Join(", ", unlinkedTags), string.Join(", ", notInGuild));

            var summary = new StringBuilder($"Assigned {role.Name} to **{assigned.Count}** of {userIds.Count} linked users ({playerTags.Count} players on the roster).");

            if (alreadyHad.Count > 0)
                summary.Append($"\n- {alreadyHad.Count} already had the role.");
            if (unlinkedTags.Count > 0)
                summary.Append($"\n- {unlinkedTags.Count} roster players have no discord link: {Sample(unlinkedTags)}");
            if (notInGuild.Count > 0)
                summary.Append($"\n- {notInGuild.Count} linked users are not in this server: {Sample(notInGuild.Select(Describe))}");
            if (failed.Count > 0)
                summary.Append($"\n- {failed.Count} failed, check the bot log: {Sample(failed.Select(Describe))}");
            if (cacheMisses.Count > 0)
                summary.Append($"\n- {cacheMisses.Count} were missing from the bot's member cache and had to be fetched.");

            return summary.ToString();

            string Describe(ulong userId) => $"{tagsByUserId.GetValueOrDefault(userId, "?")} ({userId})";
        }

        /// <summary>A few entries for a Discord message, without risking the 2000 character limit.</summary>
        private static string Sample(IEnumerable<string> values, int max = 15)
        {
            var list = values.ToList();
            var shown = string.Join(", ", list.Take(max));
            return list.Count > max ? $"{shown} (+{list.Count - max} more)" : shown;
        }

        public async Task<string> RolesRemove(SocketInteractionContext context)
        {
            try
            {
                var clanSettings = _botDb.ClanSettings.AsNoTracking();
                var roleIds = clanSettings!.Where(cs => cs.CwlRoleId != null && cs.CwlRoleId > 0).Select(o => o.CwlRoleId!.Value).ToList();

                var usersWithRoles = context.Guild.Users
                    .Where(u => u.Roles.Any(r => roleIds.Contains(r.Id)))
                    .ToList();

                foreach (var user in usersWithRoles)
                {
                    try
                    {
                        await user.RemoveRolesAsync(roleIds);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to remove roles from user {userId}", user.Id);
                    }
                }

                return "Done removing roles";
            }
            catch (Exception e)
            {
                return $"**Error**: {e.Message}";
            }
        }
    }
}
