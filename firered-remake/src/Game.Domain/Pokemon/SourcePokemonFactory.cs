using Game.Domain.Randomness;
using Game.Domain.WildBattle;
using Game.Domain.Battle;

namespace Game.Domain.Pokemon;

/// <summary>Mechanics construction uses the real CreateMon implementation without campaign admission.</summary>
public static class SourcePokemonFactory
{
    public static PokemonCreationResult Create(SourceMechanicsCatalog catalog, CreatePokemonRequest request, RngState rng)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return PokemonFactory.CreateCore(catalog.Catalog, request, rng, catalog);
    }
}

public static class SourcePokemonValidator
{
    /// <summary>Authenticates the permanent source identity, IV/EV/stat calculation, ability slot and PP.
    /// A move's acquisition history remains outside the instance schema, as in the campaign validator.</summary>
    public static string? ValidatePermanent(SourceMechanicsCatalog catalog, PokemonInstance? pokemon)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return PokemonStateValidator.ValidateInstance(catalog.Catalog, pokemon, catalog);
    }

    /// <summary>Validates the actual source frame, including Transform originals and donor snapshots,
    /// copied-move slots and ability overlay provenance; never replace permanent identity with live data.</summary>
    public static string? ValidateLive(SourceMechanicsCatalog catalog, WildBattleState state)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (!SourceBattleAdmission.IsCanonicalSourceState(catalog.Catalog, state)) return "source_mechanics_rules_profile";
        return WildBattleRules.IsSourceWild(state) ? WildBattleRules.Validate(catalog.Catalog, state) :
            WildBattleRules.ValidateCalledMoveActionState(catalog.Catalog, state);
    }

    internal static string? ValidateForBattle(PokemonCatalog? catalog, PokemonInstance? pokemon) =>
        SourceMechanicsCatalog.For(catalog) is { } mechanics
            ? ValidatePermanent(mechanics, pokemon) : PokemonStateValidator.Validate(catalog, pokemon);
}
