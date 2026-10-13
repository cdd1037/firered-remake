using System.Collections.Immutable;
using Game.Domain.Battle;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
namespace Game.Domain.WildBattle;

public enum SeviiRubyEncounterKind { Land, Surf, OldRod, GoodRod, SuperRod, RockSmash }
public sealed record SeviiRubyWildOrigin(MapId Map, SeviiRubyEncounterKind Kind, int SourceSlot)
{
    public string RulesVersion { get; init; } = SeviiRubyWildBattleContract.Version;
}
public sealed record SeviiRubySourceSlot(SpeciesId Species, byte MinimumLevel, byte MaximumLevel);
/// <summary>New source-entry admission; ownership uses the authenticated118 catalog.
/// Good/Super Rod declarations are retained for exact generation, while acquisition and field use remain separate.</summary>
public static class SeviiRubyWildBattleContract
{
    public const string Version="firered-sevii-ruby-wild-entry-v1";
    public static SeviiRubySourceSlot? SourceSlot(SeviiRubyWildOrigin origin)
    {
        if (origin is null || origin.RulesVersion!=Version || origin.SourceSlot<0 || !Enum.IsDefined(origin.Kind)) return null;
        if (origin.Kind==SeviiRubyEncounterKind.Land)
        {
            var t=SeviiRubyEncounterTables.Land.FirstOrDefault(t=>t.Map==origin.Map);
            if (t is null || origin.SourceSlot>=t.Slots.Length) return null;
            var s=t.Slots[origin.SourceSlot];return new(s.Species,s.MinimumLevel,s.MaximumLevel);
        }
        if (origin.Kind is SeviiRubyEncounterKind.Surf or SeviiRubyEncounterKind.RockSmash)
        {
            var t=(origin.Kind==SeviiRubyEncounterKind.Surf ? SeviiRubyEncounterTables.Water : SeviiRubyEncounterTables.RockSmashSource).FirstOrDefault(t=>t.Map==origin.Map);
            if (t is null || origin.SourceSlot>=t.Slots.Length) return null;
            var s=t.Slots[origin.SourceSlot];return new(s.Species,s.MinimumLevel,s.MaximumLevel);
        }
        var fish=SeviiRubyEncounterTables.FishingSource.FirstOrDefault(t=>t.Map==origin.Map);
        bool group=origin.Kind switch { SeviiRubyEncounterKind.OldRod=>origin.SourceSlot is 0 or 1,
            SeviiRubyEncounterKind.GoodRod=>origin.SourceSlot is >=2 and <=4,
            SeviiRubyEncounterKind.SuperRod=>origin.SourceSlot is >=5 and <=9,_=>false };
        if (fish is null || !group || origin.SourceSlot>=fish.Slots.Length) return null;
        var slot=fish.Slots[origin.SourceSlot];return new(slot.Species,slot.MinimumLevel,slot.MaximumLevel);
    }
    public static bool Matches(SeviiRubyWildOrigin origin,PokemonInstance mon) => mon is not null && SourceSlot(origin) is {} slot &&
        slot.Species==mon.Species && mon.Level>=slot.MinimumLevel && mon.Level<=slot.MaximumLevel;
}
