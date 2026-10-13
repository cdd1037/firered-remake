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
public static partial class WildBattleRules
{
    /// <summary>The field owner must first prove the source walk/Surf/cast has completed.
    /// This entry validates every origin and exact natural move set; it grants no new rod or map access.</summary>
    public static WildBattleResult StartSeviiRubyWild(SeviiRubyOwnedPokemonCatalog owned,SeviiRubyWildOrigin origin,
        ImmutableArray<WildBattleCombatant> playerParty,int activeIndex,WildBattleCombatant opponent,RngState rng, WildBattleTerrain terrain)
    {
        if (owned is null || !ReferenceEquals(SeviiRubyOwnedPokemonCatalog.For(owned.Catalog),owned)) return Fail(null,"sevii_ruby_wild_owned_proof");
        if (origin is null) return Fail(null,"sevii_ruby_wild_origin_missing");
        if (origin.Kind==SeviiRubyEncounterKind.RockSmash) return Fail(null,"sevii_ruby_rock_smash_field_unavailable");
        if (opponent?.Pokemon is not {} mon || opponent.Status!=WildBattleStatus.None || opponent.HeldItem!="ITEM_NONE" ||
            !SeviiRubyWildPokemonFactory.SourceInitialInstance(owned,origin,mon)) return Fail(null,"sevii_ruby_wild_source_instance");
        if (SourceBattleAdmission.ValidateOwnedEntry(owned,playerParty,activeIndex) is {} ownedError) return Fail(null,ownedError);
        if (SourceBattleAdmission.ValidateEntry(owned.Mechanics,BattleRulesProfile.CanonicalSourceSingles,playerParty,activeIndex,opponent) is {} sourceError)
            return Fail(null,sourceError);
        // BattleSetup_GetTerrainId reads the player destination tile, including shore fishing.
        // The authoritative field caller supplies that source-derived terrain.
        if (!Enum.IsDefined(terrain)) return Fail(null,"sevii_ruby_wild_terrain");
        return StartSourceWildCore(owned.Mechanics.Catalog,playerParty,activeIndex,opponent,rng,terrain,
            origin.Kind==SeviiRubyEncounterKind.OldRod,null,false,BattleRulesProfile.CanonicalSourceSingles,owned,seviiRuby:origin);
    }
}

public static partial class WildBattleRules
{
    /// <summary>Continue authentic older-map encounters with the successor owned party. The
    /// existing Victory Road origin and generic first-Sevii/scripted receipts keep their identity.</summary>
    public static WildBattleResult StartSeviiRubyInheritedWild(SeviiRubyOwnedPokemonCatalog owned,
        MapId map, FirstSeviiEncounterKind kind, ImmutableArray<WildBattleCombatant> playerParty,
        int activeIndex, WildBattleCombatant opponent, RngState rng, WildBattleTerrain terrain,
        VictoryRoadWildOrigin? victoryRoad = null, bool lostelleHypno = false,
        PokemonTowerBattleKind? towerKind = null, bool scriptedSnorlax = false)
    {
        if (owned is null || !ReferenceEquals(SeviiRubyOwnedPokemonCatalog.For(owned.Catalog),owned))
            return Fail(null,"sevii_ruby_inherited_owned_proof");
        if (!Enum.IsDefined(kind) || !Enum.IsDefined(terrain) || opponent?.Pokemon is not {} mon)
            return Fail(null,"sevii_ruby_inherited_context");
        if (SeviiRubyEncounterTables.Land.Any(t=>t.Map==map) || SeviiRubyEncounterTables.Water.Any(t=>t.Map==map))
            return Fail(null,"sevii_ruby_inherited_new_map");
        bool firstSevii = FirstSeviiEncounterTables.Land.Any(t=>t.Map==map) ||
            FirstSeviiEncounterTables.Water.Any(t=>t.Map==map);
        if (victoryRoad is not null)
        {
            if (victoryRoad.Map != map || victoryRoad.Kind != (kind switch {
                    FirstSeviiEncounterKind.Land=>VictoryRoadEncounterKind.Land,
                    FirstSeviiEncounterKind.Surf=>VictoryRoadEncounterKind.Surf,
                    _=>VictoryRoadEncounterKind.OldRod}) ||
                !VictoryRoadWildPokemonFactory.SourceInitialInstance(owned.Predecessor,victoryRoad,mon) ||
                opponent.Status != WildBattleStatus.None || opponent.HeldItem != "ITEM_NONE" || lostelleHypno || scriptedSnorlax || towerKind is not null)
                return Fail(null,"sevii_ruby_inherited_victory_origin");
        }
        else if (lostelleHypno)
        {
            if (map.Value != "map.three-island-berry-forest" || kind != FirstSeviiEncounterKind.Land ||
                !FirstSeviiHypnoBattleContract.SourceInitialInstance(mon) || opponent.Status != WildBattleStatus.None ||
                opponent.HeldItem != "ITEM_NONE" || scriptedSnorlax || towerKind is not null)
                return Fail(null,"sevii_ruby_inherited_hypno_origin");
            firstSevii = true;
        }
        else if (firstSevii)
        {
            bool Match(SpeciesId species,byte min,byte max)=>species==mon.Species && mon.Level>=min && mon.Level<=max;
            bool admitted = kind switch {
                FirstSeviiEncounterKind.Land=>FirstSeviiEncounterTables.Land.FirstOrDefault(t=>t.Map==map)?.Slots.Any(s=>Match(s.Species,s.MinimumLevel,s.MaximumLevel))==true,
                FirstSeviiEncounterKind.Surf=>FirstSeviiEncounterTables.Water.FirstOrDefault(t=>t.Map==map)?.Slots.Any(s=>Match(s.Species,s.MinimumLevel,s.MaximumLevel))==true,
                FirstSeviiEncounterKind.OldRod=>FirstSeviiOldRodEncounterRules.SourceAdditions.FirstOrDefault(t=>t.Map==map)?.Slots.Any(s=>Match(s.Species,s.MinimumLevel,s.MaximumLevel))==true,
                _=>false};
            if (!admitted || scriptedSnorlax || towerKind is not null) return Fail(null,"sevii_ruby_inherited_first_sevii_origin");
        }
        else return StartSafariOwnedWild(OwnedBattlePokemonCatalog.For(owned.Catalog)!,playerParty,activeIndex,
            opponent,rng,terrain,kind==FirstSeviiEncounterKind.OldRod,towerKind,scriptedSnorlax);
        if (SourceBattleAdmission.ValidateOwnedEntry(owned,playerParty,activeIndex) is {} error) return Fail(null,error);
        if (SourceBattleAdmission.ValidateEntry(owned.Mechanics,BattleRulesProfile.CanonicalSourceSingles,playerParty,activeIndex,opponent) is {} sourceError)
            return Fail(null,sourceError);
        return StartSourceWildCore(owned.Mechanics.Catalog,playerParty,activeIndex,opponent,rng,terrain,
            kind==FirstSeviiEncounterKind.OldRod,null,false,BattleRulesProfile.CanonicalSourceSingles,owned,
            firstSeviiWild:firstSevii,victoryRoad:victoryRoad);
    }
}
