﻿using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using ZenBotCS.Clients;
using ZenBotCS.Entities;
using ZenBotCS.Entities.Models;
using ZenBotCS.Handler;
using ZenBotCS.Services;

namespace ZenBotCS;


public class TestModule : InteractionModuleBase<SocketInteractionContext>
{
    public required ClashKingApiClient ClashKingApiClient { get; set; }
    public required GspreadService _gspreadService { get; set; }

    public required BotDataContext BotDb { get; set; }

    [SlashCommand("ping", "ping the bot")]
    public async Task Ping()
    {
        var embed = new EmbedBuilder()
            .WithTitle(":ping_pong: Pong!")
            .WithDescription($"The bot latency is {Context.Client.Latency}ms.")
            .WithColor(0x9C84EF)
            .Build();
        await RespondAsync(embed: embed);
    }

    //[SlashCommand("role_add", "ass")]
    public async Task RoleAdd()
    {
        await DeferAsync();

        if (Context.User is SocketGuildUser user)
            await user!.AddRoleAsync(1124272380995444757);

        await FollowupAsync("done");
    }

    //[SlashCommand("role_remopve", "asss")]
    public async Task RoleRemove()
    {
        await DeferAsync();
        var user = Context.User as SocketGuildUser;
        if (user != null)
            await user!.RemoveRoleAsync(1124272380995444757);

        await FollowupAsync("done");
    }

    //[SlashCommand("discord_links", "asdas")]
    public async Task DiscordLinks([Summary("PlayerTag"), Autocomplete(typeof(PlayerTagAutocompleteHandler))] string playerTag)
    {
        await DeferAsync();
        var userId = await ClashKingApiClient.PostDiscordLinksAsync(playerTag);

        if (userId is null)
            await FollowupAsync("couldn't find a linked user");

        var user = Context.Guild.GetUser((ulong)userId!);

        if (user is null)
            await FollowupAsync("couldn't find a linked user");

        var linkModel = new DiscordLink { DiscordId = (ulong)userId!, PlayerTag = playerTag };
        BotDb.AddOrUpdateDiscordLink(linkModel);
        BotDb.SaveChanges();
        await FollowupAsync("done");
    }

}

