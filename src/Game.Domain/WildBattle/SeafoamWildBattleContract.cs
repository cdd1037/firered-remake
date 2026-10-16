using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.WildBattle;

public enum SeafoamEncounterKind { Land, Surf, OldRod, GoodRod, SuperRod, Articuno }
public sealed record SeafoamWildOrigin(MapId Map, SeafoamEncounterKind Kind, int Slot = 0)
{
    public string RulesVersion { get; init; } = SeafoamWildBattleContract.Version;
}
public sealed record SeafoamSourceSlot(SpeciesId Species, byte MinimumLevel, byte MaximumLevel);

/// <summary>Exact original slot or Articuno50 provenance. Field reachability remains the world owner's policy.</summary>
public static class SeafoamWildBattleContract
{
    public const string Version = "firered-seafoam-wild-entry-v1";
    public static SeafoamWildOrigin ArticunoOrigin { get; } = new(new("map.seafoam-islands-b4f"), SeafoamEncounterKind.Articuno);
    public static bool IsRuntimeKind(SeafoamEncounterKind kind) => kind is SeafoamEncounterKind.Land or SeafoamEncounterKind.Surf or
        SeafoamEncounterKind.OldRod or SeafoamEncounterKind.GoodRod or SeafoamEncounterKind.Articuno;
    public static SeafoamSourceSlot? SourceSlot(SeafoamWildOrigin? origin, FishingTaskProfile? fishingTables = null)
    {
        if (origin is null || origin.RulesVersion != Version || origin.Slot < 0 || !IsRuntimeKind(origin.Kind)) return null;
        if (origin.Kind == SeafoamEncounterKind.Articuno)
            return origin == ArticunoOrigin ? new(SeafoamOwnedPokemonCatalog.ArticunoId, 50, 50) : null;
        if (origin.Kind == SeafoamEncounterKind.Land)
        {
            var slot = SeafoamEncounterTables.Land.SingleOrDefault(t => t.Map == origin.Map)?.Slots.SingleOrDefault(s => s.Index == origin.Slot);
            return slot is null ? null : new(slot.Species, slot.MinimumLevel, slot.MaximumLevel);
        }
        if (origin.Kind == SeafoamEncounterKind.Surf)
        {
            var slot = SeafoamEncounterTables.Water.SingleOrDefault(t => t.Map == origin.Map)?.Slots.SingleOrDefault(s => s.Index == origin.Slot);
            return slot is null ? null : new(slot.Species, slot.MinimumLevel, slot.MaximumLevel);
        }
        if (origin.Kind == SeafoamEncounterKind.GoodRod && !SeafoamEncounterTables.Water.Any(t => t.Map == origin.Map))
            return null;
        OldRodEncounterTable? table = origin.Kind == SeafoamEncounterKind.OldRod
            ? SeafoamEncounterTables.OldRod.SingleOrDefault(t => t.Map == origin.Map)
            : fishingTables?.Tables.GetValueOrDefault((FishingRod.Good, origin.Map));
        var rodSlot = table?.Slots.SingleOrDefault(s => s.Index == origin.Slot);
        return rodSlot is null ? null : new(rodSlot.Species, rodSlot.MinimumLevel, rodSlot.MaximumLevel);
    }
    public static bool Matches(SeafoamWildOrigin? origin, PokemonInstance? pokemon, FishingTaskProfile? fishingTables = null) => pokemon is not null &&
        SourceSlot(origin, fishingTables) is { } slot && pokemon.Species == slot.Species &&
        pokemon.Level >= Math.Min(slot.MinimumLevel, slot.MaximumLevel) && pokemon.Level <= Math.Max(slot.MinimumLevel, slot.MaximumLevel);
}
