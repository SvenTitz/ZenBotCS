using CocApi.Cache.Services;
using Discord.Interactions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZenBotCS.Handler;
using ZenBotCS.Services;

namespace ZenBotCS.Modules
{
    [Group("cwl", "Commands related to cwl")]
    public class CwlModule : InteractionModuleBase<SocketInteractionContext>
    {
        public required CwlService CwlService { get; set; }

        [SlashCommand("data", "Get data for the current cwl in a spreadsheet")]
        public async Task Add(
            [Summary("ClanTag"), Autocomplete(typeof(ClanTagAutocompleteHandler))] string clantag,
            [Summary("SpreadsheetId")] string? spreadsheetId = null)
        {
            await DeferAsync();
            var embed = await CwlService.Data(clantag, spreadsheetId);
            await FollowupAsync(embed: embed);
        }
    }
}
