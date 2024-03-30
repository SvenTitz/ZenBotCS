using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;

namespace ZenBotCS.Attributes;

public class RequireLeadershipRoleAttribute : PreconditionAttribute
{
    public override Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo commandInfo, IServiceProvider services)
    {
        ulong requiredRoleId;

        if (services.GetService(typeof(IConfiguration)) is IConfiguration config)
        {
            requiredRoleId = ulong.Parse(config["FamilyLeadershipRoleId"]!);
        }
        else
        {
            return Task.FromResult(PreconditionResult.FromError("Configuration not available."));
        }

        if (context.User is SocketGuildUser guildUser && guildUser.Roles.Any(r => r.Id == requiredRoleId))
        {
            return Task.FromResult(PreconditionResult.FromSuccess());
        }
        else
        {
            return Task.FromResult(PreconditionResult.FromError("You do not have permission to use this command."));
        }
    }
}
