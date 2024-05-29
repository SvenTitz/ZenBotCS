using Discord;
using Discord.Addons.Hosting;
using Microsoft.Extensions.Logging;
using System.Text;
using ZenBotCS.Models.Enums;

namespace ZenBotCS.Helper
{
    public class EmbedHelper
    {
        private readonly ILogger<DiscordClientService> _logger;

        private static readonly Dictionary<char, char> SuperscriptDigits = new Dictionary<char, char>
        {
            {'0', '⁰'},
            {'1', '¹'},
            {'2', '²'},
            {'3', '³'},
            {'4', '⁴'},
            {'5', '⁵'},
            {'6', '⁶'},
            {'7', '⁷'},
            {'8', '⁸'},
            {'9', '⁹'}
        };

        public EmbedHelper(ILogger<DiscordClientService> logger)
        {
            _logger = logger;
        }



        public string FormatAsTableOld(List<string[]> data, int minColSize)
        {
            var columnCount = data.Max(d => d.Length);

            var longestEntry = data.SelectMany(d => d).Max(d => d.Length);

            var columSize = longestEntry > minColSize ? longestEntry : minColSize;

            if (columnCount * columSize > 100) // TODO: proper value
            {
                _logger.LogWarning("Row size too long with value of " + columnCount * columSize);
            }

            var builder = new StringBuilder();

            foreach (var row in data)
            {
                foreach (var entry in row)
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

        public string FormatAsTableOld(List<string[]> data)
        {
            var colWidths = new List<int>();

            var columnCount = data.Max(d => d.Length);

            for (var i = 0; i < columnCount; i++)
            {
                var col = data.Where(d => d.Length > i).Select(d => d[i]);
                colWidths.Add(col.Max(d => d.Length));
            }

            return FormatAsTableOld(data, colWidths);
        }

        public string FormatAsTableOld(List<string[]> data, IList<int> colWidths)
        {
            if (colWidths.Sum() > 100) // TODO: proper value
            {
                _logger.LogWarning("Row size too long with value of " + colWidths.Sum());
            }

            var builder = new StringBuilder();

            foreach (var row in data)
            {
                for (var i = 0; i < row.Length; i++)
                {
                    var entry = row[i];
                    if (entry.StartsWith(':') && entry.EndsWith(":"))
                    {
                        builder.Append(entry);
                        continue;
                    }
                    builder.Append('`');
                    builder.Append(new string(' ', colWidths[i] - entry.Length));
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

        public string FormatAsTable(List<string[]> data, TextAlign headerAlign = TextAlign.Left, TextAlign dataAlign = TextAlign.Left)
        {
            var colWidths = new List<int>();

            var columnCount = data.Max(d => d.Length);

            for (var i = 0; i < columnCount; i++)
            {
                var col = data.Where(d => d.Length > i).Select(d => d[i]);
                colWidths.Add(col.Max(d => d.Length));
            }

            if (colWidths.Sum() > 100) // TODO: proper value
            {
                _logger.LogWarning("Row size too long with value of " + colWidths.Sum());
            }

            var builder = new StringBuilder();

            foreach (var row in data)
            {
                for (var i = 0; i < data[0].Length; i++)
                {
                    if (row == data.First())
                    {
                        builder.Append(FillTextWithSpace(row[i], colWidths[i], headerAlign));
                    }
                    else
                    {
                        builder.Append(FillTextWithSpace(row[i], colWidths[i], dataAlign));
                    }
                    builder.Append("  ");
                }
                builder.Append('\n');
            }

            return builder.ToString();

        }

        private string FillTextWithSpace(string text, int width, TextAlign align)
        {
            var widthDif = width - text.Length;
            if (align == TextAlign.Left)
            {

            }
            switch (align)
            {
                case TextAlign.Left:
                    return string.Concat(text, new string(' ', widthDif));
                case TextAlign.Right:
                    return string.Concat(new string(' ', widthDif), text);
                default:
                    _logger.LogError("Missing Text Aling in FillTextWithSpace");
                    return string.Concat(text, new string(' ', widthDif));
            }
        }

        public string ToSuperscript(int number)
        {
            string numberString = number.ToString();
            char[] superscriptChars = new char[numberString.Length];

            for (int i = 0; i < numberString.Length; i++)
            {
                superscriptChars[i] = SuperscriptDigits[numberString[i]];
            }

            return new string(superscriptChars);
        }

    }
}
