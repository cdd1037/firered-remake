using System.Collections.Immutable;
using Game.Domain.Pokemon;

namespace Game.Content;

/// <summary>The two missing Victory Road opponents projected from the already authenticated canonical source.
/// No new permanent species, source metadata, move recipes or reference downloads are admitted.</summary>
public static class PretVictoryRoadOpponents
{
    public static VictoryRoadOpponentPokemonCatalog Load(CinnabarOpponentPokemonCatalog predecessor)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        return VictoryRoadOpponentPokemonCatalog.Authenticate(predecessor,
            VictoryRoadOpponentPokemonCatalog.AdditionalSpecies.Select(id => predecessor.Mechanics.Catalog.Species[id]).ToImmutableArray());
    }
}
