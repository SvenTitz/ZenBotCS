using Discord.Interactions;
using ZenBotCS.Handler;
using ZenBotCS.Services.SlashCommands;

namespace ZenBotCS.Modules
{
    [Group("cwl", "Commands related to cwl")]
    public class CwlModule : InteractionModuleBase<SocketInteractionContext>
    {
        public required CwlService CwlService { get; set; }

        [SlashCommand("data", "Get data for the current cwl in a spreadsheet")]
        public async Task Data(
            [Summary("ClanTag"), Autocomplete(typeof(ClanTagAutocompleteHandler))] string clantag,
            [Summary("SpreadsheetId")] string? spreadsheetId = null)
        {
            await DeferAsync();
            var embed = await CwlService.Data(clantag, spreadsheetId);
            await FollowupAsync(embed: embed);
        }
    }
}
