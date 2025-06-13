using System.Reflection;
using Discord;
using Discord.Addons.Hosting;
using Discord.Addons.Hosting.Util;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using ZenBotCS.Extensions;

namespace ZenBotCS.Handler;

internal class InteractionHandler : DiscordClientService
{
    private readonly InteractionService _interactionService;
    private readonly IServiceProvider _provider;
    private readonly ILogger _logger;

    public InteractionHandler(DiscordSocketClient client, ILogger<DiscordClientService> logger, InteractionService interactionService, IServiceProvider provider) : base(client, logger)
    {
        _interactionService = interactionService;
        _provider = provider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Client.InteractionCreated += HandleInteraction;

        _interactionService.SlashCommandExecuted += SlashCommandExecuted;
        _interactionService.ComponentCommandExecuted += ComponentCommandExecuted;

        await _interactionService.AddModulesAsync(Assembly.GetEntryAssembly(), _provider);
        await Client.WaitForReadyAsync(stoppingToken);

        await _interactionService.RegisterCommandsToGuildAsync(1078588731549814805);
        await _interactionService.RegisterCommandsGloballyAsync();
    }

    private Task SlashCommandExecuted(SlashCommandInfo commandInfo, IInteractionContext context, IResult result)
    {
        if (!result.IsSuccess)
        {
            switch (result.Error)
            {
                case InteractionCommandError.UnmetPrecondition:
                    context.Interaction.RespondAsync(result.ErrorReason, ephemeral: true);
                    break;
                case InteractionCommandError.UnknownCommand:
                    context.Interaction.RespondAsync(result.ErrorReason, ephemeral: true);
                    break;
                case InteractionCommandError.BadArgs:
                    context.Interaction.RespondAsync(result.ErrorReason, ephemeral: true);
                    break;
                case InteractionCommandError.Exception:
                    context.Interaction.RespondAsync(result.ErrorReason, ephemeral: true);
                    break;
                case InteractionCommandError.Unsuccessful:
                    context.Interaction.RespondAsync(result.ErrorReason, ephemeral: true);
                    break;
                default:
                    break;
            }
        }

        return Task.CompletedTask;
    }

    private Task ComponentCommandExecuted(ComponentCommandInfo commandInfo, IInteractionContext context, IResult result)
    {
        if (!result.IsSuccess)
        {
            switch (result.Error)
            {
                case InteractionCommandError.UnmetPrecondition:
                    if (commandInfo.Name == "button_cwl_signup_reopen" || commandInfo.Name == "button_cwl_signup_close")
                    {
                        var userName = (context.User as SocketGuildUser)?.DisplayName;
                        context.Interaction.RespondAsync($"You know you were not supposed to press that button {userName} <:sus:1231358163320574044> " +
                            "\nYour name has been reported to the authorities <:monkaGun:1231358440543228014> ", ephemeral: true);
                    }
                    else
                    {
                        context.Interaction.RespondAsync(result.ErrorReason, ephemeral: true);
                    }
                    break;
                case InteractionCommandError.UnknownCommand:
                    context.Interaction.RespondAsync(result.ErrorReason, ephemeral: true);
                    break;
                case InteractionCommandError.BadArgs:
                    context.Interaction.RespondAsync(result.ErrorReason, ephemeral: true);
                    break;
                case InteractionCommandError.Exception:
                    context.Interaction.RespondAsync(result.ErrorReason, ephemeral: true);
                    break;
                case InteractionCommandError.Unsuccessful:
                    context.Interaction.RespondAsync(result.ErrorReason, ephemeral: true);
                    break;
                default:
                    break;
            }

        }
        return Task.CompletedTask;
    }

    private async Task HandleInteraction(SocketInteraction arg)
    {
        try
        {
            if (arg is SocketSlashCommand slashCommand)
            {
                _logger.LogInformation("{user} used {type}, with params: {data}", slashCommand.User, slashCommand.Data.GetFullNameLogString(), slashCommand.Data.GetParamLogString());
            }
            else if (arg is SocketMessageComponent messageComponent)
            {
                // Log button or component interaction
                _logger.LogInformation("{user} used interaction {customId}, with data: {data}",
                    messageComponent.User,
                    messageComponent.Data.CustomId,
                    messageComponent.Data.Values);
            }

            // Create an execution context that matches the generic type parameter of your InteractionModuleBase<T> modules
            var ctx = new SocketInteractionContext(Client, arg);
            await _interactionService.ExecuteCommandAsync(ctx, _provider);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Exception occurred whilst attempting to handle interaction.");

            if (arg.Type == InteractionType.ApplicationCommand)
            {
                var msg = await arg.GetOriginalResponseAsync();
                await msg.DeleteAsync();
            }

        }
    }
}
