using Discord;
using Discord.Addons.Hosting;
using Discord.Addons.Hosting.Util;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ZenBotCS.Handler;

internal class InteractionHandler : DiscordClientService
{
    private readonly InteractionService _interactionService;
    private readonly IServiceProvider _provider;

    public InteractionHandler(DiscordSocketClient client, ILogger<DiscordClientService> logger, InteractionService interactionService, IServiceProvider provider) : base(client, logger)
    {
        _interactionService = interactionService;
        _provider = provider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Client.InteractionCreated += HandleInteraction;

        _interactionService.SlashCommandExecuted += SlashCommandExecuted;

        await _interactionService.AddModulesAsync(Assembly.GetEntryAssembly(), _provider);
        await Client.WaitForReadyAsync(stoppingToken);

        await _interactionService.RegisterCommandsToGuildAsync(1078588731549814805); // Test Server
        await _interactionService.RegisterCommandsToGuildAsync(355698225031806978); // Zen
        //await _interactionService.RegisterCommandsGloballyAsync();
    }

    private Task SlashCommandExecuted(SlashCommandInfo commandInfo, IInteractionContext context, IResult result)
    {
        if (!result.IsSuccess)
        {
            switch (result.Error)
            {
                case InteractionCommandError.UnmetPrecondition:
                    // implement
                    break;
                case InteractionCommandError.UnknownCommand:
                    // implement
                    break;
                case InteractionCommandError.BadArgs:
                    // implement
                    break;
                case InteractionCommandError.Exception:
                    // implement
                    break;
                case InteractionCommandError.Unsuccessful:
                    // implement
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
