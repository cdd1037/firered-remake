using Game.Domain.Pokemon;

namespace Game.Domain.Encounters;

/// <summary>Exact ordinary Surf candidate bound to its loaded table and canonical catalog.
/// Special encounters continue to use their typed origin contracts.</summary>
public sealed class OrdinaryWaterBattleBinding
{
    private readonly PokemonCatalog _pokemon;
    private readonly WaterEncounterCandidate _candidate;

    internal OrdinaryWaterBattleBinding(PokemonCatalog pokemon, WaterEncounterCandidate candidate)
    {
        _pokemon = pokemon;
        _candidate = candidate;
    }

    public bool Matches(PokemonInstance pokemon) => pokemon is not null &&
        pokemon.Species == _candidate.Species && pokemon.Level == _candidate.Level &&
        pokemon.SourceSpeciesId == _pokemon.Species[_candidate.Species].SourceSpeciesId;

    public bool SourceInitialInstance(GamePokemonCatalog owned, PokemonInstance pokemon) =>
        owned is not null && ReferenceEquals(owned.Catalog, _pokemon) && Matches(pokemon) &&
        CanonicalWildPokemonFactory.IsInitialInstance(owned, pokemon);

    public string HeldItem(GamePokemonCatalog owned, int roll)
    {
        if (owned is null || !ReferenceEquals(owned.Catalog, _pokemon))
            throw new ArgumentException("ordinary_water_catalog");
        return CanonicalWildPokemonFactory.HeldItem(owned, _candidate.Species, roll);
    }
}
