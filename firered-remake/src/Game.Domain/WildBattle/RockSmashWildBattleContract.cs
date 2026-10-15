using Game.Domain.Encounters;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.World;

namespace Game.Domain.WildBattle;

public sealed record RockSmashWildOrigin(MapId Map, int SourceSlot)
{
    public string RulesVersion { get; init; } = RockSmashWildBattleContract.Version;
}

/// <summary>Source-table origin and ordinary creation over the shared owned catalog. The 139 exact source
/// initial sets include the previously inactive high-level Geodude/Graveler rock rows.</summary>
public static class RockSmashWildBattleContract
{
    public const string Version = "firered-rock-smash-wild-entry-v1";
    public static bool Matches(RockSmashWildOrigin? origin, PokemonInstance? pokemon) =>
        FieldRockSmashCatalog.LoadedSource is { } catalog && Matches(catalog, origin, pokemon);
    public static bool SourceInitialInstance(GamePokemonCatalog? owned, RockSmashWildOrigin? origin, PokemonInstance? pokemon) =>
        FieldRockSmashCatalog.LoadedSource is { } catalog && SourceInitialInstance(owned, catalog, origin, pokemon);
    public static string HeldItem(RockSmashWildOrigin origin, int roll) => HeldItem(
        FieldRockSmashCatalog.LoadedSource ?? throw new InvalidOperationException("rock_smash_source_not_loaded"), origin, roll);
    public static WaterEncounterSlot? SourceSlot(FieldRockSmashCatalog catalog, RockSmashWildOrigin? origin)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return origin is null || origin.RulesVersion != Version || origin.SourceSlot is < 0 or >= 5 ? null :
            catalog.EncountersFor(origin.Map)?.Slots[origin.SourceSlot];
    }
    public static bool Matches(FieldRockSmashCatalog catalog, RockSmashWildOrigin? origin, PokemonInstance? pokemon) =>
        pokemon is not null && SourceSlot(catalog, origin) is { } slot && pokemon.Species == slot.Species &&
        pokemon.Level >= slot.MinimumLevel && pokemon.Level <= slot.MaximumLevel;

    public static Route1WildPokemonResult Generate(GamePokemonCatalog owned, FieldRockSmashCatalog catalog,
        RockSmashWildOrigin origin, byte level, RngState rng, uint trainerId)
    {
        if (owned is null || SourceSlot(catalog, origin) is not { } slot || level < slot.MinimumLevel || level > slot.MaximumLevel ||
            !owned.Catalog.Species.TryGetValue(slot.Species, out var species) || rng.AlgorithmId != Gen3Rng.AlgorithmId)
            throw new ArgumentException("rock_smash_wild_generation");
        var moves = catalog.Wild.InitialMoves.Single(row => row.Species == slot.Species && row.Level == level).Moves;
        if (!PokemonMoveRules.InitialMovesCore(owned.Catalog, species, level).Select(move => move.Move).SequenceEqual(moves))
            throw new ArgumentException("rock_smash_initial_move_source");
        var made = Route1WildPokemonFactory.GenerateOrdinary(owned.Catalog, slot.Species, level, rng, trainerId);
        if (!SourceInitialInstance(owned, catalog, origin, made.Pokemon)) throw new InvalidOperationException("rock_smash_wild_identity");
        return made;
    }

    public static bool SourceInitialInstance(GamePokemonCatalog? owned, FieldRockSmashCatalog catalog,
        RockSmashWildOrigin? origin, PokemonInstance? pokemon)
    {
        if (owned is null || pokemon is null || !Matches(catalog, origin, pokemon) ||
            !owned!.Catalog.Species.TryGetValue(pokemon.Species, out var species) ||
            SourcePokemonValidator.ValidatePermanent(owned.Mechanics, pokemon) is not null) return false;
        var source = catalog.Wild.Species.Single(row => row.Species == pokemon.Species);
        var initial = catalog.Wild.InitialMoves.Single(row => row.Species == pokemon.Species && row.Level == pokemon.Level).Moves;
        return pokemon.SourceSpeciesId == source.SourceSpeciesId &&
            (pokemon.AbilityId == source.Ability1 || pokemon.AbilityId == source.Ability2 && source.Ability2 != "ABILITY_NONE") &&
            pokemon.NpcTradeOrigin is null && pokemon.ContestConditions is null && pokemon.StatCalculationEvs is null &&
            pokemon.EggStatCalculationIvs is null && pokemon.Evs == SixStats.Zero && pokemon.CurrentHp == pokemon.Stats.MaxHp &&
            pokemon.Friendship == species.Friendship && pokemon.Experience == GrowthRules.ExperienceAtLevel(species.GrowthRate, pokemon.Level) &&
            pokemon.Moves.Select(move => move.Move).SequenceEqual(initial) &&
            pokemon.Moves.All(move => move.PpUps == 0 && move.CurrentPp == move.MaximumPp && move.MaximumPp == owned.Catalog.Moves[move.Move].Pp);
    }

    public static string HeldItem(FieldRockSmashCatalog catalog, RockSmashWildOrigin origin, int roll)
    {
        if (SourceSlot(catalog, origin) is not { } slot || roll is < 0 or >= 100) throw new ArgumentException("rock_smash_held_source");
        var row = catalog.Wild.Species.Single(row => row.Species == slot.Species);
        if (row.ItemCommon == row.ItemRare && row.ItemCommon != "ITEM_NONE") return row.ItemCommon;
        return roll < 45 ? "ITEM_NONE" : roll < 95 ? row.ItemCommon : row.ItemRare;
    }
    public static Route1WildHeldItemResult AssignHeldItem(FieldRockSmashCatalog catalog, RockSmashWildOrigin origin, RngState rng)
    {
        if (SourceSlot(catalog, origin) is null) throw new ArgumentException("rock_smash_held_origin");
        // Source battle setup draws even for two ITEM_NONE entries; do not elide this draw.
        var draw = Gen3Rng.Next(rng, "wild-battle.held-item", 100);
        return new(HeldItem(catalog, origin, draw.Trace.Result), draw.State, [draw.Trace]);
    }
}
