using Discord;
using Discord.WebSocket;

namespace ZenBotCS.Extensions
{
    public static class SocketSlashCommandDataExtensions
    {
        public static string GetParamLogString(this SocketSlashCommandData data)
        {
            return string.Join(", ", GetParamLogString(data.Options));
        }

        private static List<string> GetParamLogString(IReadOnlyCollection<SocketSlashCommandDataOption> options)
        {

            if (options == null || options.Count == 0)
                return [];

            var paramStrings = new List<string>();
            foreach (var option in options)
            {
                if (option.Type == ApplicationCommandOptionType.SubCommand || option.Type == ApplicationCommandOptionType.SubCommandGroup)
                    paramStrings.AddRange(GetParamLogString(option.Options));
                else
                    paramStrings.Add($"[{option.Name}: {option.Value}]");
            }
            return paramStrings;
        }

        public static string GetFullNameLogString(this SocketSlashCommandData data)
        {
            var name = data.Name;
            name += GetFullNameLogString(data.Options);
            return name;
        }


        private static string GetFullNameLogString(IReadOnlyCollection<SocketSlashCommandDataOption> options)
        {
            var subCommands = string.Empty;
            // If the data has sub-commands or sub-command groups, add their names to the full name
            if (options != null)
            {
                foreach (var option in options)
                {
                    if (option.Type == ApplicationCommandOptionType.SubCommand)
                    {
                        subCommands += $".{option.Name}";
                    }
                    else if (option.Type == ApplicationCommandOptionType.SubCommandGroup)
                    {
                        subCommands += $".{option.Name}";
                        subCommands += $"{GetFullNameLogString(option.Options)}";
                    }
                }
            }

            return subCommands;
        }

    }
}
