using Game.Domain.Encounters;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.WildBattle;

/// <summary>Persisted exact encounter receipt. The source slot authenticates rod, map, species and initial level.</summary>
public sealed record FishingEncounterOrigin(FishingRod Rod, MapId Map, int SlotIndex, SpeciesId Species, byte Level)
{
    public string RulesVersion { get; init; } = FishingBattleContract.Version;
}

public static class FishingBattleContract
{
    public const string Version = "firered-fishing-origin-v1";

    public static OldRodEncounterSlot? SourceSlot(FishingEncounterOrigin? origin, FishingTaskProfile? tables = null)
    {
        if (origin is null || origin.RulesVersion != Version || !Enum.IsDefined(origin.Rod)) return null;
        if (tables is not null) return tables.SourceSlot(origin);
        // Compatibility adapters do not supply generated Good Rod definitions.
        if (origin.Rod == FishingRod.Good) return null;
        var legacy = origin.Rod == FishingRod.Super ? SuperRodEncounterRules.TaskProfile : SevenIslandOldRodEncounterRules.TaskProfile;
        return legacy.SourceSlot(origin);
    }

    public static bool Matches(FishingEncounterOrigin? origin, PokemonInstance? pokemon, FishingTaskProfile? tables = null) =>
        pokemon is not null && SourceSlot(origin, tables) is not null && pokemon.Species == origin!.Species && pokemon.Level == origin.Level;

    public static bool SourceInitialInstance(GamePokemonCatalog? owned, FishingEncounterOrigin? origin, PokemonInstance? pokemon, FishingTaskProfile? tables = null)
    {
        if (owned is null || pokemon is null || !owned.Catalog.Species.ContainsKey(pokemon.Species) || !Matches(origin, pokemon, tables) ||
            SourcePokemonValidator.ValidatePermanent(owned.Mechanics, pokemon) is not null) return false;
        var source = owned.Mechanics.Catalog.Species[pokemon.Species];
        return pokemon.Evs == SixStats.Zero && pokemon.CurrentHp == pokemon.Stats.MaxHp && pokemon.Friendship == source.Friendship &&
            pokemon.Experience == GrowthRules.ExperienceAtLevel(source.GrowthRate, pokemon.Level) &&
            pokemon.Moves.SequenceEqual(PokemonMoveRules.InitialMovesCore(owned.Mechanics.Catalog, source, pokemon.Level));
    }

    /// <summary>Original ordinary rod item pairs. The caller always consumes the source 0–99 draw, including NONE/NONE.</summary>
    public static string HeldItem(FishingEncounterOrigin origin, int roll, FishingTaskProfile? tables = null)
    {
        if (tables is null) throw new ArgumentException("fishing_held_item_definitions_required");
        return tables.HeldItem(origin, roll);
    }
}
