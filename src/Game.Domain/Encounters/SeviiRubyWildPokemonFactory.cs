using System.Collections.Immutable;
using Game.Domain.Battle;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
namespace Game.Domain.Encounters;

/// <summary>All 237 exact source species/level combinations reuse owned118 and ordinary creation.
/// A source-slot call does not grant rod possession, map access, or an encounter roll.</summary>
public static class SeviiRubyWildPokemonFactory
{
    public static ImmutableArray<SpeciesId> WildSpecies { get; } = SeviiRubyWildSource.Species.Select(s=>s.Species).ToImmutableArray();
    public static EarlyLandWildPokemonResult Generate(GamePokemonCatalog owned, EarlyLandEncounterTable table,
        EarlyLandEncounterState encounter, RngState rng, uint playerTrainerId)
    {
        ValidateOwned(owned);
        if (encounter.RulesVersion != EarlyLandEncounterRules.SeviiRubyRulesVersion) throw new ArgumentException("sevii_ruby_wild_profile");
        EarlyLandEncounterRules.ValidateState(encounter,table);
        var pending=encounter.Pending ?? throw new InvalidOperationException("sevii_ruby_wild_pending");
        // The successor keeps one walking process across predecessor maps. The actual
        // successor state/table was validated above; source generation keeps the same owned118 root.
        if (!SeviiRubyEncounterTables.Land.Any(t=>t.Map==table.Map))
        {
            var inherited=Route1WildPokemonFactory.GenerateOrdinary(owned.Catalog,pending.Candidate.Species,pending.Candidate.Level,rng,playerTrainerId);
            return new(inherited.Pokemon,inherited.GlobalRng,inherited.RandomTrace);
        }
        var result=GenerateSourceSlot(owned,new(pending.Candidate.Map,SeviiRubyEncounterKind.Land,pending.Candidate.Slot),
            pending.Candidate.Level,rng,playerTrainerId);
        return new(result.Pokemon,result.GlobalRng,result.RandomTrace);
    }
    public static Route1WildPokemonResult GenerateSourceSlot(GamePokemonCatalog owned, SeviiRubyWildOrigin origin,
        byte level, RngState rng, uint playerTrainerId)
    {
        ValidateOwned(owned);
        var slot=SeviiRubyWildBattleContract.SourceSlot(origin);
        if (slot is null || level < slot.MinimumLevel || level > slot.MaximumLevel)
            throw new ArgumentException("sevii_ruby_wild_source_slot");
        var expected=SeviiRubyWildSource.InitialMoves.SingleOrDefault(m=>m.Species==slot.Species && m.Level==level)
            ?? throw new NotSupportedException("sevii_ruby_wild_default_set");
        var species=owned.Catalog.Species[slot.Species];
        if (!PokemonMoveRules.InitialMovesCore(owned.Catalog,species,level).Select(m=>m.Move).SequenceEqual(expected.Moves))
            throw new NotSupportedException("sevii_ruby_wild_default_source");
        var result=Route1WildPokemonFactory.GenerateOrdinary(owned.Catalog,slot.Species,level,rng,playerTrainerId);
        if (!SourceInitialInstance(owned,origin,result.Pokemon)) throw new InvalidOperationException("sevii_ruby_wild_generated_identity");
        return result;
    }
    public static bool SourceInitialInstance(GamePokemonCatalog owned, SeviiRubyWildOrigin origin, PokemonInstance mon)
    {
        if (owned is null || mon is null ||
            !SeviiRubyWildBattleContract.Matches(origin,mon) || !owned.Catalog.Species.TryGetValue(mon.Species,out var species)) return false;
        var dependency=SeviiRubyWildSource.Species.Single(s=>s.Species==mon.Species);
        var moves=SeviiRubyWildSource.InitialMoves.Single(s=>s.Species==mon.Species && s.Level==mon.Level).Moves;
        return SourcePokemonValidator.ValidatePermanent(owned.Mechanics,mon) is null && mon.SourceSpeciesId==dependency.SourceSpeciesId &&
            (mon.AbilityId==dependency.Ability1 || mon.AbilityId==dependency.Ability2 && dependency.Ability2!="ABILITY_NONE") &&
            mon.Evs==SixStats.Zero && mon.CurrentHp==mon.Stats.MaxHp && mon.Friendship==species.Friendship &&
            mon.Experience==GrowthRules.ExperienceAtLevel(species.GrowthRate,mon.Level) && mon.Moves.Select(m=>m.Move).SequenceEqual(moves) &&
            mon.Moves.All(m=>m.PpUps==0 && m.CurrentPp==m.MaximumPp && m.MaximumPp==owned.Catalog.Moves[m.Move].Pp);
    }
    public static string HeldItem(SpeciesId species,int roll)
    {
        if (roll is <0 or >=100) throw new ArgumentOutOfRangeException(nameof(roll));
        var entry=SeviiRubyWildSource.Species.SingleOrDefault(s=>s.Species==species)
            ?? throw new NotSupportedException("sevii_ruby_wild_held_species");
        // Source SetWildMonHeldItem: same non-NONE common/rare always wins; otherwise 45/50/5.
        if (entry.ItemCommon==entry.ItemRare && entry.ItemCommon!="ITEM_NONE") return entry.ItemCommon;
        return roll <45 ? "ITEM_NONE" : roll <95 ? entry.ItemCommon : entry.ItemRare;
    }
    public static EarlyLandWildHeldItemResult AssignHeldItem(SpeciesId species,RngState rng)
    {
        if (!WildSpecies.Contains(species)) throw new NotSupportedException("sevii_ruby_wild_held_species");
        var draw=Gen3Rng.Next(rng,"wild-battle.held-item",100);
        return new(HeldItem(species,draw.Trace.Result),draw.State,[draw.Trace]);
    }
    private static void ValidateOwned(GamePokemonCatalog owned)
    {
        if (owned is null)
            throw new ArgumentException("sevii_ruby_wild_owned_proof");
    }
}
