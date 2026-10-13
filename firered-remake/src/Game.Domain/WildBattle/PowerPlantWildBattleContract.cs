using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.WildBattle;

public enum PowerPlantEncounterKind { Land, Zapdos, Electrode1, Electrode2 }
public sealed record PowerPlantWildOrigin(MapId Map, PowerPlantEncounterKind Kind, int Slot = 0)
{
    public string RulesVersion { get; init; } = PowerPlantWildBattleContract.Version;
}
public sealed record PowerPlantSourceSlot(SpeciesId Species, byte MinimumLevel, byte MaximumLevel);

/// <summary>Exact source-slot or setwildbattle provenance. Script access and object outcomes remain application policy.</summary>
public static class PowerPlantWildBattleContract
{
    public const string Version = "firered-power-plant-wild-entry-v1";
    public static MapId Map { get; } = new("map.power-plant");
    public static bool IsRuntimeKind(PowerPlantEncounterKind kind) => kind is PowerPlantEncounterKind.Land or
        PowerPlantEncounterKind.Zapdos or PowerPlantEncounterKind.Electrode1 or PowerPlantEncounterKind.Electrode2;
    public static PowerPlantWildOrigin Origin(PowerPlantEncounterKind kind) => kind is
        PowerPlantEncounterKind.Zapdos or PowerPlantEncounterKind.Electrode1 or PowerPlantEncounterKind.Electrode2
            ? new(Map, kind) : throw new ArgumentOutOfRangeException(nameof(kind));
    public static SpeciesId Species(PowerPlantEncounterKind kind) => kind switch {
        PowerPlantEncounterKind.Zapdos => new("pret:SPECIES_ZAPDOS"),
        PowerPlantEncounterKind.Electrode1 or PowerPlantEncounterKind.Electrode2 => new("pret:SPECIES_ELECTRODE"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)) };
    public static int Level(PowerPlantEncounterKind kind) => kind switch {
        PowerPlantEncounterKind.Zapdos => 50,
        PowerPlantEncounterKind.Electrode1 or PowerPlantEncounterKind.Electrode2 => 34,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)) };

    public static PowerPlantSourceSlot? SourceSlot(PowerPlantWildOrigin? origin)
    {
        if (origin is null || origin.RulesVersion != Version || origin.Map != Map ||
            origin.Slot < 0 || !IsRuntimeKind(origin.Kind)) return null;
        if (origin.Kind != PowerPlantEncounterKind.Land)
            return origin == Origin(origin.Kind) ? new(Species(origin.Kind), (byte)Level(origin.Kind), (byte)Level(origin.Kind)) : null;
        var table = PowerPlantEncounterTables.Land;
        if (origin.Slot >= table.Slots.Length) return null;
        var slot = table.Slots[origin.Slot];
        return new(slot.Species, slot.MinimumLevel, slot.MaximumLevel);
    }

    public static bool Matches(PowerPlantWildOrigin? origin, PokemonInstance? pokemon) =>
        pokemon is not null && SourceSlot(origin) is { } slot && pokemon.Species == slot.Species &&
        pokemon.Level >= slot.MinimumLevel && pokemon.Level <= slot.MaximumLevel;
}
