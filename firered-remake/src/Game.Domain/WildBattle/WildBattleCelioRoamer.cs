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
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool OpponentRunSelected { get; init; }
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

    /// <summary>Only produces a settlement receipt. SourceRoamerRules.Settle owns relocation/deactivation.</summary>
    public static RoamerBattleResult Settlement(PokemonCatalog catalog, WildBattleState state)
    {
        if (WildBattleRules.Validate(catalog, state) is not null || state.SourceWild?.CelioRoamer is not { SourceOutcome: { } outcome } origin ||
            state.Outcome == WildBattleOutcome.Ongoing || state.SourceWild.Turn is not null)
            throw new ArgumentException("celio_roamer_settlement_phase");
        var actual = state.CalledParty!.Opponent[0];
        return new(origin.Individual, actual.Pokemon.CurrentHp, actual.Status, outcome);
    }
}

public static partial class WildBattleRules
{
    /// <summary>Called only after the authoritative world owner admits this active roamer opportunity.</summary>
    public static WildBattleResult StartCelioRoamer(CelioOwnedPokemonCatalog owned, RoamerIndividual individual,
        uint originalTrainerId, ImmutableArray<WildBattleCombatant> playerParty, int activeIndex, RngState rng,
        WildBattleTerrain terrain = WildBattleTerrain.Grass)
    {
        if (owned is null || SourceBattleAdmission.ValidateOwnedEntry(owned, playerParty, activeIndex) is { })
            return Fail(null, "celio_roamer_owned_entry");
        WildBattleCombatant opponent;
        try { opponent = CelioRoamerBattleContract.Restore(owned, individual, originalTrainerId); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        { return Fail(null, "celio_roamer_individual"); }
        if (SourceBattleAdmission.ValidateEntry(owned.Mechanics, BattleRulesProfile.CanonicalSourceSingles,
            playerParty, activeIndex, opponent) is { } error) return Fail(null, error);
        return StartSourceWildCore(owned.Mechanics.Catalog, playerParty, activeIndex, opponent, rng, terrain,
            false, null, false, BattleRulesProfile.CanonicalSourceSingles, owned,
            celioRoamer: new(individual, originalTrainerId));
    }

    private static string? ValidateCelioRoamer(WildBattleState state)
    {
        var wild = state.SourceWild;
        if (wild is null) return null;
        if ((wild.RulesVersion == CelioRoamerBattleContract.Version) != (wild.CelioRoamer is not null))
            return "celio_roamer_required_origin";
        if (wild.CelioRoamer is not { } origin) return null;
        if (state.SourceCampaignRulesVersion != CelioOwnedPokemonCatalog.Profile || wild.VictoryRoad is not null ||
            wild.SeviiRuby is not null || wild.FourIsland is not null || wild.SixIsland is not null || wild.FiveIsland is not null ||
            state.ScriptedSnorlax || state.TowerBattleKind is not null ||
            state.CalledParty is not { BattleKind: WildBattleCalledBattleKind.Wild, Opponent.Length: 1 } party ||
            !CelioRoamerBattleContract.MatchesIdentity(origin, party.Opponent[0].Pokemon) ||
            !CelioRoamerBattleContract.MatchesOutcome(origin, state.Outcome) ||
            origin.OpponentRunSelected && wild.Turn is null)
            return "celio_roamer_origin";
        return null;
    }

    internal sealed partial class Execution
    {
        private int ChooseCelioRoamerAction(PokemonCatalog catalog)
        {
            // SetUpBattleVars has its own setup. Each OpponentHandleChooseMove calls it again.
            for (int i = 0; i < 4; i++) Draw($"roamer.ai-setup.slot-{i}", 16);
            var mon = State.Opponent;
            // AI_Roaming checks Wrap, escape prevention, Shadow Tag and Levitate/Arena Trap.
            // Status1, Ingrain and Magnet Pull are deliberately not additional flee guards.
            bool trapped = mon.BindTurnsRemaining > 0 || mon.MeanLookSource is not null ||
                WildBattleEffective.Ability(State.Player) == "ABILITY_SHADOW_TAG" ||
                WildBattleEffective.Ability(mon) != "ABILITY_LEVITATE" && WildBattleEffective.Ability(State.Player) == "ABILITY_ARENA_TRAP";
            if (!trapped)
            {
                State = State with { SourceWild = State.SourceWild! with {
                    CelioRoamer = State.SourceWild!.CelioRoamer! with { OpponentRunSelected = true } } };
                return StruggleSlot; // MOVE_NONE priority zero; the typed receipt distinguishes RUN.
            }
            // AI_Roaming leaves every legal move at score100. Reuse the canonical move-limit checks.
            var legal = Enumerable.Range(0, mon.Pokemon.Moves.Length)
                .Where(slot => ValidateSourceMoveSelection(catalog, State, WildBattleSide.Opponent, slot) is null).ToArray();
            if (legal.Length == 0) throw new UnsupportedAction("celio_roamer_no_legal_ai_move");
            return legal[Draw("roamer.ai.choose-best-move", legal.Length) % legal.Length];
        }

        private void ExecuteCelioRoamerRun()
        {
            // HandleAction_Run checks these two volatile flags again after a faster player's move.
            bool escaped = State.Opponent.BindTurnsRemaining == 0 && State.Opponent.MeanLookSource is null;
            Add(n => new WildBattleRoamerRunEvent(n, State.Turn, escaped));
            if (!escaped) return;
            State = State with { Outcome = WildBattleOutcome.Escaped, AwaitingReplacement = false };
            SetCelioRoamerOutcome(RoamerBattleOutcome.MonFled);
            Add(n => new WildBattleEndedEvent(n, State.Turn, WildBattleOutcome.Escaped));
        }

        private void SetCelioRoamerOutcome(RoamerBattleOutcome outcome)
        {
            if (State.SourceWild?.CelioRoamer is not { } origin) return;
            State = State with { SourceWild = State.SourceWild with { CelioRoamer = origin with { SourceOutcome = outcome } } };
        }

        private void CompleteCelioRoamerOutcome()
        {
            if (State.SourceWild?.CelioRoamer is not { } origin) return;
            var outcome = State.Outcome switch
            {
                WildBattleOutcome.Won => RoamerBattleOutcome.Won,
                WildBattleOutcome.Lost => RoamerBattleOutcome.Lost,
                WildBattleOutcome.Draw => RoamerBattleOutcome.Drew,
                WildBattleOutcome.Caught => RoamerBattleOutcome.Caught,
                WildBattleOutcome.Escaped => origin.SourceOutcome ?? RoamerBattleOutcome.Ran,
                _ => (RoamerBattleOutcome?)null
            };
            State = State with { SourceWild = State.SourceWild with {
                CelioRoamer = origin with { SourceOutcome = outcome, OpponentRunSelected = false } } };
        }
    }
}
