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

    public static EarlyLandWildPokemonResult Generate(PokemonCatalog catalog, EarlyLandEncounterTable table,
        EarlyLandEncounterState encounter, RngState globalRng, uint playerTrainerId, bool ceruleanRules = false, bool billRules = false, bool vermilionRules = false, bool ssAnneRules = false, bool route11Rules = false, bool rockTunnelRules = false, bool celadonApproachRules = false, bool celadonGymRules = false, bool rocketHideoutRules = false, bool pokemonTowerRules = false, bool snorlaxRules = false)
    {
        if ((catalog.Species.Count == 102) != snorlaxRules) throw new NotSupportedException("Explicit Snorlax catalog profile required.");
        pokemonTowerRules |= snorlaxRules;
        rocketHideoutRules |= pokemonTowerRules;
        celadonGymRules |= rocketHideoutRules;
        EarlyLandEncounterRules.ValidateState(encounter, table);
        var pending = encounter.Pending ?? throw new InvalidOperationException("A verified pending early land candidate is required.");
        PokemonFactory.ValidateCatalog(catalog);
        bool catalogAdmitted = snorlaxRules ? catalog.Species.Count == 102 && encounter.RulesVersion == EarlyLandEncounterRules.SnorlaxRulesVersion : pokemonTowerRules ? catalog.Species.Count == 101 && encounter.RulesVersion == EarlyLandEncounterRules.PokemonTowerRulesVersion : rocketHideoutRules ? catalog.Species.Count == 99 && encounter.RulesVersion == EarlyLandEncounterRules.RocketHideoutRulesVersion : celadonGymRules ? catalog.Species.Count == 98 && encounter.RulesVersion == EarlyLandEncounterRules.CeladonGymRulesVersion : celadonApproachRules ? catalog.Species.Count == 94 && encounter.RulesVersion == EarlyLandEncounterRules.CeladonApproachRulesVersion : rockTunnelRules ? catalog.Species.Count == 93 && encounter.RulesVersion == EarlyLandEncounterRules.RockTunnelRulesVersion : route11Rules ? catalog.Species.Count == 79 && encounter.RulesVersion == EarlyLandEncounterRules.Route11RulesVersion : ssAnneRules ? catalog.Species.Count == 73 && encounter.RulesVersion == EarlyLandEncounterRules.SsAnneRulesVersion : vermilionRules ? catalog.Species.Count == 70 && encounter.RulesVersion == EarlyLandEncounterRules.VermilionRulesVersion : billRules ? catalog.Species.Count == 68 && encounter.RulesVersion == EarlyLandEncounterRules.BillRulesVersion : ceruleanRules
            ? catalog.Species.Count == 65 && encounter.RulesVersion == EarlyLandEncounterRules.MtMoonRulesVersion
            : encounter.RulesVersion == EarlyLandEncounterRules.MtMoonRulesVersion
            ? catalog.Species.Count == 56
            : encounter.RulesVersion == EarlyLandEncounterRules.Route3RulesVersion
            ? catalog.Species.Count == 40
            : catalog.Species.Count is 22 or 29 or 32 &&
                (encounter.RulesVersion != EarlyLandEncounterRules.Route22RulesVersion || catalog.Species.Count == 32);
        if (!catalogAdmitted || !catalog.Species.ContainsKey(pending.Candidate.Species))
            throw new NotSupportedException("An authenticated catalog matching the walking profile is required (Route 22: 32; Route 3: 40; MtMoon: 56 species).");
        if (globalRng.AlgorithmId != Gen3Rng.AlgorithmId)
            throw new NotSupportedException("Unknown global RNG algorithm.");
        var result = Route1WildPokemonFactory.GenerateOrdinary(catalog, pending.Candidate.Species,
            pending.Candidate.Level, globalRng, playerTrainerId);
        return new(result.Pokemon, result.GlobalRng, result.RandomTrace);
    }

    /// <summary>Source SetWildMonHeldItem always draws once; FireRed has no Compound Eyes held-item modifier.</summary>
    public static EarlyLandWildHeldItemResult AssignHeldItem(SpeciesId species, RngState globalRng,
        string rulesVersion = EarlyLandEncounterRules.RulesVersion)
    {
        EarlyLandEncounterRules.ValidateRulesVersion(rulesVersion);
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
