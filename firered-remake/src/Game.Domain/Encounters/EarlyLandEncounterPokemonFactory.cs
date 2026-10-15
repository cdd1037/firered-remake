using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Encounters;

public sealed record EarlyLandWildPokemonResult(PokemonInstance Pokemon, RngState GlobalRng,
    ImmutableArray<RandomTrace> RandomTrace);
public sealed record EarlyLandWildHeldItemResult(string HeldItem, RngState GlobalRng,
    ImmutableArray<RandomTrace> RandomTrace);

/// <summary>Ordinary source nature/PID-rejection/IV generation; the caller commits Pending separately.</summary>
public static class EarlyLandWildPokemonFactory
{
    public const string RulesVersion = "firered-early-kanto-wild-generation-v1";

    /// <summary>Current source-table admission shares ordinary generation without a regional ownership subclass.</summary>
    public static EarlyLandWildPokemonResult Generate(GamePokemonCatalog owned, EarlyLandEncounterTable table,
        EarlyLandEncounterState encounter, RngState globalRng, uint playerTrainerId)
    {
        EarlyLandEncounterRules.ValidateState(encounter, table);
        if (encounter.RulesVersion != EarlyLandEncounterRules.FiveIslandRulesVersion)
            throw new ArgumentException("source_table_land_profile");
        var candidate = encounter.Pending?.Candidate ?? throw new InvalidOperationException("source_table_land_pending");
        var result = Game.Domain.WildBattle.SourceTableWildBattleContract.Generate(owned,
            new(candidate.Map, Game.Domain.WildBattle.SourceTableEncounterKind.Land, candidate.Slot),
            candidate.Level, globalRng, playerTrainerId);
        return new(result.Pokemon, result.GlobalRng, result.RandomTrace);
    }

    /// <summary>Source SetWildMonHeldItem always draws once; FireRed has no Compound Eyes held-item modifier.</summary>
    public static EarlyLandWildHeldItemResult AssignHeldItem(SpeciesId species, RngState globalRng,
        string rulesVersion = EarlyLandEncounterRules.RulesVersion)
    {
        EarlyLandEncounterRules.ValidateRulesVersion(rulesVersion);
        if (rulesVersion == EarlyLandEncounterRules.SafariRulesVersion && SafariLandEncounterTables.WildSpecies.Contains(species))
            return SafariWildHeldItemRules.Assign(species, globalRng);
        if (rulesVersion is EarlyLandEncounterRules.CeladonApproachRulesVersion or EarlyLandEncounterRules.CeladonGymRulesVersion && CeladonApproachLandEncounterTables.WildSpecies.Contains(species)) return CeladonApproachWildPokemonFactory.AssignHeldItem(species,globalRng);
        if (rulesVersion is EarlyLandEncounterRules.RockTunnelRulesVersion or EarlyLandEncounterRules.CeladonApproachRulesVersion or EarlyLandEncounterRules.CeladonGymRulesVersion && RockTunnelLandEncounterTables.WildSpecies.Contains(species)) return RockTunnelWildPokemonFactory.AssignHeldItem(species,globalRng);
        if(rulesVersion is EarlyLandEncounterRules.Route11RulesVersion or EarlyLandEncounterRules.RockTunnelRulesVersion or EarlyLandEncounterRules.CeladonApproachRulesVersion or EarlyLandEncounterRules.CeladonGymRulesVersion && Route11LandEncounterTables.WildSpecies.Contains(species)) return Route11WildPokemonFactory.AssignHeldItem(species,globalRng);
        if (!EarlyLandEncounterRules.WildSpecies.Contains(species) &&
            !(rulesVersion is EarlyLandEncounterRules.Route22RulesVersion or EarlyLandEncounterRules.Route3RulesVersion or EarlyLandEncounterRules.MtMoonRulesVersion or EarlyLandEncounterRules.BillRulesVersion or EarlyLandEncounterRules.VermilionRulesVersion or EarlyLandEncounterRules.SsAnneRulesVersion or EarlyLandEncounterRules.Route11RulesVersion or EarlyLandEncounterRules.RockTunnelRulesVersion or EarlyLandEncounterRules.CeladonApproachRulesVersion or EarlyLandEncounterRules.CeladonGymRulesVersion && EarlyLandEncounterRules.Route22WildSpecies.Contains(species)) &&
            !(rulesVersion is EarlyLandEncounterRules.Route3RulesVersion or EarlyLandEncounterRules.MtMoonRulesVersion or EarlyLandEncounterRules.BillRulesVersion or EarlyLandEncounterRules.VermilionRulesVersion or EarlyLandEncounterRules.SsAnneRulesVersion or EarlyLandEncounterRules.Route11RulesVersion or EarlyLandEncounterRules.RockTunnelRulesVersion or EarlyLandEncounterRules.CeladonApproachRulesVersion or EarlyLandEncounterRules.CeladonGymRulesVersion && EarlyLandEncounterRules.Route3WildSpecies.Contains(species)) &&
            !(rulesVersion is EarlyLandEncounterRules.MtMoonRulesVersion or EarlyLandEncounterRules.BillRulesVersion or EarlyLandEncounterRules.VermilionRulesVersion or EarlyLandEncounterRules.SsAnneRulesVersion or EarlyLandEncounterRules.Route11RulesVersion or EarlyLandEncounterRules.RockTunnelRulesVersion or EarlyLandEncounterRules.CeladonApproachRulesVersion or EarlyLandEncounterRules.CeladonGymRulesVersion && MtMoonLandEncounterTables.WildSpecies.Contains(species)) &&
            !(rulesVersion is EarlyLandEncounterRules.BillRulesVersion or EarlyLandEncounterRules.VermilionRulesVersion or EarlyLandEncounterRules.SsAnneRulesVersion or EarlyLandEncounterRules.Route11RulesVersion or EarlyLandEncounterRules.RockTunnelRulesVersion or EarlyLandEncounterRules.CeladonApproachRulesVersion or EarlyLandEncounterRules.CeladonGymRulesVersion && BillLandEncounterTables.WildSpecies.Contains(species)) &&
            !(rulesVersion is EarlyLandEncounterRules.VermilionRulesVersion or EarlyLandEncounterRules.SsAnneRulesVersion or EarlyLandEncounterRules.Route11RulesVersion or EarlyLandEncounterRules.RockTunnelRulesVersion or EarlyLandEncounterRules.CeladonApproachRulesVersion or EarlyLandEncounterRules.CeladonGymRulesVersion && VermilionLandEncounterTables.WildSpecies.Contains(species)))
            throw new NotSupportedException("Only the explicitly admitted wild species' verified held-item tables are supported.");
        // SetWildMonHeldItem draws before testing whether the two slots are equal.
        var draw = Gen3Rng.Next(globalRng, "wild-battle.held-item", 100);
        string heldItem = species.Value switch
        {
            "pret:SPECIES_MEOWTH" when rulesVersion is EarlyLandEncounterRules.VermilionRulesVersion or EarlyLandEncounterRules.SsAnneRulesVersion or EarlyLandEncounterRules.Route11RulesVersion or EarlyLandEncounterRules.RockTunnelRulesVersion or EarlyLandEncounterRules.CeladonApproachRulesVersion or EarlyLandEncounterRules.CeladonGymRulesVersion && draw.Trace.Result >= 95 => "ITEM_NUGGET",
            "pret:SPECIES_JIGGLYPUFF" when draw.Trace.Result is >= 45 and <= 94 => "ITEM_ORAN_BERRY",
            "pret:SPECIES_PARAS" when draw.Trace.Result is >= 45 and <= 94 => "ITEM_TINY_MUSHROOM",
            "pret:SPECIES_PARAS" when draw.Trace.Result >= 95 => "ITEM_BIG_MUSHROOM",
            "pret:SPECIES_CLEFAIRY" when draw.Trace.Result >= 95 => "ITEM_MOON_STONE",
            _ => "ITEM_NONE"
        };
        return new(heldItem, draw.State, [draw.Trace]);
    }
}
