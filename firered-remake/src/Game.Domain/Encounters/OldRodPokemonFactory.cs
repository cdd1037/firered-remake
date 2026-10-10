using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Encounters;

/// <summary>Original ordinary CreateMonWithNature/PID rejection/IV order; battle start is a separate commit.</summary>
public static class OldRodPokemonFactory
{
    public static OldRodPokemonResult Generate(PokemonCatalog catalog, OldRodEncounterCandidate candidate,
        RngState rng, uint playerTrainerId, bool route11Rules=false, bool rockTunnelRules=false, bool celadonApproachRules=false, bool celadonGymRules=false)
    {
        OldRodEncounterRules.ValidateCandidate(candidate);
        PokemonFactory.ValidateCatalog(catalog);
        if (catalog.Species.Count != (celadonGymRules?98:celadonApproachRules?94:rockTunnelRules?93:route11Rules?79:73) || !catalog.Species.ContainsKey(candidate.Species))
            throw new NotSupportedException("Old Rod requires the authenticated 73-species S.S. Anne catalog.");
        if (rng.AlgorithmId != Gen3Rng.AlgorithmId) throw new NotSupportedException("Unknown global RNG algorithm.");
        var result = Route1WildPokemonFactory.GenerateOrdinary(catalog, candidate.Species, candidate.Level, rng, playerTrainerId);
        return new(result.Pokemon, result.GlobalRng, result.RandomTrace);
    }

    /// <summary>SetWildMonHeldItem draws Random()%100 even when Magikarp's two item slots are both NONE.</summary>
    public static OldRodHeldItemResult AssignHeldItem(OldRodEncounterCandidate candidate, RngState rng)
    {
        OldRodEncounterRules.ValidateCandidate(candidate);
        var draw = Gen3Rng.Next(rng, "wild-battle.held-item", 100);
        return new("ITEM_NONE", draw.State, [draw.Trace]);
    }
}
