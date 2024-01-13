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

        public string FormatAsTable(List<string[]> data, int minColSize)
        {
            var columnCount = data.Max(d => d.Length);

            var longestEntry = data.SelectMany(d => d).Max(d => d.Length);
            
            var columSize = longestEntry > minColSize ? longestEntry : minColSize;

            if(columnCount * columSize > 100) // TODO: proper value
            {
                _logger.LogWarning("Row size too long with value of " + (columnCount * columSize));
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

        public string FormatAsTable(List<string[]> data)
        {
            var colWidths = new List<int>();

            var columnCount = data.Max(d => d.Length);

            for( var i = 0; i < columnCount; i++ )
            {
                var col = data.Where(d => d.Length > i).Select(d => d[i]);
                colWidths.Add(col.Max(d => d.Length));
            }

            return FormatAsTable(data, colWidths);
        }

        public string FormatAsTable(List<string[]> data, IList<int> colWidths)
        {
            if (colWidths.Sum() > 100) // TODO: proper value
            {
                _logger.LogWarning("Row size too long with value of " + colWidths.Sum());
            }

            var builder = new StringBuilder();

            foreach (var row in data)
            {
                foreach (var entry in row)
                {
                    if (entry.StartsWith(':') && entry.EndsWith(":"))
                    {
                        builder.Append(entry);
                        continue;
                    }   
                    builder.Append('`');
                    builder.Append(new string(' ', colWidths[Array.IndexOf(row, entry)] - entry.Length));
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
