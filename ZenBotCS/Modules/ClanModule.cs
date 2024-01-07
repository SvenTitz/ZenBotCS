using CocApi.Rest.Models;
using Discord.Interactions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZenBotCS.Services;

namespace ZenBotCS.Modules
{
    [Group("clan", "Commands to add, remove or get info about clans.")]
    public class ClanModule : InteractionModuleBase<SocketInteractionContext>
    {
        public required ClanService ClanService { get; set; }

        [RequireUserPermission(Discord.GuildPermission.Administrator)]
        [SlashCommand("add", "Add a Clan to the bot.")]
        public async Task Add(string clantag)
        {
            await DeferAsync();
            var embed = await ClanService.Add(clantag);
            await FollowupAsync(embed: embed);
        }

        [RequireUserPermission(Discord.GuildPermission.Administrator)]
        [SlashCommand("delete", "Delete a Clan from the bot.")]
        public async Task Delete(string clantag)
        {
            await DeferAsync();
            var embed = await ClanService.Delete(clantag);
            await FollowupAsync(embed: embed);
        }

        [SlashCommand("list", "Lists all Clans")]
        public async Task List()
        {
            await DeferAsync();
            var embed = await ClanService.List();
            await FollowupAsync(embed: embed);
        }


    }
}
