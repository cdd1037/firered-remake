using System.Collections.Immutable;
using Game.Domain.Battle;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
namespace Game.Domain.WildBattle;

public enum VictoryRoadEncounterKind { Land, Surf, OldRod, GoodRod, SuperRod }
public sealed record VictoryRoadWildOrigin(MapId Map, VictoryRoadEncounterKind Kind, int SourceSlot)
{
    public string RulesVersion { get; init; } = VictoryRoadWildBattleContract.Version;
}
public sealed record VictoryRoadSourceSlot(SpeciesId Species, byte MinimumLevel, byte MaximumLevel);
/// <summary>New source-entry admission; ownership remains the existing authenticated115 catalog.
/// Good/Super Rod declarations are retained for exact generation, while acquisition and field use remain separate.</summary>
public static class VictoryRoadWildBattleContract
{
    public const string Version="firered-victory-road-wild-entry-v1";
    public static VictoryRoadSourceSlot? SourceSlot(VictoryRoadWildOrigin origin)
    {
        if (origin is null || origin.RulesVersion!=Version || origin.SourceSlot<0 || !Enum.IsDefined(origin.Kind)) return null;
        if (origin.Kind==VictoryRoadEncounterKind.Land)
        {
            var t=VictoryRoadEncounterTables.Land.FirstOrDefault(t=>t.Map==origin.Map);
            if (t is null || origin.SourceSlot>=t.Slots.Length) return null;
            var s=t.Slots[origin.SourceSlot];return new(s.Species,s.MinimumLevel,s.MaximumLevel);
        }
        if (origin.Kind==VictoryRoadEncounterKind.Surf)
        {
            var t=VictoryRoadEncounterTables.Water.FirstOrDefault(t=>t.Map==origin.Map);
            if (t is null || origin.SourceSlot>=t.Slots.Length) return null;
            var s=t.Slots[origin.SourceSlot];return new(s.Species,s.MinimumLevel,s.MaximumLevel);
        }
        var fish=VictoryRoadEncounterTables.FishingSource.FirstOrDefault(t=>t.Map==origin.Map);
        bool group=origin.Kind switch { VictoryRoadEncounterKind.OldRod=>origin.SourceSlot is 0 or 1,
            VictoryRoadEncounterKind.GoodRod=>origin.SourceSlot is >=2 and <=4,
            VictoryRoadEncounterKind.SuperRod=>origin.SourceSlot is >=5 and <=9,_=>false };
        if (fish is null || !group || origin.SourceSlot>=fish.Slots.Length) return null;
        var slot=fish.Slots[origin.SourceSlot];return new(slot.Species,slot.MinimumLevel,slot.MaximumLevel);
    }
    public static bool Matches(VictoryRoadWildOrigin origin,PokemonInstance mon) => mon is not null && SourceSlot(origin) is {} slot &&
        slot.Species==mon.Species && mon.Level>=slot.MinimumLevel && mon.Level<=slot.MaximumLevel;
}
