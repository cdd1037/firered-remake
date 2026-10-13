using Game.Domain.Ids;
using Game.Domain.Pokemon;
namespace Game.Domain.WildBattle;

public enum EventIslandEncounterKind { HoOh, Lugia, Deoxys }
public sealed record EventIslandWildOrigin(MapId Map, EventIslandEncounterKind Kind)
{
    public string RulesVersion { get; init; } = EventIslandWildBattleContract.Version;
    public bool ModernFatefulEncounter { get; init; } = true;
}
/// <summary>Three exact seteventmon entries. A matching origin is not authorization to unlock an island.</summary>
public static class EventIslandWildBattleContract
{
    public const string Version = "firered-event-island-wild-entry-v1";
    public static EventIslandWildOrigin Origin(EventIslandEncounterKind kind) => kind switch {
        EventIslandEncounterKind.HoOh => new(new("map.navel-rock-summit"), kind),
        EventIslandEncounterKind.Lugia => new(new("map.navel-rock-base"), kind),
        EventIslandEncounterKind.Deoxys => new(new("map.birth-island-exterior"), kind),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)) };
    public static SpeciesId Species(EventIslandEncounterKind kind) => kind switch {
        EventIslandEncounterKind.HoOh => EventIslandOwnedPokemonCatalog.AdditionalSpecies[0],
        EventIslandEncounterKind.Lugia => EventIslandOwnedPokemonCatalog.AdditionalSpecies[1],
        EventIslandEncounterKind.Deoxys => EventIslandOwnedPokemonCatalog.AdditionalSpecies[2],
        _ => throw new ArgumentOutOfRangeException(nameof(kind)) };
    public static int Level(EventIslandEncounterKind kind) => Enum.IsDefined(kind)
        ? kind == EventIslandEncounterKind.Deoxys ? 30 : 70 : throw new ArgumentOutOfRangeException(nameof(kind));
    public static bool Matches(EventIslandWildOrigin? origin, PokemonInstance? pokemon) =>
        origin is not null && pokemon is not null && Enum.IsDefined(origin.Kind) && origin == Origin(origin.Kind) &&
        pokemon.Species == Species(origin.Kind) && pokemon.Level == Level(origin.Kind);
}
