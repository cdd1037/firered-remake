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
        return PokemonFactory.CreateCore(catalog.Catalog, request, rng, catalog.Data);
    }
}

public static class SourcePokemonValidator
{
    /// <summary>Authenticates the permanent source identity, IV/EV/stat calculation, ability slot and PP.
    /// A move's acquisition history remains outside the instance schema, as in the campaign validator.</summary>
    public static string? ValidatePermanent(SourceMechanicsCatalog catalog, PokemonInstance? pokemon)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return PokemonStateValidator.ValidateInstance(catalog.Catalog, pokemon, catalog.Data);
    }

    /// <summary>Validates the actual source frame, including Transform originals and donor snapshots,
    /// copied-move slots and ability overlay provenance; never replace permanent identity with live data.</summary>
    public static string? ValidateLive(SourceMechanicsCatalog catalog, SourceBattleFrame frame)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return SourceFourActionTurns.ValidateCapability(catalog.Catalog, frame);
    }

    internal static string? ValidateForBattle(PokemonCatalog? catalog, PokemonInstance? pokemon,
        TrainerTowerEncounterRoster? tower = null) =>
        tower is not null && tower.MatchesIdentity(pokemon) && catalog?.MechanicsData is not null
            ? PokemonStateValidator.ValidateTrainerTowerInstance(catalog!, pokemon, tower) :
        ValidateOrdinaryForBattle(catalog, pokemon);

    // Reserve eggs need exact owned metadata. A mechanics catalog alone grants no egg admission.
    internal static string? ValidatePartyMember(PokemonCatalog catalog, WildBattleCalledPartyMember? member,
        GamePokemonCatalog? ownership = null, TrainerTowerEncounterRoster? tower = null)
    {
        if (member?.Pokemon is null) return "pokemon_missing";
        if (!member.IsEgg) return ValidateForBattle(catalog, member.Pokemon, tower);
        if (tower is not null || ownership is null ||
            !ReferenceEquals(ownership.Mechanics.Catalog, catalog)) return "pokemon_egg_catalog";
        if (member.Status != 0 || member.HeldItem != "ITEM_NONE" || member.KnockedOffItem is not null ||
            member.PersistentHeldItemOverride is not null) return "pokemon_egg_battle_metadata";
        return PokemonStateValidator.ValidateEggInstance(ownership.Catalog, member.Pokemon, ownership.Mechanics.Data);
    }

    internal static string? ValidateFrameParties(PokemonCatalog catalog, SourceBattleFrame frame)
    {
        var ownership = frame.Campaign;
        foreach (var party in frame.Parties)
            foreach (var member in party.Members)
                if (ValidatePartyMember(catalog, member, party.Side == BattleSide.Player ? ownership : null,
                    party.Side == BattleSide.Opponent ? frame.TrainerTowerEncounter?.Roster : null) is { } error)
                    return error;
        return null;
    }

    private static string? ValidateOrdinaryForBattle(PokemonCatalog? catalog, PokemonInstance? pokemon) =>
        catalog?.MechanicsData is { } mechanics
            ? PokemonStateValidator.ValidateInstance(catalog, pokemon, mechanics) : PokemonStateValidator.Validate(catalog, pokemon);
}
