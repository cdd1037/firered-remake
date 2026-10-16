using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.FirstBattle;

/// <summary>
/// Pinned FireRed first-lab-battle four owned moves plus Struggle, using semantic RNG (no VBlank draws).
/// AI selection occurs separately, before resolving a turn, on the same state.Rng stream.
/// A successful externally applied Potion may replace the player's move; inventory remains the caller's operation.
/// Rewards, evolution, other items, switching, escape and overworld return are not this rule's operations.
/// </summary>
public static class FirstBattleRules
{
    public const string RulesVersion = "firered-first-lab-potion-semantic-v3";
    /// <summary>Automatic fallback only when every owned move has zero PP; it is not an owned move slot.</summary>
    public const int StruggleSlot = -1;
    public static MoveId StruggleMove { get; } = new("pret:MOVE_STRUGGLE");
    private const string SourceCommit = PokemonFactory.SourceCommit;

    public static FirstBattleResult Start(PokemonCatalog catalog, FirstBattleCombatant player,
        FirstBattleCombatant opponent, RngState rng)
    {
        var error = ValidateCombatant(catalog, player, "player") ?? ValidateCombatant(catalog, opponent, "opponent")
            ?? ValidateRng(rng);
        if (error is not null) return Fail(null, error);
        if (player.AttackStage != 6 || player.DefenseStage != 6 || opponent.AttackStage != 6 || opponent.DefenseStage != 6)
            return Fail(null, "first_battle_entry_stat_stages");
        if (player.Pokemon.CurrentHp == 0) return Fail(null, "first_battle_player_fainted");
        if (opponent.Pokemon.CurrentHp == 0) return Fail(null, "first_battle_opponent_fainted");
        if (!RivalPair(player.Pokemon.SourceSpeciesId, opponent.Pokemon.SourceSpeciesId))
            return Fail(null, "first_battle_rival_pair_unsupported");
        var state = new FirstBattleState(player, opponent, FirstBattleTutorialFlags.None, 0, 0, 0, rng, 0, FirstBattleOutcome.Ongoing);
        var context = new Execution(state);
        try
        {
            // SetUpBattleVars performs setup once before the first real chooseMove setup.
            for (int slot = 0; slot < 4; slot++) context.Draw($"first-battle.entry.ai-setup.slot-{slot}", 16);
            context.Add(n => new FirstBattleStartedEvent(n, 0));
            context.Order(FirstBattleOrderPhase.Entry, 0, 0);
            ushort number = context.Draw("first-battle.entry.random-turn-number");
            context.State = context.State with { RandomTurnNumber = number };
            context.Add(n => new FirstBattleTurnReadyEvent(n, 0, number));
            return context.Success();
        }
        catch (OverflowException) { return Fail(null, "first_battle_counter_exhausted"); }
    }

    /// <summary>Supply selected owned slots, or StruggleSlot for an exhausted side. Skip opponent AI entirely
    /// when it requires Struggle; otherwise supply state.Rng after that AI choice.</summary>
    public static FirstBattleResult ResolveTurn(PokemonCatalog catalog, FirstBattleState state,
        int playerSlot, int opponentSlot)
    {
        var error = Validate(catalog, state);
        if (error is not null) return Fail(state, error);
        if (state.Outcome != FirstBattleOutcome.Ongoing) return Fail(state, "first_battle_already_ended");
        error = ValidateSelection(state.Player, playerSlot, "player") ?? ValidateSelection(state.Opponent, opponentSlot, "opponent");
        if (error is not null) return Fail(state, error);
        var context = new Execution(state);
        try
        {
            var playerMove = SelectedMove(catalog, state.Player, playerSlot);
            var opponentMove = SelectedMove(catalog, state.Opponent, opponentSlot);
            var first = context.Order(FirstBattleOrderPhase.Actions, playerMove.Priority, opponentMove.Priority);
            context.Move(catalog, first, first == FirstBattleSide.Player ? playerSlot : opponentSlot);
            if (context.State.Outcome == FirstBattleOutcome.Ongoing)
            {
                var second = Other(first);
                context.Move(catalog, second, second == FirstBattleSide.Player ? playerSlot : opponentSlot);
            }
            if (context.State.Outcome == FirstBattleOutcome.Ongoing)
            {
                // DoFieldEndTurnEffects resets order to player/opponent and sorts again, with chosen priorities.
                context.Order(FirstBattleOrderPhase.EndTurn, playerMove.Priority, opponentMove.Priority);
                ushort number = context.Draw("first-battle.turn-end.random-turn-number");
                context.State = context.State with { Turn = Math.Min(255, state.Turn + 1), RandomTurnNumber = number };
                context.Add(n => new FirstBattleTurnReadyEvent(n, context.State.Turn, number));
            }
            context.State = context.State with { Revision = checked(state.Revision + 1) };
            return context.Success();
        }
        catch (OverflowException) { return Fail(state, "first_battle_counter_exhausted"); }
    }

    /// <summary>
    /// Complete one successful Potion action using the exact Pokemon returned by OpeningPotionRules.Use.
    /// State retains pre-Potion HP/stages and carries the RNG after one AI selection against that old state;
    /// opponentSlot is that retained choice (or StruggleSlot, with AI skipped when all opponent PP is zero).
    /// This validates and projects the supplied HP change; it neither heals again nor consumes inventory.
    /// The caller must commit its item result and this result atomically, discarding both on any fault.
    /// </summary>
    public static FirstBattleResult ResolvePotionTurn(PokemonCatalog catalog, FirstBattleState state,
        PokemonInstance healedPlayer, int opponentSlot)
    {
        var error = Validate(catalog, state);
        if (error is not null) return Fail(state, error);
        if (state.Outcome != FirstBattleOutcome.Ongoing) return Fail(state, "first_battle_already_ended");
        error = ValidateSelection(state.Opponent, opponentSlot, "opponent") ?? ValidatePotion(catalog, state.Player.Pokemon, healedPlayer);
        if (error is not null) return Fail(state, error);
        var context = new Execution(state);
        try
        {
            context.Potion(healedPlayer);
            // SetActionsAndBattlersTurnOrder appends item actions first and never compares this pair's speed.
            context.Add(n => new FirstBattleOrderEvent(n, state.Turn, FirstBattleOrderPhase.Actions,
                FirstBattleSide.Player, FirstBattleSide.Opponent));
            // BattleScript_PlayerUseItem is moveendcase 15; end. Attacker == target makes case15 a no-op.
            context.Move(catalog, FirstBattleSide.Opponent, opponentSlot);
            if (context.State.Outcome == FirstBattleOutcome.Ongoing)
            {
                // Item action has MOVE_NONE priority0 at DoFieldEndTurnEffects; this sort still draws ties.
                var opponentMove = SelectedMove(catalog, state.Opponent, opponentSlot);
                context.Order(FirstBattleOrderPhase.EndTurn, 0, opponentMove.Priority);
                ushort number = context.Draw("first-battle.turn-end.random-turn-number");
                context.State = context.State with { Turn = Math.Min(255, state.Turn + 1), RandomTurnNumber = number };
                context.Add(n => new FirstBattleTurnReadyEvent(n, context.State.Turn, number));
            }
            context.State = context.State with { Revision = checked(state.Revision + 1) };
            return context.Success();
        }
        catch (OverflowException) { return Fail(state, "first_battle_counter_exhausted"); }
    }

    private static string? ValidatePotion(PokemonCatalog catalog, PokemonInstance before, PokemonInstance? healed)
    {
        if (before.CurrentHp == 0 || before.CurrentHp == before.Stats.MaxHp) return "first_battle_potion_no_effect";
        var error = PokemonStateValidator.Validate(catalog, healed);
        if (error is not null) return "first_battle_potion_" + error;
        // Compare all non-HP fields structurally, including PP; allow an equivalent copied move array.
        if (healed! with { CurrentHp = before.CurrentHp, Moves = before.Moves } != before ||
            !healed.Moves.SequenceEqual(before.Moves)) return "first_battle_potion_player_changed";
        int restored = healed.CurrentHp - before.CurrentHp;
        // Validate the 20-HP cap and maximum-HP clamp without applying an item effect a second time.
        if (restored is <= 0 or > 20 || restored < 20 && healed.CurrentHp != before.Stats.MaxHp)
            return "first_battle_potion_hp_delta";
        return null;
    }

    /// <summary>Validation of this bounded represented profile only, including terminal pre-reward snapshots.</summary>
    public static string? Validate(PokemonCatalog? catalog, FirstBattleState? state)
    {
        if (state is null) return "first_battle_state_missing";
        if (state.RulesVersion != RulesVersion) return "first_battle_rules_version";
        if (state.Turn is < 0 or > 255 || state.Revision < 0 || state.EventSequence < 0) return "first_battle_counters";
        if (((int)state.TutorialFlags & ~15) != 0) return "first_battle_tutorial_flags";
        if (!Enum.IsDefined(state.Outcome)) return "first_battle_outcome";
        var error = ValidateCombatant(catalog, state.Player, "player") ?? ValidateCombatant(catalog, state.Opponent, "opponent")
            ?? ValidateRng(state.Rng);
        if (error is not null) return error;
        if (!RivalPair(state.Player.Pokemon.SourceSpeciesId, state.Opponent.Pokemon.SourceSpeciesId))
            return "first_battle_rival_pair_unsupported";
        int playerHp = state.Player.Pokemon.CurrentHp, opponentHp = state.Opponent.Pokemon.CurrentHp;
        if (state.Outcome == FirstBattleOutcome.Ongoing && (playerHp == 0 || opponentHp == 0) ||
            state.Outcome == FirstBattleOutcome.Won && (playerHp == 0 || opponentHp != 0) ||
            state.Outcome == FirstBattleOutcome.Lost && (playerHp != 0 || opponentHp == 0) ||
            state.Outcome == FirstBattleOutcome.Draw && (playerHp != 0 || opponentHp != 0))
            return "first_battle_outcome_hp";
        return null;
    }

    private static bool RivalPair(int player, int opponent) => (player, opponent) is (1, 4) or (4, 7) or (7, 1);
    private static string? ValidateRng(RngState rng) => rng.AlgorithmId == Gen3Rng.AlgorithmId ? null : "first_battle_rng_algorithm";
    private static string? ValidateCombatant(PokemonCatalog? catalog, FirstBattleCombatant? combatant, string side)
    {
        if (combatant is null) return $"first_battle_{side}_missing";
        var error = PokemonStateValidator.Validate(catalog, combatant.Pokemon);
        if (error is not null) return $"first_battle_{side}_{error}";
        if (catalog!.SourceCommit != SourceCommit) return "first_battle_source_commit";
        if (combatant.HeldItem != "ITEM_NONE") return $"first_battle_{side}_held_item_unsupported";
        if (combatant.Status != 0) return $"first_battle_{side}_status_unsupported";
        if (combatant.AttackStage is < 0 or > 12 || combatant.DefenseStage is < 0 or > 12)
            return $"first_battle_{side}_stat_stage";
        var p = combatant.Pokemon;
        if (p.SourceSpeciesId is not (1 or 4 or 7) || p.Level != 5 || p.Experience != 135 || p.Evs != SixStats.Zero)
            return $"first_battle_{side}_starter_profile_unsupported";
        if (p.Moves.Length != 2 || p.Moves.Any(m => m.PpUps != 0)) return $"first_battle_{side}_moves_unsupported";
        if (!FirstBattleProfile.HasStarterMovePair(p))
            return $"first_battle_{side}_moves_unsupported";
        foreach (var slot in p.Moves)
        {
            var move = catalog.Moves[slot.Move];
            if (!FirstBattleProfile.SupportsMove(move))
                return "first_battle_move_definition_unsupported";
        }
        var species = catalog.Species[p.Species];
        if (!FirstBattleProfile.SupportsSpecies(species))
            return "first_battle_species_definition_unsupported";
        return null;
    }

    private static string? ValidateSelection(FirstBattleCombatant combatant, int slot, string side)
    {
        if (RequiresStruggle(combatant)) return slot == StruggleSlot ? null : $"first_battle_{side}_struggle_required";
        if (slot == StruggleSlot) return $"first_battle_{side}_struggle_not_required";
        if (slot < 0 || slot >= combatant.Pokemon.Moves.Length) return $"first_battle_{side}_move_slot";
        return combatant.Pokemon.Moves[slot].CurrentPp == 0 ? $"first_battle_{side}_move_pp_empty" : null;
    }
    /// <summary>Call only for a validated combatant; this profile represents PP as its only move limitation.</summary>
    public static bool RequiresStruggle(FirstBattleCombatant combatant) => combatant.Pokemon.Moves.All(m => m.CurrentPp == 0);
    private static (MoveId Id, int Accuracy, string Effect, int Priority) SelectedMove(
        PokemonCatalog catalog, FirstBattleCombatant combatant, int slot)
    {
        // Pinned MOVE_STRUGGLE is a rule fallback. The catalog and owned two-slot learnsets remain unchanged.
        if (slot == StruggleSlot) return (StruggleMove, 100, "EFFECT_RECOIL", 0);
        var move = catalog.Moves[combatant.Pokemon.Moves[slot].Move];
        return (move.Id, move.Accuracy, move.Effect, move.Priority);
    }
    private static FirstBattleResult Fail(FirstBattleState? state, string code) =>
        new(TransitionStatus.Fault, state, [], [], new(code, "Input is outside the supported first-lab-battle transition."));
    private static FirstBattleSide Other(FirstBattleSide side) => side == FirstBattleSide.Player ? FirstBattleSide.Opponent : FirstBattleSide.Player;

    private sealed class Execution(FirstBattleState initial)
    {
        public FirstBattleState State = initial;
        private readonly ImmutableArray<FirstBattleEvent>.Builder events = ImmutableArray.CreateBuilder<FirstBattleEvent>();
        private readonly ImmutableArray<RandomTrace>.Builder trace = ImmutableArray.CreateBuilder<RandomTrace>();
        public ushort Draw(string purpose, int bound = 65536)
        {
            var draw = Gen3Rng.Next(State.Rng, purpose, bound);
            State = State with { Rng = draw.State };
            trace.Add(draw.Trace);
            return draw.Value;
        }
        public void Add(Func<long, FirstBattleEvent> create)
        {
            long sequence = checked(State.EventSequence + 1);
            events.Add(create(sequence));
            State = State with { EventSequence = sequence };
        }
        private FirstBattleCombatant Get(FirstBattleSide side) => side == FirstBattleSide.Player ? State.Player : State.Opponent;
        private void Set(FirstBattleSide side, FirstBattleCombatant mon) =>
            State = side == FirstBattleSide.Player ? State with { Player = mon } : State with { Opponent = mon };
        public FirstBattleSide Order(FirstBattleOrderPhase phase, int playerPriority, int opponentPriority)
        {
            bool opponentFirst = playerPriority < opponentPriority;
            if (playerPriority == opponentPriority)
            {
                int playerSpeed = State.Player.Pokemon.Stats.Speed, opponentSpeed = State.Opponent.Pokemon.Stats.Speed;
                opponentFirst = playerSpeed < opponentSpeed;
                if (playerSpeed == opponentSpeed)
                    opponentFirst = (Draw($"first-battle.{phase.ToString().ToLowerInvariant()}.speed-tie", 2) & 1) != 0;
            }
            var first = opponentFirst ? FirstBattleSide.Opponent : FirstBattleSide.Player;
            Add(n => new FirstBattleOrderEvent(n, State.Turn, phase, first, Other(first)));
            return first;
        }
        private void Tutorial(FirstBattleTutorialFlags flag)
        {
            if ((State.TutorialFlags & flag) != 0) return;
            State = State with { TutorialFlags = State.TutorialFlags | flag };
            Add(n => new FirstBattleTutorialEvent(n, State.Turn, flag, State.TutorialFlags));
        }
        public void Potion(PokemonInstance healed)
        {
            int before = State.Player.Pokemon.CurrentHp;
            State = State with { Player = State.Player with { Pokemon = healed } };
            Add(n => new FirstBattlePotionRecoveredEvent(n, State.Turn, FirstBattleSide.Player,
                before, healed.CurrentHp, healed.CurrentHp - before));
            Tutorial(FirstBattleTutorialFlags.HpRestore);
        }
        public void Move(PokemonCatalog catalog, FirstBattleSide side, int slot)
        {
            var targetSide = Other(side);
            var actor = Get(side);
            var target = Get(targetSide);
            bool struggle = slot == StruggleSlot;
            var move = SelectedMove(catalog, actor, slot);
            string purpose = $"first-battle.{side.ToString().ToLowerInvariant()}";
            // Forced Struggle calls GetMoveTarget: even single battles reject random self-targets until the foe wins.
            if (struggle)
                while (Draw($"{purpose}.struggle-target", 2) % 2 == (int)side) { }
            // All five source scripts pass ACC_CURR_MOVE=0. Tutorial inspects MOVE_NONE.power before resolving it.
            bool exempt = side == FirstBattleSide.Player && (State.TutorialFlags & FirstBattleTutorialFlags.StatChange) == 0;
            bool hit = exempt || Draw($"{purpose}.accuracy", 100) % 100 + 1 <= move.Accuracy;
            int ppBefore = struggle ? 0 : actor.Pokemon.Moves[slot].CurrentPp;
            int ppAfter = struggle ? 0 : ppBefore - 1;
            if (!struggle)
            {
                var pp = actor.Pokemon.Moves[slot];
                actor = actor with { Pokemon = actor.Pokemon with { Moves = actor.Pokemon.Moves.SetItem(slot, pp with { CurrentPp = ppAfter }) } };
                Set(side, actor);
            }
            Add(n => new FirstBattleMoveUsedEvent(n, State.Turn, side, targetSide, slot, move.Id, ppBefore, ppAfter));
            if (!hit)
            {
                Add(n => new FirstBattleMoveMissedEvent(n, State.Turn, side, targetSide, move.Id));
                return;
            }
            if (move.Effect == "EFFECT_HIT" || struggle)
            {
                // Random precedes the damage tutorial gate even when critical hits are still suppressed.
                bool rolledCritical = Draw($"{purpose}.critical", 16) % 16 == 0;
                bool critical = rolledCritical && (State.TutorialFlags & FirstBattleTutorialFlags.Damage) != 0;
                int percent = 100 - Draw($"{purpose}.damage", 16) % 16;
                int damage = FirstBattleDamage.Calculate(catalog, actor.Pokemon, target.Pokemon, move.Id,
                    actor.AttackStage, target.DefenseStage, critical, percent).Damage;
                int before = target.Pokemon.CurrentHp, after = Math.Max(0, before - damage);
                target = target with { Pokemon = target.Pokemon with { CurrentHp = after } };
                Set(targetSide, target);
                Add(n => new FirstBattleHpChangedEvent(n, State.Turn, side, targetSide, move.Id, before, after, damage, critical, percent));
                // Opponent controller sets flag1 after the player's first damage health-bar event, before the next actor.
                if (side == FirstBattleSide.Player) Tutorial(FirstBattleTutorialFlags.Damage);
                if (critical) Add(n => new FirstBattleCriticalHitEvent(n, State.Turn, side, targetSide));
                if (struggle)
                {
                    // MOVE_EFFECT_CERTAIN branches before Random. gHpDealt was clamped by datahpupdate.
                    int recoil = Math.Max(1, (before - after) / 4);
                    int actorBefore = actor.Pokemon.CurrentHp, actorAfter = Math.Max(0, actorBefore - recoil);
                    Set(side, actor with { Pokemon = actor.Pokemon with { CurrentHp = actorAfter } });
                    Add(n => new FirstBattleRecoilEvent(n, State.Turn, side, move.Id, actorBefore, actorAfter, recoil));
                    // Any opponent HP bar, including its own recoil, sets source damage tutorial flag1.
                    if (side == FirstBattleSide.Opponent) Tutorial(FirstBattleTutorialFlags.Damage);
                    if (actorAfter == 0) Add(n => new FirstBattleFaintedEvent(n, State.Turn, side));
                }
                else Draw($"{purpose}.secondary-effect", 100); // effect byte 0 evaluates Random even on KO.
                if (after == 0)
                    Add(n => new FirstBattleFaintedEvent(n, State.Turn, targetSide));
                bool playerFainted = State.Player.Pokemon.CurrentHp == 0, opponentFainted = State.Opponent.Pokemon.CurrentHp == 0;
                if (playerFainted || opponentFainted)
                {
                    // checkteamslost ORs LOST and WON after both faint scripts, allowing original outcome3.
                    var outcome = playerFainted && opponentFainted ? FirstBattleOutcome.Draw :
                        playerFainted ? FirstBattleOutcome.Lost : FirstBattleOutcome.Won;
                    State = State with { Outcome = outcome };
                    Add(n => new FirstBattleEndedEvent(n, State.Turn, outcome));
                }
                return;
            }
            var stat = move.Effect == "EFFECT_ATTACK_DOWN" ? FirstBattleStat.Attack : FirstBattleStat.Defense;
            int prior = stat == FirstBattleStat.Attack ? target.AttackStage : target.DefenseStage;
            if (prior == 0)
            {
                Add(n => new FirstBattleStatAtMinimumEvent(n, State.Turn, side, targetSide, stat));
                return;
            }
            target = stat == FirstBattleStat.Attack ? target with { AttackStage = prior - 1 } : target with { DefenseStage = prior - 1 };
            Set(targetSide, target);
            Add(n => new FirstBattleStatChangedEvent(n, State.Turn, side, targetSide, stat, prior, prior - 1));
            if (side == FirstBattleSide.Player) Tutorial(FirstBattleTutorialFlags.StatChange);
        }
        public FirstBattleResult Success() => new(TransitionStatus.Accepted, State, events.ToImmutable(), trace.ToImmutable(), null);
    }
}
