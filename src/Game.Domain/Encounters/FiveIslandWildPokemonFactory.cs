using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
namespace Game.Domain.Encounters;

/// <summary>Regional origin admission over canonical ordinary wild generation.
/// Source-slot admission does not grant rod possession, map access or an encounter roll.</summary>
public static class FiveIslandWildPokemonFactory
{
    public static EarlyLandWildPokemonResult Generate(GamePokemonCatalog owned, EarlyLandEncounterTable table,
        EarlyLandEncounterState encounter, RngState rng, uint playerTrainerId)
    {
        ValidateOwned(owned);
        if (encounter.RulesVersion != EarlyLandEncounterRules.FiveIslandRulesVersion) throw new ArgumentException("five_island_wild_profile");
        EarlyLandEncounterRules.ValidateState(encounter,table);
        var pending=encounter.Pending ?? throw new InvalidOperationException("five_island_wild_pending");
        // The successor keeps one walking process across predecessor maps. The actual
        // successor state/table was validated above; source generation keeps the same owned135 root.
        if (!FiveIslandEncounterTables.Land.Any(t=>t.Map==table.Map))
        {
            var inherited=Route1WildPokemonFactory.GenerateOrdinary(owned.Catalog,pending.Candidate.Species,pending.Candidate.Level,rng,playerTrainerId);
            return new(inherited.Pokemon,inherited.GlobalRng,inherited.RandomTrace);
        }
        var result=GenerateSourceSlot(owned,new(pending.Candidate.Map,FiveIslandEncounterKind.Land,pending.Candidate.Slot),
            pending.Candidate.Level,rng,playerTrainerId);
        return new(result.Pokemon,result.GlobalRng,result.RandomTrace);
    }
    public static Route1WildPokemonResult GenerateSourceSlot(GamePokemonCatalog owned, FiveIslandWildOrigin origin,
        byte level, RngState rng, uint playerTrainerId)
    {
        ValidateOwned(owned);
        var slot=FiveIslandWildBattleContract.SourceSlot(origin);
        if (origin is null || !FiveIslandWildBattleContract.IsRuntimeKind(origin.Kind) || slot is null || !owned.Catalog.Species.ContainsKey(slot.Species) || level < slot.MinimumLevel || level > slot.MaximumLevel)
            throw new ArgumentException("five_island_wild_source_slot");
        var result=Route1WildPokemonFactory.GenerateOrdinary(owned.Catalog,slot.Species,level,rng,playerTrainerId);
        if (!SourceInitialInstance(owned,origin,result.Pokemon)) throw new InvalidOperationException("five_island_wild_generated_identity");
        return result;
    }
    public static bool SourceInitialInstance(GamePokemonCatalog owned, FiveIslandWildOrigin origin, PokemonInstance mon)
    {
        if (owned is null || mon is null || origin is null ||
            !FiveIslandWildBattleContract.IsRuntimeKind(origin.Kind) || !FiveIslandWildBattleContract.Matches(origin,mon) || !owned.Catalog.Species.TryGetValue(mon.Species,out var species)) return false;
        return CanonicalWildPokemonFactory.IsInitialInstance(owned, mon);
    }
    private static void ValidateOwned(GamePokemonCatalog owned)
    {
        if (owned is null)
            throw new ArgumentException("five_island_wild_owned_proof");
    }
}
