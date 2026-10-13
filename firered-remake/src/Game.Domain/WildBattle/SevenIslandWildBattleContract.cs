using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.WildBattle;

public enum SevenIslandEncounterKind { Land, Surf, OldRod }
public sealed record SevenIslandWildOrigin(MapId Map, SevenIslandEncounterKind Kind, int Slot)
{
    public string RulesVersion { get; init; } = SevenIslandWildBattleContract.Version;
}
public sealed record SevenIslandSourceSlot(SpeciesId Species, byte MinimumLevel, byte MaximumLevel);

/// <summary>Exact source-entry provenance. Map access, encounter checks and static-story gates belong to Application.</summary>
public static class SevenIslandWildBattleContract
{
    public const string Version = "firered-seven-island-wild-entry-v1";
    public static bool IsRuntimeKind(SevenIslandEncounterKind kind) => kind is
        SevenIslandEncounterKind.Land or SevenIslandEncounterKind.Surf or
        SevenIslandEncounterKind.OldRod;
    public static SevenIslandSourceSlot? SourceSlot(SevenIslandWildOrigin? origin)
    {
        if (origin is null || origin.RulesVersion != Version || origin.Slot < 0 || !IsRuntimeKind(origin.Kind)) return null;
        if (origin.Kind == SevenIslandEncounterKind.Land)
        {
            var table = SevenIslandEncounterTables.Land.SingleOrDefault(t => t.Map == origin.Map);
            if (table is null || origin.Slot >= table.Slots.Length) return null;
            var slot = table.Slots[origin.Slot];
            return new(slot.Species, slot.MinimumLevel, slot.MaximumLevel);
        }
        if (origin.Kind == SevenIslandEncounterKind.Surf)
        {
            var table = SevenIslandEncounterTables.Water.SingleOrDefault(t => t.Map == origin.Map);
            if (table is null || origin.Slot >= table.Slots.Length) return null;
            var slot = table.Slots[origin.Slot];
            return new(slot.Species, slot.MinimumLevel, slot.MaximumLevel);
        }
        var fishing = SevenIslandEncounterTables.OldRod.SingleOrDefault(t => t.Map == origin.Map);
        if (fishing is null || origin.Slot >= fishing.Slots.Length) return null;
        var fish = fishing.Slots[origin.Slot];
        return new(fish.Species, fish.MinimumLevel, fish.MaximumLevel);
    }

    public static bool Matches(SevenIslandWildOrigin? origin, PokemonInstance? pokemon) =>
        pokemon is not null && SourceSlot(origin) is { } slot && slot.Species == pokemon.Species &&
        pokemon.Level >= slot.MinimumLevel && pokemon.Level <= slot.MaximumLevel &&
        (pokemon.Species.Value != "pret:SPECIES_UNOWN" || UnownFormRules.Form(pokemon.Personality) == UnownFormRules.SourceSlotForm(origin!.Map, origin.Slot));
}
