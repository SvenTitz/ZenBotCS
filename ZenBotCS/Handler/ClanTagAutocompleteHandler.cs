using CocApi.Cache;
using CocApi.Rest.Models;
using Discord.Interactions;
using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace ZenBotCS.Handler
{
    public class ClanTagAutocompleteHandler : AutocompleteHandler
    {
        private readonly ClansClient _clansClient;
        public ClanTagAutocompleteHandler(ClansClient clansClient)
        {
            _clansClient = clansClient;
        }

        public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
        {
            var clans = await (from i in _clansClient.ScopeFactory.CreateScope().ServiceProvider.GetRequiredService<CacheDbContext>().Clans.AsNoTracking()
                                where i.Download
                                select i.Content).ToListAsync<Clan>().ConfigureAwait(continueOnCapturedContext: false);

            if (clans is null || !clans.Any())
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
