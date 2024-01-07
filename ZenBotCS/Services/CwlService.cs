using CocApi.Cache;
using CocApi.Rest.Apis;
using CocApi.Rest.Models;
using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZenBotCS.Models;

namespace ZenBotCS.Services
{
    public class CwlService(ClansClient clansClient, EmbedHelper embedHelper, GspreadService gspreadService)
    {
        private readonly ClansClient _clansClient = clansClient;
        private readonly EmbedHelper _embedHelper = embedHelper;
        private readonly GspreadService _gspreadService = gspreadService;
        private static readonly string[] _cwlDataHeaders = new[] { "Stars", "% Dest", "TH", "+/-", "Defence" };
        private static readonly string[] _cwlEmptyAttack = new[] { "", "", "", "", "-" };
        private static readonly string[] _cwlMissedAttack = new[] { "0", "0", "", "", "-" };
        private static readonly EmbedFieldBuilder _cwlDataInstructionField = new EmbedFieldBuilder()
            .WithName("Instructions:")
            .WithValue("1. Open the spreadsheet above and copy all lines containing player data (everything except the first two).\n" +
                        "2. Open the Family CWL Data Spreadsheet and select the first cell for player data for the respective clan.\n" +
                        "3. Paste values only. This can either be done with Ctrl+Shift+V or Rightclick -> Paste Special -> Value only.\n\n" +
                        "(on some browsers you might need the [Google Docs Offline](https://chrome.google.com/webstore/detail/google-docs-offline/ghbmnnjooekpmoecnnnilnnbdlolhkhi/related) extension to copy/paste from one sheet to another)");

        public async Task<Embed> Data(string clantag, string? spreadsheetId)
        {
            try
            {
                var group = await _clansClient.GetOrFetchLeagueGroupOrDefaultAsync(clantag);
                var clan = group?.Clans.FirstOrDefault(c => c.Tag == clantag);

                if (group is null || clan is null)
                {
                    return _embedHelper.ErrorEmbed("Error", "Clan does not seem to be in an active CWL.");
                }

                var memberModels = await ExtractCwlDataMemberModelsAsync(group!, clan.Tag);

                var data = FormatDataForCwlSpreadsheet(memberModels, clan);

                var url = _gspreadService.WriteCwlData(data, clan.Name, spreadsheetId);

                var urlField = new EmbedFieldBuilder()
                    .WithName("Sheet:")
                    .WithValue(url);

                return new EmbedBuilder()
                            .WithTitle($"__CWL Data {clan.Name}__")
                            .WithColor(Color.DarkPurple)
                            .AddField(urlField)
                            .AddField(_cwlDataInstructionField)
                            .Build();
            }
            catch (Exception ex)
            {
                return _embedHelper.ErrorEmbed("Error", ex.Message);
            }
            
        }

        private object[][] FormatDataForCwlSpreadsheet(List<CwlDataMemberModel> memberModels, ClanWarLeagueClan clan)
        {
            var data = new List<List<object>>();
            var days = new List<object> { "", "" };
            var headers = new List<object> { "Players", "TH" };
            for (int i = 0; i < 7; i++)
            {
                days.AddRange(new[] { $"Day {i+1}", "", "", "", "" });
                headers.AddRange(_cwlDataHeaders);
            }
            data.Add(days);
            data.Add(headers);

            foreach (var member in memberModels)
            {
                var memberRow = new List<object>
                {
                    member.Member.Name,
                    member.Member.TownhallLevel
                };
                for (int i = 0; i < 7; i++)
                {
                    if (member.Attacks[i] is null)
                    {
                        memberRow.AddRange(_cwlEmptyAttack);
                    }
                    else if (member.Attacks[i]!.isMissedAttack)
                    {
                        memberRow.AddRange(_cwlMissedAttack);
                    }
                    else
                    {
                        memberRow.AddRange(new[]
                            {
                                member.Attacks[i]!.Stars.ToString(),
                                member.Attacks[i]!.DestructionPercentage.ToString(),
                                member.Attacks[i]!.DefenderTownHall.ToString(),
                                (member.Attacks[i]!.DefenderTownHall - member.Attacks[i]!.AttackerTownHall).ToString(),
                                "-"
                            });
                    }
                }
                data.Add(memberRow);
            }

            return data.Select(d => d.ToArray()).ToArray();
        }

        private async Task<List<CwlDataMemberModel>> ExtractCwlDataMemberModelsAsync(ClanWarLeagueGroup group, string clantag)
        {
            var allWars = await _clansClient.GetOrFetchLeagueWarsAsync(group);
            var wars = allWars.Where(w => w.Clans.ContainsKey(clantag)).OrderBy(c => c.StartTime).ToList();

            List<CwlDataMemberModel> memberModels = new();
            foreach (var war in wars)
            {
                int index = wars.IndexOf(war);
                var clan = war.Clans[clantag];
                foreach (var member in clan.Members)
                {
                    var model = GetOrAddCwlDataMemberModel(memberModels, member);
                    if (member.Attacks is not null && member.Attacks.Count > 0)
                    {
                        model.Attacks[index] = new CwlDataMemberAttack(member.Attacks[0]);
                    }
                    else
                    {
                        model.Attacks[index] = new CwlDataMemberAttack { isMissedAttack = true };
                    }
                }
            }
            return memberModels;
        }

        private CwlDataMemberModel GetOrAddCwlDataMemberModel(List<CwlDataMemberModel> members, ClanWarMember member)
        {
            var model = members.FirstOrDefault(p => p.Member.Tag == member.Tag);
            if (model is null) 
            {
                model = new CwlDataMemberModel(member);
                members.Add(model);
            }
            return model;
        }

    }
}
