using System.Collections.Immutable;
using Game.Domain.Battle;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
namespace Game.Domain.Encounters;

/// <summary>Exact admitted land, Surf and Old Rod combinations reuse owned132 and ordinary creation.
/// A source-slot call does not grant rod possession, map access, or an encounter roll.</summary>
public static class SixIslandWildPokemonFactory
{
    public static ImmutableArray<SpeciesId> WildSpecies { get; } = SixIslandEncounterTables.Land.SelectMany(t=>t.Slots).Select(s=>s.Species).Concat(SixIslandEncounterTables.Water.SelectMany(t=>t.Slots).Select(s=>s.Species)).Concat(SixIslandOldRodEncounterRules.SourceAdditions.SelectMany(t=>t.Slots).Select(s=>s.Species)).Distinct().ToImmutableArray();
    public static EarlyLandWildPokemonResult Generate(SixIslandOwnedPokemonCatalog owned, EarlyLandEncounterTable table,
        EarlyLandEncounterState encounter, RngState rng, uint playerTrainerId)
    {
        ValidateOwned(owned);
        if (encounter.RulesVersion != EarlyLandEncounterRules.SixIslandRulesVersion) throw new ArgumentException("six_island_wild_profile");
        EarlyLandEncounterRules.ValidateState(encounter,table);
        var pending=encounter.Pending ?? throw new InvalidOperationException("six_island_wild_pending");
        // The successor keeps one walking process across predecessor maps. The actual
        // successor state/table was validated above; source generation keeps the same owned132 root.
        if (!SixIslandEncounterTables.Land.Any(t=>t.Map==table.Map))
        {
            var inherited=Route1WildPokemonFactory.GenerateOrdinary(owned.Catalog,pending.Candidate.Species,pending.Candidate.Level,rng,playerTrainerId);
            return new(inherited.Pokemon,inherited.GlobalRng,inherited.RandomTrace);
        }
        var result=GenerateSourceSlot(owned,new(pending.Candidate.Map,SixIslandEncounterKind.Land,pending.Candidate.Slot),
            pending.Candidate.Level,rng,playerTrainerId);
        return new(result.Pokemon,result.GlobalRng,result.RandomTrace);
    }
    public static Route1WildPokemonResult GenerateSourceSlot(SixIslandOwnedPokemonCatalog owned, SixIslandWildOrigin origin,
        byte level, RngState rng, uint playerTrainerId)
    {
        ValidateOwned(owned);
        var slot=SixIslandWildBattleContract.SourceSlot(origin);
        if (origin is null || !SixIslandWildBattleContract.IsRuntimeKind(origin.Kind) || slot is null || !owned.Catalog.Species.ContainsKey(slot.Species) || level < slot.MinimumLevel || level > slot.MaximumLevel)
            throw new ArgumentException("six_island_wild_source_slot");
        var expected=SixIslandWildSource.InitialMoves.SingleOrDefault(m=>m.Species==slot.Species && m.Level==level)
            ?? throw new NotSupportedException("six_island_wild_default_set");
        var species=owned.Catalog.Species[slot.Species];
        if (!PokemonMoveRules.InitialMovesCore(owned.Catalog,species,level).Select(m=>m.Move).SequenceEqual(expected.Moves))
            throw new NotSupportedException("six_island_wild_default_source");
        var result=Route1WildPokemonFactory.GenerateOrdinary(owned.Catalog,slot.Species,level,rng,playerTrainerId);
        if (!SourceInitialInstance(owned,origin,result.Pokemon)) throw new InvalidOperationException("six_island_wild_generated_identity");
        return result;
    }
    public static bool SourceInitialInstance(SixIslandOwnedPokemonCatalog owned, SixIslandWildOrigin origin, PokemonInstance mon)
    {
        if (owned is null || !ReferenceEquals(SixIslandOwnedPokemonCatalog.For(owned.Catalog),owned) || mon is null || origin is null ||
            !SixIslandWildBattleContract.IsRuntimeKind(origin.Kind) || !SixIslandWildBattleContract.Matches(origin,mon) || !owned.Catalog.Species.TryGetValue(mon.Species,out var species)) return false;
        var dependency=SixIslandWildSource.Species.Single(s=>s.Species==mon.Species);
        var moves=SixIslandWildSource.InitialMoves.Single(s=>s.Species==mon.Species && s.Level==mon.Level).Moves;
        return SourcePokemonValidator.ValidatePermanent(owned.Mechanics,mon) is null && mon.SourceSpeciesId==dependency.SourceSpeciesId &&
            (mon.AbilityId==dependency.Ability1 || mon.AbilityId==dependency.Ability2 && dependency.Ability2!="ABILITY_NONE") &&
            mon.Evs==SixStats.Zero && mon.CurrentHp==mon.Stats.MaxHp && mon.Friendship==species.Friendship &&
            mon.Experience==GrowthRules.ExperienceAtLevel(species.GrowthRate,mon.Level) && mon.Moves.Select(m=>m.Move).SequenceEqual(moves) &&
            mon.Moves.All(m=>m.PpUps==0 && m.CurrentPp==m.MaximumPp && m.MaximumPp==owned.Catalog.Moves[m.Move].Pp);
    }
    public static string HeldItem(SpeciesId species,int roll)
    {
        if (roll is <0 or >=100) throw new ArgumentOutOfRangeException(nameof(roll));
        var entry=SixIslandWildSource.Species.SingleOrDefault(s=>s.Species==species)
            ?? throw new NotSupportedException("six_island_wild_held_species");
        // Source SetWildMonHeldItem: same non-NONE common/rare always wins; otherwise 45/50/5.
        if (entry.ItemCommon==entry.ItemRare && entry.ItemCommon!="ITEM_NONE") return entry.ItemCommon;
        return roll <45 ? "ITEM_NONE" : roll <95 ? entry.ItemCommon : entry.ItemRare;
    }
    public static EarlyLandWildHeldItemResult AssignHeldItem(SpeciesId species,RngState rng)
    {
        if (!WildSpecies.Contains(species)) throw new NotSupportedException("six_island_wild_held_species");
        var draw=Gen3Rng.Next(rng,"wild-battle.held-item",100);
        return new(HeldItem(species,draw.Trace.Result),draw.State,[draw.Trace]);
    }
    private static void ValidateOwned(SixIslandOwnedPokemonCatalog owned)
    {
        if (owned is null || !ReferenceEquals(SixIslandOwnedPokemonCatalog.For(owned.Catalog),owned) || owned.Catalog.Species.Count!=132)
            throw new ArgumentException("six_island_wild_owned_proof");
    }
}
