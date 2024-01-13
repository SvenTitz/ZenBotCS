using Discord;
using Discord.Interactions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZenBotCS.Handler
{
    internal class BooleanAutocompleteHandler : AutocompleteHandler
    {
        public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
        {
            IEnumerable<AutocompleteResult> suggestions = new List<AutocompleteResult>()
            {
                new AutocompleteResult("True", true),
                new AutocompleteResult("False", false)
            };

            return Task.FromResult(AutocompletionResult.FromSuccess(suggestions.Take(25)));
        }
    }
}
