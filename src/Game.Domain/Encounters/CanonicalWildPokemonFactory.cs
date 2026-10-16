using Game.Domain.Battle;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Encounters;

/// <summary>Ordinary wild creation properties from the loaded canonical species and mechanics.
/// Callers retain responsibility for the exact encounter origin, slot and level admission.</summary>
public static class CanonicalWildPokemonFactory
{
    public static bool IsInitialInstance(GamePokemonCatalog owned, PokemonInstance pokemon)
    {
        if (owned is null || pokemon is null ||
            !owned.Catalog.Species.TryGetValue(pokemon.Species, out var species) ||
            SourcePokemonValidator.ValidatePermanent(owned.Mechanics, pokemon) is not null)
            return false;
        return pokemon.NpcTradeOrigin is null && pokemon.ContestConditions is null && pokemon.StatCalculationEvs is null &&
            pokemon.Evs == SixStats.Zero && pokemon.CurrentHp == pokemon.Stats.MaxHp &&
            pokemon.Friendship == species.Friendship &&
            pokemon.Experience == GrowthRules.ExperienceAtLevel(species.GrowthRate, pokemon.Level) &&
            pokemon.Moves.SequenceEqual(PokemonMoveRules.InitialMovesCore(owned.Catalog, species, pokemon.Level));
    }

    public static string HeldItem(GamePokemonCatalog owned, SpeciesId species, int roll)
    {
        ArgumentNullException.ThrowIfNull(owned);
        if (roll is < 0 or >= 100) throw new ArgumentOutOfRangeException(nameof(roll));
        if (!owned.Catalog.Species.ContainsKey(species) ||
            !owned.Mechanics.SpeciesMechanics.TryGetValue(species, out var source))
            throw new ArgumentException("wild_held_item_species");
        return source.ItemCommon == source.ItemRare ? source.ItemCommon :
            roll < 45 ? "ITEM_NONE" : roll < 95 ? source.ItemCommon : source.ItemRare;
    }

    /// <summary>Ordinary SetWildMonHeldItem always consumes one draw, including NONE/NONE and
    /// identical item pairs. Legendary/event suppression belongs to battle entry.</summary>
    public static EarlyLandWildHeldItemResult AssignHeldItem(GamePokemonCatalog owned, SpeciesId species, RngState rng)
    {
        _ = HeldItem(owned, species, 0);
        var draw = Gen3Rng.Next(rng, "wild-battle.held-item", 100);
        return new(HeldItem(owned, species, draw.Trace.Result), draw.State, [draw.Trace]);
    }
}
