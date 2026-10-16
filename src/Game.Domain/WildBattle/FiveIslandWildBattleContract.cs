using System.Collections.Immutable;
using Game.Domain.Battle;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
namespace Game.Domain.WildBattle;

public enum FiveIslandEncounterKind { Land, Surf, OldRod, GoodRod, SuperRod }
public sealed record FiveIslandWildOrigin(MapId Map, FiveIslandEncounterKind Kind, int SourceSlot)
{
    public string RulesVersion { get; init; } = FiveIslandWildBattleContract.Version;
}
public sealed record FiveIslandSourceSlot(SpeciesId Species, byte MinimumLevel, byte MaximumLevel);
/// <summary>New source-entry admission; ownership uses the authenticated135 catalog.
/// Good/Super Rod declarations are retained for exact generation, while acquisition and field use remain separate.</summary>
public static class FiveIslandWildBattleContract
{
    public const string Version="firered-five-island-wild-entry-v1";
    public static bool IsRuntimeKind(FiveIslandEncounterKind kind) => kind is FiveIslandEncounterKind.Land or FiveIslandEncounterKind.Surf or FiveIslandEncounterKind.OldRod;
    public static FiveIslandSourceSlot? SourceSlot(FiveIslandWildOrigin origin)
    {
        if (origin is null || origin.RulesVersion!=Version || origin.SourceSlot<0 || !Enum.IsDefined(origin.Kind)) return null;
        if (origin.Kind==FiveIslandEncounterKind.Land)
        {
            var t=FiveIslandEncounterTables.Land.FirstOrDefault(t=>t.Map==origin.Map);
            if (t is null || origin.SourceSlot>=t.Slots.Length) return null;
            var s=t.Slots[origin.SourceSlot];return new(s.Species,s.MinimumLevel,s.MaximumLevel);
        }
        if (origin.Kind == FiveIslandEncounterKind.Surf)
        {
            var t=FiveIslandEncounterTables.Water.FirstOrDefault(t=>t.Map==origin.Map);
            if (t is null || origin.SourceSlot>=t.Slots.Length) return null;
            var s=t.Slots[origin.SourceSlot];return new(s.Species,s.MinimumLevel,s.MaximumLevel);
        }
        var fish=FiveIslandEncounterTables.FishingSource.FirstOrDefault(t=>t.Map==origin.Map);
        bool group=origin.Kind switch { FiveIslandEncounterKind.OldRod=>origin.SourceSlot is 0 or 1,
            FiveIslandEncounterKind.GoodRod=>origin.SourceSlot is >=2 and <=4,
            FiveIslandEncounterKind.SuperRod=>origin.SourceSlot is >=5 and <=9,_=>false };
        if (fish is null || !group || origin.SourceSlot>=fish.Slots.Length) return null;
        var slot=fish.Slots[origin.SourceSlot];return new(slot.Species,slot.MinimumLevel,slot.MaximumLevel);
    }
    public static bool Matches(FiveIslandWildOrigin origin,PokemonInstance mon) => mon is not null && SourceSlot(origin) is {} slot &&
        slot.Species==mon.Species && mon.Level>=slot.MinimumLevel && mon.Level<=slot.MaximumLevel;
}
