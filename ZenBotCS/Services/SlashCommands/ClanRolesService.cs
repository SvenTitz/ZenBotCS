using System.Text;
using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using ZenBotCS.Clients;
using ZenBotCS.Entities;
using ZenBotCS.Helper;

namespace ZenBotCS.Services.SlashCommands;

public class ClanRolesService(CustomClansClient _clansClient, BotDataContext _botDb, EmbedHelper _embedHelper)
{
    public async Task<Embed[]> Audit(SocketInteractionContext context, string clanTag)
    {
        var settings = _botDb.ClanSettings.AsNoTracking().FirstOrDefault(cs => cs.ClanTag == clanTag);
        if (settings is null)
            return [_embedHelper.ErrorEmbed("Error", $"No clan settings found for {clanTag}.")];

        List<(string Label, ulong Id)> roles = [];
        if (settings.MemberRoleId is > 0) roles.Add(("Member", settings.MemberRoleId.Value));
        if (settings.ElderRoleId is > 0) roles.Add(("Elder", settings.ElderRoleId.Value));
        if (settings.LeaderRoleId is > 0) roles.Add(("Lead", settings.LeaderRoleId.Value));

        if (roles.Count == 0)
            return [_embedHelper.ErrorEmbed("Error", "This clan has no Member, Elder or Leadership role configured. Set them with `/clan settings edit`.")];

        var roleIds = roles.Select(r => r.Id).ToHashSet();

        var clan = await _clansClient.GetOrFetchClanAsync(clanTag);
        if (clan is null)
            return [_embedHelper.ErrorEmbed("Error", $"Could not fetch clan {clanTag}.")];

        // Guild.Users and Guild.GetUser only ever read the local cache. If the member list has
        // not been downloaded yet every lookup silently returns null, which would report the
        // whole clan as "not in server", so make sure the cache is filled first.
        if (!context.Guild.HasAllMembers)
            await context.Guild.DownloadUsersAsync();

        if (context.Guild.Users.Count <= 1)
            return [_embedHelper.ErrorEmbed("Error", "Could not load the server member list, so role state cannot be checked. Make sure the Server Members Intent is enabled for the bot in the Discord Developer Portal.")];

        var memberTags = clan.Members.Select(m => m.Tag).ToHashSet();

        var links = _botDb.DiscordLinks.AsNoTracking().ToList();
        // A user counts as "in the clan" if any of their linked accounts is in it.
        var userIdsInClan = links.Where(l => memberTags.Contains(l.PlayerTag)).Select(l => l.DiscordId).ToHashSet();
        var userIdByPlayerTag = links.ToDictionary(l => l.PlayerTag, l => l.DiscordId);

        var stringBuilder = new StringBuilder();

        // Direction 1: holds one of the clan roles, but no linked account in the clan.
        var extraRoles = context.Guild.Users
            .Where(u => u.Roles.Any(r => roleIds.Contains(r.Id)) && !userIdsInClan.Contains(u.Id))
            .OrderBy(u => u.DisplayName)
            // Mentions inside embeds only render if the viewer's client already has the user
            // cached, so add the username as a fallback identifier next to the mention.
            .Select(u => $"`{string.Join('/', roles.Where(r => u.Roles.Any(ur => ur.Id == r.Id)).Select(r => r.Label))}` <@{u.Id}> **{Format.Sanitize(u.Username)}**")
            .ToList();

        // Direction 2: in the clan, but the linked user holds none of the clan roles.
        var missingRole = new List<string>();
        var unlinked = new List<string>();
        var notInServer = new List<string>();

        foreach (var member in clan.Members.OrderBy(m => m.Name))
        {
            if (!userIdByPlayerTag.TryGetValue(member.Tag, out var userId))
            {
                unlinked.Add($"`{member.Tag}` **{Format.Sanitize(member.Name)}**");
                continue;
            }

            var user = context.Guild.GetUser(userId);
            if (user is null)
            {
                // Not in the guild, so there is no username to resolve - print the raw id.
                notInServer.Add($"`{member.Tag}` **{Format.Sanitize(member.Name)}** (`{userId}`)");
                continue;
            }

            if (!user.Roles.Any(r => roleIds.Contains(r.Id)))
                missingRole.Add($"`{member.Tag}` **{Format.Sanitize(member.Name)}** <@{user.Id}> ({Format.Sanitize(user.Username)})");
        }

        void AppendSection(string heading, string subtitle, List<string> lines)
        {
            stringBuilder.AppendLine(heading);
            stringBuilder.AppendLine(subtitle);
            foreach (var line in lines)
            {
                stringBuilder.AppendLine(line);
            }
            stringBuilder.AppendLine($"Count: **{lines.Count}**");
            stringBuilder.AppendLine();
        }

        AppendSection("### Extra Roles:", "*Has a clan role, but no linked account in the clan.*", extraRoles);
        AppendSection("### Missing Roles:", "*In the clan, but has none of the clan roles.*", missingRole);
        AppendSection("### No Discord Link:", "*In the clan, but no linked Discord account - role state unknown.*", unlinked);
        AppendSection("### Not In Server:", "*In the clan and linked, but the Discord account is not in this server.*", notInServer);

        var baseEmbed = new EmbedBuilder()
            .WithTitle($"{clan.Name} Role Audit")
            .WithColor(Color.DarkPurple)
            .WithFooter($"Checked roles: {string.Join(", ", roles.Select(r => r.Label))} | {context.Guild.Name}: {context.Guild.Users.Count} cached members, all downloaded: {context.Guild.HasAllMembers}");

        return _embedHelper.BuildEmbedsFromLongDescription(stringBuilder, baseEmbed);
    }
}
