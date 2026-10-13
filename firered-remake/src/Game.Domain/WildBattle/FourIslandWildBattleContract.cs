using System.Collections.Immutable;
using Game.Domain.Battle;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
namespace Game.Domain.WildBattle;

public enum FourIslandEncounterKind { Land, Surf, OldRod, GoodRod, SuperRod }
public sealed record FourIslandWildOrigin(MapId Map, FourIslandEncounterKind Kind, int SourceSlot)
{
    public string RulesVersion { get; init; } = FourIslandWildBattleContract.Version;
}
public sealed record FourIslandSourceSlot(SpeciesId Species, byte MinimumLevel, byte MaximumLevel);
/// <summary>New source-entry admission; ownership uses the authenticated125 catalog.
/// Good/Super Rod declarations are retained for exact generation, while acquisition and field use remain separate.</summary>
public static class FourIslandWildBattleContract
{
    public const string Version="firered-four-island-wild-entry-v1";
    public static FourIslandSourceSlot? SourceSlot(FourIslandWildOrigin origin)
    {
        if (origin is null || origin.RulesVersion!=Version || origin.SourceSlot<0 || !Enum.IsDefined(origin.Kind)) return null;
        if (origin.Kind==FourIslandEncounterKind.Land)
        {
            var t=FourIslandEncounterTables.Land.FirstOrDefault(t=>t.Map==origin.Map);
            if (t is null || origin.SourceSlot>=t.Slots.Length) return null;
            var s=t.Slots[origin.SourceSlot];return new(s.Species,s.MinimumLevel,s.MaximumLevel);
        }
        if (origin.Kind == FourIslandEncounterKind.Surf)
        {
            var t=FourIslandEncounterTables.Water.FirstOrDefault(t=>t.Map==origin.Map);
            if (t is null || origin.SourceSlot>=t.Slots.Length) return null;
            var s=t.Slots[origin.SourceSlot];return new(s.Species,s.MinimumLevel,s.MaximumLevel);
        }
        var fish=FourIslandEncounterTables.FishingSource.FirstOrDefault(t=>t.Map==origin.Map);
        bool group=origin.Kind switch { FourIslandEncounterKind.OldRod=>origin.SourceSlot is 0 or 1,
            FourIslandEncounterKind.GoodRod=>origin.SourceSlot is >=2 and <=4,
            FourIslandEncounterKind.SuperRod=>origin.SourceSlot is >=5 and <=9,_=>false };
        if (fish is null || !group || origin.SourceSlot>=fish.Slots.Length) return null;
        var slot=fish.Slots[origin.SourceSlot];return new(slot.Species,slot.MinimumLevel,slot.MaximumLevel);
    }
    public static bool Matches(FourIslandWildOrigin origin,PokemonInstance mon) => mon is not null && SourceSlot(origin) is {} slot &&
        slot.Species==mon.Species && mon.Level>=slot.MinimumLevel && mon.Level<=slot.MaximumLevel;
}
