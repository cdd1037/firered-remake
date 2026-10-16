using Game.Domain.Items;

namespace Game.Domain.Pokemon;

/// <summary>DaisyMassageServices / AdjustFriendship(MASSAGE). The caller owns selection,
/// script eligibility and the unconditional massage cooldown reset.</summary>
public static class DaisyGroomingRules
{
    public static PartyPokemon Apply(PartyPokemon pokemon, string currentMapSection)
    {
        ArgumentNullException.ThrowIfNull(pokemon);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentMapSection);
        // AdjustFriendship excludes eggs, but does not test HP. Egg friendship is hatch cycles.
        if (pokemon.IsEgg) return pokemon;
        // MASSAGE is +3 in all three friendship bands. Reuse the shared positive-delta
        // order: held-item scaling, ball bonus, met-section bonus, then saturation.
        int friendship = SourceItemFriendshipRules.Apply(pokemon.Pokemon.Friendship, 3,
            pokemon.HeldItem, new(pokemon.Ball, pokemon.MetLocation, currentMapSection));
        return friendship == pokemon.Pokemon.Friendship ? pokemon :
            pokemon with { Pokemon = pokemon.Pokemon with { Friendship = friendship } };
    }
}
