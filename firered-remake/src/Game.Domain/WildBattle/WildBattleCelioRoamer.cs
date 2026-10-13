using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Roaming;

namespace Game.Domain.WildBattle;

/// <summary>The exact saved individual at encounter entry, not a new generated encounter slot.
/// SourceOutcome retains source result numbers independently of the historical battle enum.</summary>
public sealed record CelioRoamerBattleOrigin(RoamerIndividual Individual, uint OriginalTrainerId)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public RoamerBattleOutcome? SourceOutcome { get; init; }
}

public sealed record WildBattleRoamerRunEvent(long Sequence, int Turn, bool Escaped) : WildBattleEvent(Sequence, Turn);

public static class CelioRoamerBattleContract
{
    public const string Version = "firered-celio-roamer-battle-v1";

    /// <summary>Restore fixed PID and all six stored IVs, zero EVs and fresh level-50 moves/PP.
    /// This operation has no RNG argument and consumes no random values.</summary>
    public static WildBattleCombatant Restore(CelioOwnedPokemonCatalog owned, RoamerIndividual individual, uint originalTrainerId)
    {
        ArgumentNullException.ThrowIfNull(owned);
        if (!ReferenceEquals(CelioOwnedPokemonCatalog.For(owned.Catalog), owned)) throw new ArgumentException("celio_roamer_catalog");
        var restored = SourceRoamerRules.RestoreEncounter(individual);
        var mon = SourcePokemonFactory.Create(owned.Mechanics, new(new SpeciesId(individual.Species), individual.Level,
            originalTrainerId, IvPolicy.Fixed(0), PersonalityPolicy.Fixed(individual.Personality)), new RngState(0)).Pokemon;
        var iv = restored.Ivs;
        var ivs = new SixStats(iv[0], iv[1], iv[2], iv[3], iv[4], iv[5]);
        var stats = PokemonStatCalculation.Calculate(owned.Catalog.Species[mon.Species], mon.Level, mon.Nature,
            ivs, SixStats.Zero, owned.Mechanics);
        if (stats.MaxHp != restored.MaximumHp) throw new ArgumentException("celio_roamer_max_hp");
        return new(mon with { Ivs = ivs, Stats = stats, CurrentHp = individual.CurrentHp }, Status: individual.Status);
    }

    internal static bool MatchesIdentity(CelioRoamerBattleOrigin origin, PokemonInstance mon)
    {
        try { SourceRoamerRules.ValidateIndividual(origin.Individual); }
        catch (ArgumentException) { return false; }
        var i = origin.Individual;
        uint packed = (uint)(mon.Ivs.Hp | mon.Ivs.Attack << 5 | mon.Ivs.Defense << 10 |
            mon.Ivs.Speed << 15 | mon.Ivs.SpecialAttack << 20 | mon.Ivs.SpecialDefense << 25);
        return i.Active && i.CurrentHp > 0 && mon.Species.Value == i.Species && mon.Level == i.Level &&
            mon.Personality == i.Personality && packed == i.PackedIvs && mon.Evs == SixStats.Zero &&
            mon.OriginalTrainerId == origin.OriginalTrainerId;
    }

    internal static bool MatchesOutcome(CelioRoamerBattleOrigin origin, WildBattleOutcome outcome) => outcome switch
    {
        WildBattleOutcome.Ongoing => origin.SourceOutcome is null,
        WildBattleOutcome.Won => origin.SourceOutcome == RoamerBattleOutcome.Won,
        WildBattleOutcome.Lost => origin.SourceOutcome == RoamerBattleOutcome.Lost,
        WildBattleOutcome.Draw => origin.SourceOutcome == RoamerBattleOutcome.Drew,
        WildBattleOutcome.Caught => origin.SourceOutcome == RoamerBattleOutcome.Caught,
        WildBattleOutcome.Escaped => origin.SourceOutcome is RoamerBattleOutcome.Ran or RoamerBattleOutcome.MonFled or RoamerBattleOutcome.PlayerTeleported,
        _ => false
    };

}
