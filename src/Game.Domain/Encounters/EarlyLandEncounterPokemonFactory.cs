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

}
