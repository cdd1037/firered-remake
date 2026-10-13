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
public static partial class WildBattleRules
{
    /// <summary>The field owner must first prove the source walk/Surf/cast has completed.
    /// This entry validates every origin and exact natural move set; it grants no new rod or map access.</summary>
    public static WildBattleResult StartFiveIslandWild(FiveIslandOwnedPokemonCatalog owned,FiveIslandWildOrigin origin,
        ImmutableArray<WildBattleCombatant> playerParty,int activeIndex,WildBattleCombatant opponent,RngState rng, WildBattleTerrain terrain, OwnedBattlePokemonCatalog? successorOwned = null)
    {
        if (owned is null || !ReferenceEquals(FiveIslandOwnedPokemonCatalog.For(owned.Catalog),owned)) return Fail(null,"five_island_wild_owned_proof");
        if (successorOwned is not null && !OwnedBattlePokemonCatalog.IsCampaignSuccessorOf(owned, successorOwned))
            return Fail(null,"source_wild_successor_owned");
        OwnedBattlePokemonCatalog actualOwned = successorOwned ?? OwnedBattlePokemonCatalog.For(owned.Catalog)!;
        if (origin is null) return Fail(null,"five_island_wild_origin_missing");
        if (!FiveIslandWildBattleContract.IsRuntimeKind(origin.Kind)) return Fail(null,"five_island_rod_field_unavailable");
        if (opponent?.Pokemon is not {} mon || opponent.Status!=WildBattleStatus.None || opponent.HeldItem!="ITEM_NONE" ||
            !FiveIslandWildPokemonFactory.SourceInitialInstance(owned,origin,mon)) return Fail(null,"five_island_wild_source_instance");
        if (SourceBattleAdmission.ValidateOwnedEntry(actualOwned,playerParty,activeIndex) is {} ownedError) return Fail(null,ownedError);
        if (SourceBattleAdmission.ValidateEntry(owned.Mechanics,BattleRulesProfile.CanonicalSourceSingles,playerParty,activeIndex,opponent) is {} sourceError)
            return Fail(null,sourceError);
        // BattleSetup_GetTerrainId reads the player destination tile, including shore fishing.
        // The authoritative field caller supplies that source-derived terrain.
        if (!Enum.IsDefined(terrain)) return Fail(null,"five_island_wild_terrain");
        return StartSourceWildCore(owned.Mechanics.Catalog,playerParty,activeIndex,opponent,rng,terrain,
            origin.Kind==FiveIslandEncounterKind.OldRod,null,false,BattleRulesProfile.CanonicalSourceSingles,actualOwned,fiveIsland:origin);
    }
}

public static partial class WildBattleRules
{
    /// <summary>Runs older source entries with the new owned party and preserves the original encounter receipt.</summary>
    public static WildBattleResult StartFiveIslandInheritedWild(FiveIslandOwnedPokemonCatalog owned,
        MapId map, FirstSeviiEncounterKind kind, ImmutableArray<WildBattleCombatant> playerParty,
        int activeIndex, WildBattleCombatant opponent, RngState rng, WildBattleTerrain terrain,
        VictoryRoadWildOrigin? victoryRoad = null, SeviiRubyWildOrigin? seviiRuby = null, FourIslandWildOrigin? fourIsland = null, SixIslandWildOrigin? sixIsland = null,
        bool lostelleHypno = false, PokemonTowerBattleKind? towerKind = null, bool scriptedSnorlax = false, OwnedBattlePokemonCatalog? successorOwned = null)
    {
        if (owned is null || !ReferenceEquals(FiveIslandOwnedPokemonCatalog.For(owned.Catalog),owned))
            return Fail(null,"five_island_inherited_owned_proof");
        if (successorOwned is not null && !OwnedBattlePokemonCatalog.IsCampaignSuccessorOf(owned, successorOwned))
            return Fail(null,"source_wild_successor_owned");
        OwnedBattlePokemonCatalog actualOwned = successorOwned ?? OwnedBattlePokemonCatalog.For(owned.Catalog)!;
        if (!Enum.IsDefined(kind) || !Enum.IsDefined(terrain) || opponent?.Pokemon is null)
            return Fail(null,"five_island_inherited_context");
        if (map.Value.StartsWith("map.five-island", StringComparison.Ordinal))
            return Fail(null,"five_island_inherited_new_map");
        if (sixIsland is not null)
        {
            if (sixIsland.Map != map || victoryRoad is not null || seviiRuby is not null || fourIsland is not null || lostelleHypno || towerKind is not null || scriptedSnorlax ||
                sixIsland.Kind != (kind switch { FirstSeviiEncounterKind.Land=>SixIslandEncounterKind.Land,
                    FirstSeviiEncounterKind.Surf=>SixIslandEncounterKind.Surf, _=>SixIslandEncounterKind.OldRod }))
                return Fail(null,"five_island_inherited_six_origin");
            return StartSixIslandWild(owned.Predecessor,sixIsland,playerParty,activeIndex,opponent,rng,terrain,actualOwned);
        }
        return StartSixIslandInheritedWild(owned.Predecessor,map,kind,playerParty,activeIndex,opponent,rng,terrain,
            victoryRoad,seviiRuby,fourIsland,lostelleHypno,towerKind,scriptedSnorlax,actualOwned);
    }
}
