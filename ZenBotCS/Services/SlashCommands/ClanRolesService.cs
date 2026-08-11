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

        var memberTags = clan.Members.Select(m => m.Tag).ToHashSet();

        var links = _botDb.DiscordLinks.AsNoTracking().ToList();
        // A user counts as "in the clan" if any of their linked accounts is in it.
        var userIdsInClan = links.Where(l => memberTags.Contains(l.PlayerTag)).Select(l => l.DiscordId).ToHashSet();
        var userIdByPlayerTag = links.ToDictionary(l => l.PlayerTag, l => l.DiscordId);

        var stringBuilder = new StringBuilder();

        // Direction 1: holds one of the clan roles, but no linked account in the clan.
        var extraRoleUsers = context.Guild.Users
            .Where(u => u.Roles.Any(r => roleIds.Contains(r.Id)) && !userIdsInClan.Contains(u.Id))
            .OrderBy(u => u.DisplayName)
            .ToList();

        stringBuilder.AppendLine("### Extra Roles:");
        stringBuilder.AppendLine("*Has a clan role, but no linked account in the clan.*");
        foreach (var user in extraRoleUsers)
        {
            var heldRoles = roles.Where(r => user.Roles.Any(ur => ur.Id == r.Id)).Select(r => r.Label);
            stringBuilder.AppendLine($"`{string.Join('/', heldRoles)}` <@{user.Id}>");
        }
        stringBuilder.AppendLine($"Count: **{extraRoleUsers.Count}**");
        stringBuilder.AppendLine();

        // Direction 2: in the clan, but the linked user holds none of the clan roles.
        var missingRole = new List<string>();
        var unlinked = 0;
        var notInServer = 0;

        foreach (var member in clan.Members.OrderBy(m => m.Name))
        {
            if (!userIdByPlayerTag.TryGetValue(member.Tag, out var userId))
            {
                unlinked++;
                continue;
            }

            var user = context.Guild.GetUser(userId);
            if (user is null)
            {
                notInServer++;
                continue;
            }

            if (!user.Roles.Any(r => roleIds.Contains(r.Id)))
                missingRole.Add($"`{member.Tag}` **{member.Name}** <@{user.Id}>");
        }

        stringBuilder.AppendLine("### Missing Roles:");
        stringBuilder.AppendLine("*In the clan, but has none of the clan roles.*");
        foreach (var line in missingRole)
        {
            stringBuilder.AppendLine(line);
        }
        stringBuilder.AppendLine($"Count: **{missingRole.Count}**");

        if (unlinked > 0 || notInServer > 0)
            stringBuilder.AppendLine($"-# Skipped: **{unlinked}** without a Discord link, **{notInServer}** not in this server.");

        var baseEmbed = new EmbedBuilder()
            .WithTitle($"{clan.Name} Role Audit")
            .WithColor(Color.DarkPurple)
            .WithFooter($"Checked roles: {string.Join(", ", roles.Select(r => r.Label))}");

        return _embedHelper.BuildEmbedsFromLongDescription(stringBuilder, baseEmbed);
    }
}
