using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZenBotCS.Services
{
    public class ClashKingApiService
    {
        private readonly ClashKingApiClient _client;
        public ClashKingApiService(ClashKingApiClient client)
        {
            _client = client;
        }

        public async Task<IEnumerable<string>> GetUsersLinkedAccountsAsync(SocketUser user)
        {
            return Array.Empty<string>();
        }


    }
}
