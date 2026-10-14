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
    public static string? ValidateLive(SourceMechanicsCatalog catalog, SourceBattleFrame frame)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return SourceFourActionTurns.ValidateCapability(catalog.Catalog, frame);
    }

    internal static string? ValidateForBattle(PokemonCatalog? catalog, PokemonInstance? pokemon,
        SourceTrainerTowerPartyReceipt? tower = null) =>
        tower is not null && tower.MatchesIdentity(pokemon) && ReferenceEquals(tower.Mechanics.Catalog, catalog)
            ? PokemonStateValidator.ValidateTrainerTowerInstance(catalog!, pokemon, tower) :
        ValidateOrdinaryForBattle(catalog, pokemon);

    // Reserve eggs need exact owned metadata. A mechanics catalog alone grants no egg admission.
    internal static string? ValidatePartyMember(PokemonCatalog catalog, WildBattleCalledPartyMember? member,
        OwnedBattlePokemonCatalog? ownership = null, SourceTrainerTowerPartyReceipt? tower = null)
    {
        if (member?.Pokemon is null) return "pokemon_missing";
        if (!member.IsEgg) return ValidateForBattle(catalog, member.Pokemon, tower);
        if (tower is not null || ownership is null ||
            !ReferenceEquals(OwnedBattlePokemonCatalog.For(ownership.Catalog), ownership) ||
            !ReferenceEquals(ownership.Mechanics.Catalog, catalog)) return "pokemon_egg_catalog";
        if (member.Status != 0 || member.HeldItem != "ITEM_NONE" || member.KnockedOffItem is not null ||
            member.PersistentHeldItemOverride is not null) return "pokemon_egg_battle_metadata";
        return PokemonStateValidator.ValidateEggInstance(ownership.Catalog, member.Pokemon, ownership.Mechanics);
    }

    internal static string? ValidateFrameParties(PokemonCatalog catalog, SourceBattleFrame frame)
    {
        var ownership = OwnedBattlePokemonCatalog.ForMechanics(catalog, frame.SourceCampaignRulesVersion);
        foreach (var party in frame.Parties)
            foreach (var member in party.Members)
                if (ValidatePartyMember(catalog, member, party.Side == BattleSide.Player ? ownership : null,
                    party.Side == BattleSide.Opponent ? frame.TrainerTowerEncounter?.Receipt : null) is { } error)
                    return error;
        return null;
    }

    private static string? ValidateOrdinaryForBattle(PokemonCatalog? catalog, PokemonInstance? pokemon) =>
        SourceMechanicsCatalog.For(catalog) is { } mechanics
            ? ValidatePermanent(mechanics, pokemon) : PokemonStateValidator.Validate(catalog, pokemon);
}
