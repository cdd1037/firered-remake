using System.Collections.Immutable;
using Game.Domain.Capture;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Safari;

/// <summary>
/// Pinned FireRed Safari action selection/AI, player action, opposing action and round boundary.
/// Source: battle_main.c, battle_ai_script_commands.c, battle_script_commands.c,
/// battle_controller_opponent.c and data/battle_scripts_2.s. Rendering/VBlank and encounter/intro
/// RNG are outside this contract, as are party/PC mutation and player-save policy.
/// </summary>
public static class SafariBattleRules
{
    public const string RulesVersion = "firered-safari-battle-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const int InitialBallCount = 30;

    /// <summary>
    /// Input RNG is immediately before TryDoEventsBeforeFirstTurn's final gRandomTurnNumber draw.
    /// Earlier encounter generation, controller setup, intro and speed-order RNG belong to the caller.
    /// Each encounter starts with fresh catch/flee factors and counters; the visit ball pool persists.
    /// </summary>
    public static SafariBattleResult Start(SourceMechanicsCatalog mechanics, PokemonInstance target,
        int remainingBalls, RngState rng)
    {
        string? fault = ValidateTarget(mechanics, target);
        if (fault is not null) return Rejected(null, fault);
        if (remainingBalls is < 1 or > InitialBallCount) return Rejected(null, "safari-ball-pool");
        if (rng.AlgorithmId != Gen3Rng.AlgorithmId) return Rejected(null, "safari-rng-algorithm");
        if (rng.Cursor == ulong.MaxValue) return Rejected(null, "safari-rng-cursor-exhausted");
        var draw = Gen3Rng.Next(rng, "safari:turn-random");
        int catchFactor = mechanics.Catalog.Species[target.Species].CatchRate * 100 / 1275;
        int escapeFactor = Math.Max(2, SafariSpeciesRules.FleeRate(target.Species)!.Value * 100 / 1275);
        return new(true, null, new(target, remainingBalls, catchFactor, escapeFactor,
            0, 0, 0, SafariBattleOutcome.Active, draw.State), [], [draw.Trace]);
    }

    /// <summary>
    /// Resolves a legal menu action atomically. Capacity false rejects Ball at selection, exactly
    /// where HandleTurnActionSelectionState checks IsPlayerPartyAndPokemonStorageFull; it does not
    /// discard a caught Pokemon. Other actions remain usable. Application supplies current capacity.
    /// </summary>
    public static SafariBattleResult Apply(SourceMechanicsCatalog mechanics, SafariBattleState state,
        SafariAction action, bool canReceiveCaughtPokemon = true)
    {
        string? fault = Validate(mechanics, state);
        if (fault is not null) return Rejected(state, fault);
        if (!Enum.IsDefined(action)) return Rejected(state, "safari-action");
        if (state.Outcome != SafariBattleOutcome.Active) return Rejected(state, "safari-battle-ended");
        if (action == SafariAction.Ball && !canReceiveCaughtPokemon)
            return Rejected(state, "safari-party-and-storage-full");

        RngState rng = state.Rng;
        var traces = ImmutableArray.CreateBuilder<RandomTrace>();
        var events = ImmutableArray.CreateBuilder<SafariBattleEvent>();
        RngDraw Draw(string purpose, int bound = 65536)
        {
            var draw = Gen3Rng.Next(rng, purpose, bound);
            rng = draw.State;
            traces.Add(draw.Trace);
            return draw;
        }
        try
        {
            // OpponentHandleChooseMove -> SetupAIData: these four draws occur even though their
            // simulated damage is unused by AI_Safari. The foe chooses before either action runs.
            for (int slot = 0; slot < 4; slot++) Draw($"safari:ai-simulated-rng-{slot + 1}", 16);
            int chance = FleeChancePercent(state);
            int roll = Draw("safari:flee-decision", 100).Trace.Result;
            var decision = new SafariFleeDecision(chance, roll, roll < chance);
            var next = state;
            events.Add(new SafariActionEvent(action, action == SafariAction.Ball
                ? state.BallsRemaining - 1 : state.BallsRemaining));

            switch (action)
            {
                case SafariAction.Run:
                    next = next with { Outcome = SafariBattleOutcome.Ran };
                    break;
                case SafariAction.Bait:
                case SafariAction.Rock:
                    int added = Draw(action == SafariAction.Bait ? "safari:bait-duration" : "safari:rock-duration", 5).Trace.Result + 2;
                    next = action == SafariAction.Bait
                        ? next with { EatingTurns = Math.Min(6, next.EatingTurns + added), AngryTurns = 0,
                            CatchFactor = Math.Max(3, next.CatchFactor >> 1) }
                        : next with { AngryTurns = Math.Min(6, next.AngryTurns + added), EatingTurns = 0,
                            CatchFactor = Math.Min(20, next.CatchFactor << 1) };
                    events.Add(new SafariMoodChangedEvent(action, added, next.EatingTurns, next.AngryTurns, next.CatchFactor));
                    break;
                case SafariAction.Ball:
                    next = next with { BallsRemaining = next.BallsRemaining - 1 };
                    // Safari and Great are BOTH 15/10 in sBallCatchBonuses. Reuse only the existing
                    // integer catch/shake primitive with Safari's reconstructed u8 catch rate.
                    // Ordinary Safari admission remains rejected by CaptureRules itself.
                    int effectiveCatchRate = next.CatchFactor * 1275 / 100;
                    var capture = CaptureRules.Resolve(new CaptureInput(next.Target.Stats.MaxHp,
                        next.Target.CurrentHp, effectiveCatchRate, CaptureBall.Great), rng);
                    if (capture.Outcome is not (CaptureOutcome.Caught or CaptureOutcome.Escaped))
                        return Rejected(state, "safari-capture:" + capture.Diagnostic);
                    rng = capture.Rng;
                    traces.AddRange(capture.RandomTrace);
                    events.Add(new SafariCaptureEvent(capture, next.BallsRemaining));
                    if (capture.Outcome == CaptureOutcome.Caught)
                        next = next with { Outcome = SafariBattleOutcome.Caught };
                    else if (next.BallsRemaining == 0)
                        next = next with { Outcome = SafariBattleOutcome.OutOfBalls };
                    break;
            }

            // Caught, ran, and failed-last-ball outcomes take precedence over the preselected foe
            // action. An enemy flee does not execute WatchesCarefully or decrement either counter.
            if (next.Outcome == SafariBattleOutcome.Active)
            {
                if (decision.SelectedFlee)
                    next = next with { Outcome = SafariBattleOutcome.Fled };
                else
                {
                    SafariReaction reaction = SafariReaction.Watching;
                    if (next.AngryTurns != 0)
                    {
                        next = next with { AngryTurns = next.AngryTurns - 1 };
                        if (next.AngryTurns == 0)
                            next = next with { CatchFactor = mechanics.Catalog.Species[next.Target.Species].CatchRate * 100 / 1275 };
                        else reaction = SafariReaction.Angry;
                    }
                    else if (next.EatingTurns != 0)
                    {
                        next = next with { EatingTurns = next.EatingTurns - 1 };
                        if (next.EatingTurns != 0) reaction = SafariReaction.Eating;
                    }
                    events.Add(new SafariReactionEvent(reaction, next.EatingTurns, next.AngryTurns, next.CatchFactor));
                    next = next with { BattleTurnCounter = Math.Min(255, next.BattleTurnCounter + 1) };
                    Draw("safari:turn-random");
                }
            }
            if (next.Outcome != SafariBattleOutcome.Active) events.Add(new SafariBattleEndedEvent(next.Outcome));
            return new(true, null, next with { Rng = rng }, events.ToImmutable(), traces.ToImmutable(), decision);
        }
        catch (OverflowException)
        {
            return Rejected(state, "safari-rng-cursor-exhausted");
        }
    }

    public static int FleeChancePercent(SafariBattleState state) => 5 * (state.AngryTurns != 0
        ? Math.Min(20, state.EscapeFactor * 2)
        : state.EatingTurns != 0 ? Math.Max(1, state.EscapeFactor / 4) : state.EscapeFactor);

    public static string? Validate(SourceMechanicsCatalog mechanics, SafariBattleState? state)
    {
        if (state is null) return "safari-state";
        string? fault = ValidateTarget(mechanics, state.Target);
        if (fault is not null) return fault;
        if (!Enum.IsDefined(state.Outcome)) return "safari-outcome";
        if (state.BallsRemaining is < 0 or > InitialBallCount ||
            state.Outcome == SafariBattleOutcome.Active && state.BallsRemaining == 0 ||
            state.Outcome is SafariBattleOutcome.Fled or SafariBattleOutcome.Ran && state.BallsRemaining == 0 ||
            state.Outcome == SafariBattleOutcome.OutOfBalls && state.BallsRemaining != 0)
            return "safari-ball-pool";
        if (state.CatchFactor is < 2 or > 20 || state.EscapeFactor !=
            Math.Max(2, SafariSpeciesRules.FleeRate(state.Target.Species)!.Value * 100 / 1275))
            return "safari-factors";
        if (state.EatingTurns is < 0 or > 6 || state.AngryTurns is < 0 or > 6 ||
            state.EatingTurns != 0 && state.AngryTurns != 0 || state.BattleTurnCounter is < 0 or > 255)
            return "safari-counters";
        if (state.Rng.AlgorithmId != Gen3Rng.AlgorithmId) return "safari-rng-algorithm";
        return null;
    }

    private static string? ValidateTarget(SourceMechanicsCatalog? mechanics, PokemonInstance? target)
    {
        if (mechanics is null) return "safari-source-mechanics";
        string? fault = SourcePokemonValidator.ValidatePermanent(mechanics, target);
        if (fault is not null) return "safari-target:" + fault;
        if (SafariSpeciesRules.FleeRate(target!.Species) is null) return "safari-unsupported-species";
        if (target.CurrentHp != target.Stats.MaxHp) return "safari-target-not-healthy";
        if (!target.Moves.Any(m => m.CurrentPp > 0)) return "safari-target-no-usable-move";
        return null;
    }

    private static SafariBattleResult Rejected(SafariBattleState? state, string fault) =>
        new(false, fault, state, [], []);
}
