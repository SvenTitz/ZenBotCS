using Discord;
using Discord.Interactions;

namespace ZenBotCS.Handler;

public class TownHallAutocompleteHandler : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {

        IEnumerable<AutocompleteResult> suggestions =
        [
            new AutocompleteResult($"TH 9", 9),
            new AutocompleteResult($"TH 10", 10),
            new AutocompleteResult($"TH 11", 11),
            new AutocompleteResult($"TH 12", 12),
            new AutocompleteResult($"TH 13", 13),
            new AutocompleteResult($"TH 14", 14),
            new AutocompleteResult($"TH 15", 15),
            new AutocompleteResult($"TH 16", 16),
        ];

        return AutocompletionResult.FromSuccess(suggestions.Take(25));
    }

}