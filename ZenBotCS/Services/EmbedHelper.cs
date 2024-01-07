using Discord;
using Discord.Addons.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZenBotCS.Services
{
    public class EmbedHelper
    {
        private readonly ILogger<DiscordClientService> _logger;

        public EmbedHelper(ILogger<DiscordClientService> logger)
        {
            _logger = logger;
        }

        public string FormatAsTable(IEnumerable<IEnumerable<string>> data, int minRowSize)
        {
            var columnCount = data.Max(d => d.Count());

            var longestEntry = data.SelectMany(d => d).Max(d => d.Length);
            
            var columSize = longestEntry > minRowSize ? longestEntry : minRowSize;

            if(columnCount * columSize > 100) // TODO: proper value
            {
                _logger.LogWarning("Row size too long with value of " + columnCount * columSize);
            }

            var builder = new StringBuilder();

            foreach( var row in data) 
            {
                foreach( var entry in row)
                {
                    builder.Append('`');
                    builder.Append(new string(' ', columSize - entry.Length));
                    builder.Append(entry);
                    builder.Append("` ");
                }
                builder.Append('\n');
            }

            return builder.ToString();
        }
         
        public Embed ErrorEmbed(string title, string message)
        {
            return new EmbedBuilder()
                        .WithTitle(title)
                        .WithColor(Color.Red)
                        .WithDescription(message)
                        .Build();
        }

    }
}
