using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Encounters;

public sealed record Route1WildPokemonResult(PokemonInstance Pokemon, RngState GlobalRng,
    ImmutableArray<RandomTrace> RandomTrace);
public sealed record Route1WildHeldItemResult(string HeldItem, RngState GlobalRng,
    ImmutableArray<RandomTrace> RandomTrace);

/// <summary>
/// GenerateWildMon's ordinary Route 1 instance projection. Does not start a battle, clear Pending,
/// assign a held item, touch the rate RNG, advance VBlank, or mutate the player's party.
/// </summary>
public static class Route1WildPokemonFactory
{
    public const string RulesVersion = "firered-route1-wild-generation-v1";

    public static Route1WildPokemonResult Generate(PokemonCatalog catalog, Route1EncounterState encounter,
        RngState globalRng, uint playerTrainerId)
    {
        Route1EncounterRules.ValidateState(encounter);
        if (encounter.Pending is not {} pending)
            throw new InvalidOperationException("A verified pending Route 1 candidate is required.");
        PokemonFactory.ValidateCatalog(catalog);
        // Later authenticated catalogs retain the complete Route 1 families.
        // Route1EncounterRules still restricts the pending species/table.
        if (!catalog.Species.ContainsKey(pending.Candidate.Species))
            throw new NotSupportedException("The complete Route 1 Pokemon catalog is required.");
        if (globalRng.AlgorithmId != Gen3Rng.AlgorithmId)
            throw new NotSupportedException("Unknown global RNG algorithm.");

        return GenerateOrdinary(catalog, pending.Candidate.Species, pending.Candidate.Level, globalRng, playerTrainerId);
    }

    // Shared with the separately validated early-Kanto entry point.
    internal static Route1WildPokemonResult GenerateOrdinary(PokemonCatalog catalog, SpeciesId species, byte level,
        RngState globalRng, uint playerTrainerId)
    {
        // Every draw remains local until the complete instance and trace can be returned together.
        var trace = ImmutableArray.CreateBuilder<RandomTrace>();
        int Draw(string purpose,int bound=65536)
        {
            var draw=Gen3Rng.Next(globalRng,purpose,bound);
            globalRng=draw.State;trace.Add(draw.Trace);return draw.Trace.Result;
        }
        int nature=Draw("wild.nature",25);
        uint personality;ulong attempt=0;
        do
        {
            attempt=checked(attempt+1);
            string prefix="wild.personality."+attempt.ToString(System.Globalization.CultureInfo.InvariantCulture);
            uint low=(uint)Draw(prefix+".low16");
            uint high=(uint)Draw(prefix+".high16");
            personality=low|(high<<16);
        }while(personality%25!=nature);

        // The fixed-PID path consumes only the two original IV draws and shares all deterministic
        // stats, gender, ability, experience and initial-move rules with the existing instance model.
        var created=PokemonFactory.Create(catalog,new(species,level,
            playerTrainerId,IvPolicy.Random,PersonalityPolicy.Fixed(personality)),globalRng);
        if(created.Trace.Length!=2)throw new InvalidOperationException("Fixed-PID creation RNG contract changed.");
        trace.AddRange(created.Trace.Select(draw=>draw with{Purpose="wild."+draw.Purpose["pokemon.".Length..]}));
        return new(created.Pokemon,created.Rng,trace.ToImmutable());
    }

    /// <summary>
    /// Separate SetWildMonHeldItem call boundary for these two species. Even two ITEM_NONE slots
    /// consume Random()%100. The caller owns when battle initialization reaches this function.
    /// </summary>
    public static Route1WildHeldItemResult AssignHeldItem(SpeciesId species,RngState globalRng)
    {
        if(species.Value is not ("pret:SPECIES_PIDGEY" or "pret:SPECIES_RATTATA"))
            throw new NotSupportedException("Only the two verified Route 1 empty held-item tables are supported.");
        var draw=Gen3Rng.Next(globalRng,"wild-battle.held-item",100);
        return new("ITEM_NONE",draw.State,[draw.Trace]);
    }
}
