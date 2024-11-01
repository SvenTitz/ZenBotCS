using CocApi.Cache;
using Discord;
using Discord.Interactions;
using ZenBotCS.Extensions;
using ZenBotCS.Helper;

namespace ZenBotCS.Handler;

public class PlayerTagAutocompleteHandler(PlayersClient _playersClient, EmbedHelper _embedHelper) : AutocompleteHandler
{

    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        var players = await _playersClient.GetCachedPlayersAsync();

        if (players is null || players.Count == 0)
        {
            return AutocompletionResult.FromSuccess(Array.Empty<AutocompleteResult>());
        }

        IEnumerable<AutocompleteResult> suggestions = players
            .Where(p => p.Name.Contains(autocompleteInteraction.Data.Current.Value.ToString() ?? string.Empty, StringComparison.InvariantCultureIgnoreCase))
            .Select(p => new AutocompleteResult($"{p.Name}{_embedHelper.ToSuperscript(p.TownHallLevel)} ({p.Tag})", p.Tag));

        return AutocompletionResult.FromSuccess(suggestions.Take(25));
    }
}
