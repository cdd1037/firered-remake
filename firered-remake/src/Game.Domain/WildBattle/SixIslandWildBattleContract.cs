using System.Collections.Immutable;
using Game.Domain.Battle;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
namespace Game.Domain.WildBattle;

public enum SixIslandEncounterKind { Land, Surf, OldRod, GoodRod, SuperRod }
public sealed record SixIslandWildOrigin(MapId Map, SixIslandEncounterKind Kind, int SourceSlot)
{
    public string RulesVersion { get; init; } = SixIslandWildBattleContract.Version;
}
public sealed record SixIslandSourceSlot(SpeciesId Species, byte MinimumLevel, byte MaximumLevel);
/// <summary>New source-entry admission; ownership uses the authenticated132 catalog.
/// Good/Super Rod declarations are retained for exact generation, while acquisition and field use remain separate.</summary>
public static class SixIslandWildBattleContract
{
    public const string Version="firered-six-island-wild-entry-v1";
    public static bool IsRuntimeKind(SixIslandEncounterKind kind) => kind is SixIslandEncounterKind.Land or SixIslandEncounterKind.Surf or SixIslandEncounterKind.OldRod;
    public static SixIslandSourceSlot? SourceSlot(SixIslandWildOrigin origin)
    {
        if (origin is null || origin.RulesVersion!=Version || origin.SourceSlot<0 || !Enum.IsDefined(origin.Kind)) return null;
        if (origin.Kind==SixIslandEncounterKind.Land)
        {
            var t=SixIslandEncounterTables.Land.FirstOrDefault(t=>t.Map==origin.Map);
            if (t is null || origin.SourceSlot>=t.Slots.Length) return null;
            var s=t.Slots[origin.SourceSlot];return new(s.Species,s.MinimumLevel,s.MaximumLevel);
        }
        if (origin.Kind == SixIslandEncounterKind.Surf)
        {
            var t=SixIslandEncounterTables.Water.FirstOrDefault(t=>t.Map==origin.Map);
            if (t is null || origin.SourceSlot>=t.Slots.Length) return null;
            var s=t.Slots[origin.SourceSlot];return new(s.Species,s.MinimumLevel,s.MaximumLevel);
        }
        var fish=SixIslandEncounterTables.FishingSource.FirstOrDefault(t=>t.Map==origin.Map);
        bool group=origin.Kind switch { SixIslandEncounterKind.OldRod=>origin.SourceSlot is 0 or 1,
            SixIslandEncounterKind.GoodRod=>origin.SourceSlot is >=2 and <=4,
            SixIslandEncounterKind.SuperRod=>origin.SourceSlot is >=5 and <=9,_=>false };
        if (fish is null || !group || origin.SourceSlot>=fish.Slots.Length) return null;
        var slot=fish.Slots[origin.SourceSlot];return new(slot.Species,slot.MinimumLevel,slot.MaximumLevel);
    }
    public static bool Matches(SixIslandWildOrigin origin,PokemonInstance mon) => mon is not null && SourceSlot(origin) is {} slot &&
        slot.Species==mon.Species && mon.Level>=slot.MinimumLevel && mon.Level<=slot.MaximumLevel;
}
