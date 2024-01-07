using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZenBotCS.Services
{
    public class PlayerService(EmbedHelper embedHelper, ClashKingApiClient ckApiClient)
    {
        private readonly EmbedHelper _embedHelper = embedHelper;
        private readonly ClashKingApiClient _ckApiClient = ckApiClient;

        public async Task<Embed> StatsMisses(string? playerTag, SocketUser? user)
        {
            if ( playerTag is null && user is null)
            {
                return _embedHelper.ErrorEmbed("Error", "You need to provide either a User or Playertag.");
            }

            var playerTags = new List<string>();
            if(playerTag is not null)
            {
                playerTags.Add(playerTag);
            }
            if(user is not null)
            {
                //temp
                return _embedHelper.ErrorEmbed("Error", "Can not access discord-player links at this time. Please try again with a playertag.");
            }

            try
            {
                await _ckApiClient.GetClanWarHistory("#2G2LJUYGV");
            }
            catch (Exception ex)
            {
                return _embedHelper.ErrorEmbed("Error", ex.Message);
            }
            


            return _embedHelper.ErrorEmbed("Error", "Not done yet.");
        }
    }
}
