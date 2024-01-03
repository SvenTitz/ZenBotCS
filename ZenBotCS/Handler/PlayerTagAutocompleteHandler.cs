using CocApi.Cache;
using CocApi.Rest.Models;
using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Formats.Asn1.AsnWriter;

namespace ZenBotCS.Handler
{
    public class PlayerTagAutocompleteHandler : AutocompleteHandler
    {
        private readonly PlayersClient _playersClient;
        public PlayerTagAutocompleteHandler(PlayersClient playersClient) 
        {
            _playersClient = playersClient;
        }

        public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
        {
            var players = await (from i in _playersClient.ScopeFactory.CreateScope().ServiceProvider.GetRequiredService<CacheDbContext>().Players.AsNoTracking()
                   select i.Content).ToListAsync<Player>().ConfigureAwait(continueOnCapturedContext: false);

            if (players is null || !players.Any())
            {
                return AutocompletionResult.FromSuccess(Array.Empty<AutocompleteResult>());
            }

            IEnumerable<AutocompleteResult> suggestions = players
                .Where(p => p.Name.Contains(autocompleteInteraction.Data.Current.Value.ToString() ?? string.Empty, StringComparison.InvariantCultureIgnoreCase))
                .Select(p => new AutocompleteResult($"{p.Name} ({p.Tag})", p.Tag));

            return AutocompletionResult.FromSuccess(suggestions.Take(25));
        }
    }
}
