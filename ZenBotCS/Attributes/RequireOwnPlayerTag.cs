using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZenBotCS.Entities;

namespace ZenBotCS.Attributes;

public class RequireOwnPlayerTag(string _parameterName) : PreconditionAttribute
{

    public override Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo commandInfo, IServiceProvider services)
    {

        if (context is not IInteractionContext interactionContext)
            return Task.FromResult(PreconditionResult.FromError("Command is not an interaction."));

        if (interactionContext.Interaction is not ISlashCommandInteraction slashCommand)
            return Task.FromResult(PreconditionResult.FromError("Command is not a slash command."));

        var innermostOptions = GetInnermostOptions(slashCommand.Data.Options);

        if (innermostOptions is null)
            return Task.FromResult(PreconditionResult.FromError("Failed getting command parameters."));

        var parameterValue = innermostOptions.FirstOrDefault(option => option.Name == _parameterName)?.Value.ToString() ?? "";

        using var scope = services.CreateScope();
        var botDb = services.GetRequiredService<BotDataContext>();
        var userId = context.User.Id;
        var usersTags = botDb.DiscordLinks.Where(dl => dl.DiscordId == userId).Select(dl => dl.PlayerTag).ToList();

        var logger = services.GetRequiredService<ILogger<RequireOwnPlayerTag>>();

        if (usersTags.Contains(parameterValue))
        {
            return Task.FromResult(PreconditionResult.FromSuccess());
        }
        else
        {
            return Task.FromResult(PreconditionResult.FromError("You do not have permission to use this command."));
        }
    }


    private static IEnumerable<IApplicationCommandInteractionDataOption>? GetInnermostOptions(IEnumerable<IApplicationCommandInteractionDataOption>? options)
    {
        // Loop until we reach the deepest subcommand level
        while (options?.FirstOrDefault()?.Type == ApplicationCommandOptionType.SubCommand ||
               options?.FirstOrDefault()?.Type == ApplicationCommandOptionType.SubCommandGroup)
        {
            options = options.FirstOrDefault()?.Options;
        }
        return options;
    }
}