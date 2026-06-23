using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZenBotCS.Clients;
using ZenBotCS.Entities;

namespace ZenBotCS.Services.SlashCommands
{
    public class CwlRolesService(
        BotDataContext _botDb,
        GspreadService _gspreadService,
        ClashKingApiClient _clashKingApiClient,
        ILogger<CwlRolesService> _logger)
    {
        public async Task<string> RolesAssign(SocketInteractionContext context, SocketRole role, string? spreadsheetUrl, string? clantag)
        {
            if (spreadsheetUrl is null && _botDb.PinnedRosters.FirstOrDefault(x => x.ClanTag == clantag)?.SpreadsheetId is null)
            {
                return "Please provide either a spreadsheet-url or select a clan with a pinned roster.";
            }

            spreadsheetUrl ??= _gspreadService.GetUrl(_botDb.PinnedRosters.FirstOrDefault(x => x.ClanTag == clantag)!);

            var playerTags = await _gspreadService.GetPlayerTags(spreadsheetUrl);
            var discordLinks = await _clashKingApiClient.PostDiscordLinksAsync(playerTags);
            var userIds = discordLinks.Values.OfType<ulong>().Distinct();

            foreach (var userId in userIds)
            {
                var user = context.Guild.GetUser(userId);
                if (user != null && !user.Roles.Contains(role))
                {
                    try
                    {
                        await user.AddRoleAsync(role);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to assign role to user {userId}", user.Id);
                    }
                }
            }

            return "done";
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
