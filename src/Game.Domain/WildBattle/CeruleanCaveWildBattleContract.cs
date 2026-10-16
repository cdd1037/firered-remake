using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.WildBattle;

public enum CeruleanCaveEncounterKind { Land, Surf, OldRod, Mewtwo }
public sealed record CeruleanCaveWildOrigin(MapId Map, CeruleanCaveEncounterKind Kind, int Slot)
{
    public string RulesVersion { get; init; } = CeruleanCaveWildBattleContract.Version;
}
public sealed record CeruleanCaveSourceSlot(SpeciesId Species, byte MinimumLevel, byte MaximumLevel);

/// <summary>Exact source-entry provenance. Map access, encounter checks and static-story gates belong to Application.</summary>
public static class CeruleanCaveWildBattleContract
{
    public const string Version = "firered-cerulean-cave-wild-entry-v1";
    public static bool IsRuntimeKind(CeruleanCaveEncounterKind kind) => kind is
        CeruleanCaveEncounterKind.Land or CeruleanCaveEncounterKind.Surf or
        CeruleanCaveEncounterKind.OldRod or CeruleanCaveEncounterKind.Mewtwo;
    public static CeruleanCaveWildOrigin MewtwoOrigin { get; } =
        new(new("map.cerulean-cave-b1f"), CeruleanCaveEncounterKind.Mewtwo, 0);

    public static CeruleanCaveSourceSlot? SourceSlot(CeruleanCaveWildOrigin? origin)
    {
        if (origin is null || origin.RulesVersion != Version || origin.Slot < 0 || !IsRuntimeKind(origin.Kind)) return null;
        if (origin.Kind == CeruleanCaveEncounterKind.Mewtwo)
            return origin == MewtwoOrigin ? new(new("pret:SPECIES_MEWTWO"), 70, 70) : null;
        if (origin.Kind == CeruleanCaveEncounterKind.Land)
        {
            var table = CeruleanCaveEncounterTables.Land.SingleOrDefault(t => t.Map == origin.Map);
            if (table is null || origin.Slot >= table.Slots.Length) return null;
            var slot = table.Slots[origin.Slot];
            return new(slot.Species, slot.MinimumLevel, slot.MaximumLevel);
        }
        if (origin.Kind == CeruleanCaveEncounterKind.Surf)
        {
            var table = CeruleanCaveEncounterTables.Water.SingleOrDefault(t => t.Map == origin.Map);
            if (table is null || origin.Slot >= table.Slots.Length) return null;
            var slot = table.Slots[origin.Slot];
            return new(slot.Species, slot.MinimumLevel, slot.MaximumLevel);
        }
        var fishing = CeruleanCaveEncounterTables.OldRod.SingleOrDefault(t => t.Map == origin.Map);
        if (fishing is null || origin.Slot >= fishing.Slots.Length) return null;
        var fish = fishing.Slots[origin.Slot];
        return new(fish.Species, fish.MinimumLevel, fish.MaximumLevel);
    }

    public static bool Matches(CeruleanCaveWildOrigin? origin, PokemonInstance? pokemon) =>
        pokemon is not null && SourceSlot(origin) is { } slot && slot.Species == pokemon.Species &&
        pokemon.Level >= slot.MinimumLevel && pokemon.Level <= slot.MaximumLevel;
}
