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

    public static OldRodEncounterSlot? SourceSlot(FishingEncounterOrigin? origin)
    {
        if (origin is null || origin.RulesVersion != Version || !Enum.IsDefined(origin.Rod)) return null;
        var tables = origin.Rod == FishingRod.Super ? SuperRodEncounterRules.Tables : origin.Rod == FishingRod.Good ? GoodRodEncounterRules.Tables : SevenIslandOldRodEncounterRules.Tables;
        var table = tables.SingleOrDefault(t => t.Map == origin.Map);
        var slot = table?.Slots.SingleOrDefault(s => s.Index == origin.SlotIndex);
        return slot is not null && origin.Species == slot.Species && origin.Level >= slot.MinimumLevel &&
            origin.Level <= slot.MaximumLevel ? slot : null;
    }

    public static bool Matches(FishingEncounterOrigin? origin, PokemonInstance? pokemon) =>
        pokemon is not null && SourceSlot(origin) is not null && pokemon.Species == origin!.Species && pokemon.Level == origin.Level;

    public static bool SourceInitialInstance(GamePokemonCatalog? owned, FishingEncounterOrigin? origin, PokemonInstance? pokemon)
    {
        if (owned is null || pokemon is null || !owned.Catalog.Species.ContainsKey(pokemon.Species) || !Matches(origin, pokemon) ||
            SourcePokemonValidator.ValidatePermanent(owned.Mechanics, pokemon) is not null) return false;
        var source = owned.Mechanics.Catalog.Species[pokemon.Species];
        return pokemon.Evs == SixStats.Zero && pokemon.CurrentHp == pokemon.Stats.MaxHp && pokemon.Friendship == source.Friendship &&
            pokemon.Experience == GrowthRules.ExperienceAtLevel(source.GrowthRate, pokemon.Level) &&
            pokemon.Moves.SequenceEqual(PokemonMoveRules.InitialMovesCore(owned.Mechanics.Catalog, source, pokemon.Level));
    }

    /// <summary>Original ordinary rod item pairs. The caller always consumes the source 0–99 draw, including NONE/NONE.</summary>
    public static string HeldItem(FishingEncounterOrigin origin, int roll)
    {
        if (SourceSlot(origin) is null || roll is < 0 or >= 100) throw new ArgumentException("fishing_held_source");
        return origin.Rod == FishingRod.Super && origin.Species.Value == "pret:SPECIES_SHELLDER"
            ? roll < 45 ? "ITEM_NONE" : roll < 95 ? "ITEM_PEARL" : "ITEM_BIG_PEARL"
            : "ITEM_NONE";
    }
}
