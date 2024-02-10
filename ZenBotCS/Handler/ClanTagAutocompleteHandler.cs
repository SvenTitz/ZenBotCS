using Discord;
using Discord.Interactions;
using ZenBotCS.Clients;

namespace ZenBotCS.Handler
{
    public class ClanTagAutocompleteHandler : AutocompleteHandler
    {
        private readonly CustomClansClient _clansClient;
        public ClanTagAutocompleteHandler(CustomClansClient clansClient)
        {
            _clansClient = clansClient;
        }

        public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
        {
            var clans = await _clansClient.GetCachedClansAsync();

            if (clans is null || clans.Count == 0)
            {
                return AutocompletionResult.FromSuccess(Array.Empty<AutocompleteResult>());
            }

            IEnumerable<AutocompleteResult> suggestions = clans
                .Where(c => c is not null && c.Name.Contains(autocompleteInteraction.Data.Current.Value.ToString() ?? string.Empty, StringComparison.InvariantCultureIgnoreCase))
                .Select(c => new AutocompleteResult($"{c.Name} ({c.Tag})", c.Tag));

            return AutocompletionResult.FromSuccess(suggestions.Take(25));
        }
    }
}
