using System.Text.Json.Serialization;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Rules;

namespace Game.Domain.WildBattle;

/// <summary>Source singles action position and actual party slot. A full-turn caller must supply both.</summary>
public sealed record WildBattleCalledActionContext(int ActionOrderIndex = 0, int ActorPartyIndex = 0);

/// <summary>Battler-local registers cleared by ordinary departure/faint, independently of delayed slot effects.</summary>
public sealed record WildBattleCalledVolatileState
{
    public string RulesVersion { get; init; } = WildBattleRules.CalledMoveKernelRulesVersion;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int SubstituteHp { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int StockpileCount { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? PerishSongTimer { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool Tormented { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int TauntTurnsRemaining { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool MagicCoat { get; init; }
    /// <summary>Retained action-order position of this turn's armed Snatch; cleared on consumption/departure.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? SnatchOrderIndex { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool Grudge { get; init; }
}

public sealed record WildBattleCalledWish(int TurnsRemaining, int SourcePartyIndex, SpeciesId SourceSpecies,
    uint SourceOriginalTrainerId, uint SourcePersonality);
public sealed record WildBattleCalledFutureAttack(int TurnsRemaining, MoveId Move, WildBattleSide Attacker, int Damage);
/// <summary>Wish and Future Sight/Doom Desire belong to field positions and survive either combatant switching.</summary>
public sealed record WildBattleCalledDelayedState
{
    public string RulesVersion { get; init; } = WildBattleRules.CalledMoveKernelRulesVersion;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleCalledWish? PlayerWish { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleCalledWish? OpponentWish { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleCalledFutureAttack? PlayerFutureAttack { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleCalledFutureAttack? OpponentFutureAttack { get; init; }
}
public sealed record WildBattleSubstituteDamagedEvent(long Sequence, int Turn, WildBattleSide Actor,
    WildBattleSide Target, MoveId Move, int HpBefore, int HpAfter, int Damage, bool Critical, int RandomPercent)
    : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleCalledDelayedEvent(long Sequence, int Turn, WildBattleSide Target, MoveId Move,
    int Before, int After, string Reason, int? SourcePartyIndex = null) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleMagicCoatPressurePpEvent(long Sequence, int Turn, WildBattleSide PressuringActor, WildBattleSide Reflector, int Slot, int Before, int After) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleMagicCoatEvent(long Sequence, int Turn, WildBattleSide OriginalActor,
    WildBattleSide Reflector, MoveId Move) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleGrudgePpEvent(long Sequence, int Turn, WildBattleSide Fainted,
    WildBattleSide Attacker, MoveId Move, int ChosenSlot, int Before) : WildBattleEvent(Sequence, Turn);

public static partial class WildBattleRules
{
    private static bool HasCalledVolatileRecipe(string effect) => effect is "EFFECT_SUBSTITUTE" or "EFFECT_PERISH_SONG" or
        "EFFECT_FUTURE_SIGHT" or "EFFECT_STOCKPILE" or "EFFECT_SPIT_UP" or "EFFECT_SWALLOW" or "EFFECT_TORMENT" or
        "EFFECT_TAUNT" or "EFFECT_WISH" or "EFFECT_MAGIC_COAT" or "EFFECT_GRUDGE";
    private static void AddCalledVolatileSupport(PokemonMoveDefinition move, ref bool supported)
    { if (MetronomeSourceMoveDefinition.Matches(move) && HasCalledVolatileRecipe(move.Effect)) supported = true; }

    private static string? ValidateCalledVolatileState(PokemonCatalog catalog, WildBattleState state)
    {
        foreach (var mon in new[] { state.Player, state.Opponent })
        {
            if (mon.CalledVolatile is not { } v) continue;
            if (v.RulesVersion != CalledMoveKernelRulesVersion) return "called_volatile_version";
            if (v.SubstituteHp is < 0 or > 255 || v.StockpileCount is < 0 or > 3 || v.PerishSongTimer is < 0 or > 3 ||
                v.TauntTurnsRemaining is < 0 or > 2 || v.SnatchOrderIndex is < 0 or > 0) return "called_volatile_counter";
            if (mon.Pokemon.CurrentHp == 0 && v != new WildBattleCalledVolatileState()) return "called_volatile_fainted";
        }
        if (state.CalledDelayed is not { } d) return null;
        if (d.RulesVersion != CalledMoveKernelRulesVersion) return "called_delayed_version";
        foreach (var wish in new[] { d.PlayerWish, d.OpponentWish })
            if (wish is not null && (wish.TurnsRemaining is < 1 or > 2 || wish.SourcePartyIndex is < 0 or > 5 ||
                !catalog.Species.ContainsKey(wish.SourceSpecies))) return "called_wish_state";
        foreach (var future in new[] { d.PlayerFutureAttack, d.OpponentFutureAttack })
            if (future is not null && (future.TurnsRemaining is < 1 or > 3 || future.Damage < 1 ||
                !Enum.IsDefined(future.Attacker) || future.Move.Value is not ("pret:MOVE_FUTURE_SIGHT" or "pret:MOVE_DOOM_DESIRE") ||
                !catalog.Moves.TryGetValue(future.Move, out var move) || !MetronomeSourceMoveDefinition.Matches(move))) return "called_future_state";
        return null;
    }

    /// <summary>Source move-selection restrictions. Taunt is also checked later by the action canceller.</summary>
    public static bool IsCalledMoveSelectable(PokemonCatalog catalog, WildBattleCombatant mon, int slot)
    {
        if (mon.CalledTurnState?.Lock is { } locked) return slot == locked.PpSlot;
        if (ForcedMoveSlot(mon) is { } forced) return slot == forced;
        bool Available(int i) => IsMoveSelectable(mon, i) && !CalledVolatileMoveLimited(catalog, mon, i);
        if (slot == StruggleSlot) return !Enumerable.Range(0, mon.Pokemon.Moves.Length).Any(Available);
        return slot >= 0 && slot < mon.Pokemon.Moves.Length && Available(slot);
    }
    private static bool CalledVolatileMoveLimited(PokemonCatalog catalog, WildBattleCombatant mon, int slot)
    {
        if (slot < 0 || slot >= mon.Pokemon.Moves.Length) return false;
        MoveId id = mon.Pokemon.Moves[slot].Move;
        return mon.CalledVolatile is { } v && (v.Tormented && id != StruggleMove && id == mon.LastUsedMove ||
            v.TauntTurnsRemaining > 0 && catalog.Moves.TryGetValue(id, out var move) && move.Power == 0);
    }

    /// <summary>Runs only Wish, Taunt, delayed attacks, Perish Song and Magic Coat expiry. This is a
    /// composable source-command boundary, not a full turn: weather, status, items, locks and turn RNG are separate.</summary>
    public static WildBattleResult ResolveCalledVolatileEndTurn(PokemonCatalog catalog, WildBattleState state,
        WildBattleSide first = WildBattleSide.Player)
    {
        string? error = ValidateCalledMoveActionState(catalog, state);
        if (error is null && !Enum.IsDefined(first)) error = "called_volatile_residual_order";
        if (error is null && state.CalledParty?.PendingSwitch is not null) error = "called_party_switch_pending";
        if (error is not null) return Fail(state, error);
        return Execute(state, state, context => {
            // BattleTurnPassed's TurnValuesCleanUp(TRUE) precedes all end-turn commands.
            context.State = context.State with { Player = context.State.Player with { Protected = false, Enduring = false },
                Opponent = context.State.Opponent with { Protected = false, Enduring = false } };
            var order = new[] { first, first == WildBattleSide.Player ? WildBattleSide.Opponent : WildBattleSide.Player };
            foreach (var side in order) context.SourceCalledWishResidual(catalog, side);
            foreach (var side in order) context.SourceCalledTauntResidual(side);
            // Future attacks iterate battler positions; Perish Song follows the supplied speed order.
            foreach (var side in new[] { WildBattleSide.Player, WildBattleSide.Opponent }) context.SourceCalledFutureResidual(catalog, side);
            foreach (var side in order) context.SourceCalledPerishResidual(side);
            foreach (var side in order) { context.SourceCalledClearMagicCoat(side); context.SourceClearSnatch(side); }
            context.ForestOutcome(true);
            context.State = context.State with { Revision = checked(state.Revision + 1) };
        });
    }

    internal sealed partial class Execution
    {
        internal WildBattleCalledActionContext calledActionContext = new();
        private readonly SourceSubstituteLifecycle substituteLifecycle = new();
        private bool calledPassiveDelayed;
        private void SetCalledVolatile(WildBattleSide side, WildBattleCalledVolatileState next) =>
            Set(side, Get(side) with { CalledVolatile = next == new WildBattleCalledVolatileState() ? null : next });
        private bool SourceCalledHasSubstitute(WildBattleSide side) =>
            Recipes.SubstitutePresent(S(side));
        private bool SourceCalledSecondaryBlocked(WildBattleSide side) => SourceCalledHasSubstitute(side);
        private bool SourceCalledHitReachedPokemon(WildBattleSide side) => Recipes.SubstituteHitReachedPokemon(S(side));
        private void SourceCalledVolatileEvent(WildBattleSide side, string field, int before, int after, string reason = "move") =>
            Add(n => new WildBattleMtMoonVolatileEvent(n, State.Turn, side, field, before, after, reason));
        private void SourceCalledVolatileFail(WildBattleSide side, WildBattleSide target, MoveId move, string reason) =>
            Add(n => new WildBattleMoveFailedEvent(n, State.Turn, side, target, move, reason));

        private void ExecuteCalledVolatileEffect(PokemonCatalog catalog, WildBattleSide side, int slot,
            PokemonMoveDefinition move, string purpose, ref bool handled)
        {
            if (!HasCalledVolatileRecipe(move.Effect)) return;
            if (activeInvocation is null || !MetronomeSourceMoveDefinition.Matches(move))
                throw new UnsupportedAction("called_volatile_source_definition");
            handled = true;
            var target = activeInvocation.Target;
            if (State.SourceMechanicsRulesVersion == BattleRulesProfile.CanonicalSourceSinglesVersion && SourceRecipeContext.HasDelayedRecipe(move.Effect))
            { Recipes.ExecuteDelayed(catalog, S(side), S(target), slot, move); return; }
            if (move.Effect == "EFFECT_MAGIC_COAT")
            {
                Recipes.ExecutePreparedMove(catalog, S(side), slot, move, purpose, ref handled); return;
            }
            if (move.Effect == "EFFECT_SUBSTITUTE")
            {
                int before = Get(activeInvocation.PpOwner).Pokemon.Moves[activeInvocation.PpSlot].CurrentPp;
                SpendInvocationPp(side, target, move, printAttackString: false);
                bool printed = PrintInvocationAttackString();
                if (printed) Add(n => new WildBattleMoveUsedEvent(n, State.Turn, side, target, slot, move.Id,
                    before, Get(activeInvocation!.PpOwner).Pokemon.Moves[activeInvocation.PpSlot].CurrentPp));
            }
            else SpendEarlyPartyPp(side, target, slot, move);
            var v = Get(side).CalledVolatile ?? new();
            switch (move.Effect)
            {
                case "EFFECT_SUBSTITUTE":
                    Recipes.ApplySubstitute(S(side), move); return;
                case "EFFECT_STOCKPILE":
                    if (v.StockpileCount == 3) { SourceCalledVolatileFail(side, side, move.Id, "stockpile-full"); return; }
                    SetCalledVolatile(side, v with { StockpileCount = v.StockpileCount + 1 });
                    SourceCalledVolatileEvent(side, "stockpile", v.StockpileCount, v.StockpileCount + 1); return;
                case "EFFECT_SPIT_UP": SourceCalledSpitUp(catalog, side, target, slot, move, purpose); return;
                case "EFFECT_SWALLOW":
                    if (v.StockpileCount == 0) { SourceCalledVolatileFail(side, side, move.Id, "stockpile-empty"); return; }
                    SetCalledVolatile(side, v with { StockpileCount = 0 });
                    SourceCalledVolatileEvent(side, "stockpile", v.StockpileCount, 0, "swallow");
                    SourceStatRecoveryHeal(side, move, Math.Max(1, Get(side).Pokemon.Stats.MaxHp / (1 << (3 - v.StockpileCount)))); return;
                case "EFFECT_PERISH_SONG":
                    Recipes.ApplyPerishSong(S(side), S(target), move); return;
                case "EFFECT_FUTURE_SIGHT":
                    if (SourceCalledFuture(target) is not null) { SourceCalledVolatileFail(side, target, move.Id, "future-slot-occupied"); return; }
                    int damage = SourceCalledBaseDamage(catalog, side, target, move);
                    SetCalledFuture(target, new(3, move.Id, side, damage));
                    Add(n => new WildBattleCalledDelayedEvent(n, State.Turn, target, move.Id, 0, 3, "scheduled")); return;
                case "EFFECT_WISH":
                    if (SourceCalledWish(side) is not null) { SourceCalledVolatileFail(side, side, move.Id, "wish-slot-occupied"); return; }
                    var source = WildBattleTransformationRules.PersistentPokemon(Get(side));
                    SetCalledWish(side, new(2, calledActionContext.ActorPartyIndex, source.Species, source.OriginalTrainerId, source.Personality));
                    Add(n => new WildBattleCalledDelayedEvent(n, State.Turn, side, move.Id, 0, 2, "scheduled", calledActionContext.ActorPartyIndex)); return;
                case "EFFECT_TORMENT":
                case "EFFECT_TAUNT":
                    if (!SourceGenericAccuracy(side, target, move, purpose)) { SourceCalledVolatileFail(side, target, move.Id, "accuracy"); return; }
                    var tv = Get(target).CalledVolatile ?? new(); bool torment = move.Effect == "EFFECT_TORMENT";
                    if (torment ? tv.Tormented : tv.TauntTurnsRemaining > 0)
                    { SourceCalledVolatileFail(side, target, move.Id, torment ? "already-tormented" : "already-taunted"); return; }
                    SetCalledVolatile(target, torment ? tv with { Tormented = true } : tv with { TauntTurnsRemaining = 2 });
                    SourceCalledVolatileEvent(target, torment ? "torment" : "taunt", 0, torment ? 1 : 2); return;
                case "EFFECT_MAGIC_COAT":
                    if (calledActionContext.ActionOrderIndex == 1) { SourceCalledVolatileFail(side, side, move.Id, "magic-coat-last-action"); return; }
                    return;
                case "EFFECT_GRUDGE":
                    Recipes.ApplyGrudge(S(side), move); return;
            }
        }

        // Cmd_trysetfutureattack and stockpiletobasedamage stop at CalculateBaseDamage.
        // Reuse the shared damage calculation's source +2 boundary before STAB/type/random commands.
        private int SourceCalledBaseDamage(PokemonCatalog catalog, WildBattleSide side, WildBattleSide target, PokemonMoveDefinition move) =>
            ForestBattleDamage.Calculate(catalog, Get(side), Get(target), move.Id, false, 100,
                true, true, true, true, route3Rules: true, mtMoonRules: true, ceruleanRules: true, billRules: true,
                vermilionRules: true, ssAnneRules: true, vermilionGymRules: true, route11Rules: true,
                rain: SourceCalledRain, rockTunnelRules: true, reflect: RockTunnelReflects(target),
                sourceDefinition: move, sun: SourceCalledSun, lightScreen: SourceCalledLightScreen(target),
                weatherSuppressed: SourceDamageWeatherSuppressed).Steps.Single(s => s.Operation == "plus-two").Value;

        private void SourceCalledSpitUp(PokemonCatalog catalog, WildBattleSide side, WildBattleSide target, int slot,
            PokemonMoveDefinition move, string purpose)
        {
            bool protectedTarget = Get(target).Protected && move.Flags.Contains("FLAG_PROTECT_AFFECTED");
            if (protectedTarget) invocationCommandNoEffect = true;
            if (!protectedTarget && !SourceGenericAccuracy(side, target, move, purpose))
            {
                // BattleScript_PrintMoveMissed contains another ppreduce. The counter survives this branch.
                SpendEarlyPartyPp(side, target, slot, move); SourceGenericMiss(side, target, move); return;
            }
            var v = Get(side).CalledVolatile ?? new();
            if (v.StockpileCount == 0) { SourceCalledVolatileFail(side, target, move.Id, "stockpile-empty"); return; }
            int damage = protectedTarget ? 0 : checked(SourceCalledBaseDamage(catalog, side, target, move) * v.StockpileCount);
            SetCalledVolatile(side, v with { StockpileCount = 0 });
            SourceCalledVolatileEvent(side, "stockpile", v.StockpileCount, 0, "spit-up");
            if (protectedTarget) { SourceCalledVolatileFail(side, target, move.Id, "protected"); return; }
            var species = WildBattleEffective.Types(catalog, Get(side));
            if (species.Type1 == move.Type || species.Type2 == move.Type) damage = damage * 15 / 10;
            var defender = WildBattleEffective.Types(catalog, Get(target));
            foreach (var effect in TypeRules.OrderedEffects(ScalarType(move.Type), ScalarType(defender.Type1), ScalarType(defender.Type2)))
            {
                if (Get(target).Identified && effect.Defense == ElementType.Ghost) continue;
                damage = damage * effect.MultiplierTenths / 10;
                if (effect.MultiplierTenths == 0) { damage = 0; break; }
                if (damage == 0) damage = 1;
            }
            damage = ScalarAdjust(target, move, damage, purpose);
            int lost = SourceGenericApplyDamage(side, target, move, damage, false, 100);
            SourceGenericEffectiveness(catalog, side, target, move);
            SourceGenericSecondary(catalog, side, target, move, purpose, damage);
            ScalarFinish(catalog, side, target, move, purpose, lost, ScalarType(move.Type));
        }

        private bool SourceCalledConsumeSubstituteDamage(WildBattleSide side, WildBattleSide target, PokemonMoveDefinition move,
            int damage, bool critical, int percent, out int lost)
            => SourceCalledConsumeSubstituteDamage(side, target, move.Id, damage, critical, percent, out lost);
        private bool SourceCalledConsumeSubstituteDamage(WildBattleSide side, WildBattleSide target, MoveId move,
            int damage, bool critical, int percent, out int lost)
        {
            return Recipes.ConsumeSubstituteDamage(S(side), S(target), move, damage, critical, percent, out lost, noEffect: fixedDamageNoEffect ?? (damage == 0 && !Recipes.HeldDamageEffectiveZero(S(target))));
        }

        // One datahpupdate boundary, also used by inherited admitted hit recipes. Passive damage,
        // self-cost scripts and healing retain their explicit IGNORE_SUBSTITUTE paths.
        // Action-local receipt for contact abilities. Never serialized or reconstructed from events.
        private (WildBattleSide Actor, WildBattleSide Target, MoveId Move, int HpLost)? sourceContactDamage;

        private int SourceGenericApplyDamage(WildBattleSide side, WildBattleSide targetSide, MoveId move,
            int damage, bool critical, int percent, bool criticalMessageOnZero = false, bool applyEndure = true)
        {
            sourceContactDamage = null;
            if (SourceCalledConsumeSubstituteDamage(side, targetSide, move, damage, critical, percent, out int substituteLost))
            {
                SourceHitItemsRecord(targetSide, move, damage, substituteLost, substitute: true);
                return substituteLost;
            }
            if (applyEndure) damage = RockTunnelAdjustDamage(targetSide, damage);
            var target = Get(targetSide);
            int before = target.Pokemon.CurrentHp, after = Math.Max(0, before - damage);
            sourceContactDamage = (side, targetSide, move, before - after);
            SourceHitItemsRecord(targetSide, move, damage, before - after, substitute: false);
            Set(targetSide, target with { Pokemon = target.Pokemon with { CurrentHp = after } });
            Add(n => new WildBattleHpChangedEvent(n, State.Turn, side, targetSide, move, before, after, damage, critical, percent));
            if (critical && (damage > 0 || criticalMessageOnZero || SourceHeldDamageEnabled && Recipes.HeldDamageEffectiveZero(S(targetSide)))) Add(n => new WildBattleCriticalHitEvent(n, State.Turn, side, targetSide));
            return before - after;
        }

        // These are explicit jumpifstatus2/command gates from the individual scripts. In particular,
        // ChangeStatBuffs has no substitute test: Haze, Psych Up, Tickle, Torment, Taunt and Attract pass.
        private bool SourceCalledSubstituteScriptBlocks(PokemonCatalog catalog, WildBattleSide side, int slot,
            PokemonMoveDefinition move)
        {
            return Recipes.SubstituteScriptBlocks(catalog, S(side), slot, move);
        }

        private void SourceCalledBeginAction(WildBattleSide side)
        {
            Recipes.BeginSubstituteAction();
            Recipes.ClearGrudge(S(side), "action-attempt");
        }
        private void SourceCalledMoveEndSubstitute() => Recipes.EndSubstitutePass();
        private bool SourceCalledTauntPreventsMove(PokemonCatalog catalog, WildBattleSide side, MoveId move)
        {
            if (SourceBattleAdmission.IsCanonicalSourceState(catalog, State)) return Recipes.TauntPreventsMove(catalog, S(side), move);
            if (Get(side).CalledVolatile is not { TauntTurnsRemaining: > 0 } || !catalog.Moves.TryGetValue(move, out var definition) || definition.Power != 0) return false;
            CancelRoute3Rollout(side, "taunt");
            SourceCalledVolatileFail(side, Other(side), move, "taunted"); return true;
        }
        private void SourceCalledApplyGrudge(WildBattleSide fainted) => Recipes.ApplyGrudgePp(S(fainted), calledPassiveDelayed);
        private void SourceCalledClearVolatiles(WildBattleSide side)
        { Set(side, Get(side) with { CalledVolatile = null }); substituteLifecycle.Faded.Remove(S(side)); substituteLifecycle.Hit.Remove(S(side)); }

        private bool SourceCalledReflectMove(PokemonCatalog catalog, ref WildBattleSide side, int slot, PokemonMoveDefinition move)
        {
            if (!Recipes.TryPreparedInterception(move, snatch: false)) return false;
            side = activeInvocation!.Actor; return true;
        }

        private WildBattleCalledWish? SourceCalledWish(WildBattleSide side) => side == WildBattleSide.Player ? State.CalledDelayed?.PlayerWish : State.CalledDelayed?.OpponentWish;
        private WildBattleCalledFutureAttack? SourceCalledFuture(WildBattleSide side) => side == WildBattleSide.Player ? State.CalledDelayed?.PlayerFutureAttack : State.CalledDelayed?.OpponentFutureAttack;
        private void SetCalledWish(WildBattleSide side, WildBattleCalledWish? wish)
        {
            var d = State.CalledDelayed ?? new();
            State = State with { CalledDelayed = side == WildBattleSide.Player ? d with { PlayerWish = wish } : d with { OpponentWish = wish } };
        }
        private void SetCalledFuture(WildBattleSide side, WildBattleCalledFutureAttack? future)
        {
            var d = State.CalledDelayed ?? new();
            State = State with { CalledDelayed = side == WildBattleSide.Player ? d with { PlayerFutureAttack = future } : d with { OpponentFutureAttack = future } };
        }
        internal void SourceCalledWishResidual(PokemonCatalog catalog, WildBattleSide side)
        {
            if (State.SourceMechanicsRulesVersion == BattleRulesProfile.CanonicalSourceSinglesVersion)
            { new SinglesEndTurnBridge(this).WishResidual(catalog, S(side)); return; }
            if (SourceCalledWish(side) is not { } wish) return;
            int after = wish.TurnsRemaining - 1;
            SetCalledWish(side, after == 0 ? null : wish with { TurnsRemaining = after });
            var move = catalog.Moves[new MoveId("pret:MOVE_WISH")];
            Add(n => new WildBattleCalledDelayedEvent(n, State.Turn, side, move.Id, wish.TurnsRemaining, after,
                after == 0 ? "resolved" : "countdown", wish.SourcePartyIndex));
            if (after == 0 && Get(side).Pokemon.CurrentHp > 0)
                SourceStatRecoveryHeal(side, move, Math.Max(1, Get(side).Pokemon.Stats.MaxHp / 2));
        }
        internal void SourceCalledTauntResidual(WildBattleSide side)
        {
            if (State.SourceMechanicsRulesVersion == BattleRulesProfile.CanonicalSourceSinglesVersion)
            { new SinglesEndTurnBridge(this).TauntResidual(S(side)); return; }
            if (Get(side).Pokemon.CurrentHp == 0 || Get(side).CalledVolatile is not { TauntTurnsRemaining: > 0 } v) return;
            SetCalledVolatile(side, v with { TauntTurnsRemaining = v.TauntTurnsRemaining - 1 });
            SourceCalledVolatileEvent(side, "taunt", v.TauntTurnsRemaining, v.TauntTurnsRemaining - 1, "countdown");
        }
        internal void SourceCalledFutureResidual(PokemonCatalog catalog, WildBattleSide target)
        {
            if (SourceCalledFuture(target) is not { } future) return;
            int after = future.TurnsRemaining - 1;
            SetCalledFuture(target, after == 0 ? null : future with { TurnsRemaining = after });
            Add(n => new WildBattleCalledDelayedEvent(n, State.Turn, target, future.Move, future.TurnsRemaining, after, after == 0 ? "resolved" : "countdown"));
            if (after != 0 || Get(target).Pokemon.CurrentHp == 0) return;
            if (SourceHeldDamageEnabled) Recipes.SetHitItemReceipt(S(target), Recipes.HitItemReceipt(S(target)) with { Damage = 0xFFFF });
            var move = catalog.Moves[future.Move]; string purpose = $"wild-battle.{future.Attacker.ToString().ToLowerInvariant()}.future-attack";
            var hit = Recipes.PrepareFutureHit(S(future.Attacker), S(target), move, future.Damage, purpose);
            if (!hit.Hit) return;
            int percent = hit.Percent;
            int damage = SourceHeldDamageEnabled ? SourceAdjustHeldDamage(target, move, hit.Damage, purpose, normal2: true) : ScalarAdjust(target, move, hit.Damage, purpose);
            sourceHitItemCatalog = catalog;
            calledPassiveDelayed = true;
            try
            {
                int lost = SourceGenericApplyDamage(future.Attacker, target, move, damage, false, percent);
                SourceHeldDamageResult(target, move.Id);
                if (SourceCalledHitReachedPokemon(target)) RecordCalledDamage(target, future.Attacker, lost, "TYPE_NORMAL");
                if (Get(target).Pokemon.CurrentHp == 0) ForestFaint(target);
                // HandleAction_ActionFinished cleared gCurrentMove to MOVE_NONE; the source's
                // MOVEEND_RAGE case therefore fails its power != 0 test for this delayed script.
                VermilionMoveEndBerries();
                SourceHitItemsEnd(future.Attacker, target, new("pret:MOVE_NONE"), affected: false);
                SourceCalledMoveEndSubstitute();
            }
            finally { calledPassiveDelayed = false; }
        }
        internal void SourceCalledPerishResidual(WildBattleSide side)
        {
            calledPassiveDelayed = true;
            try { new SinglesEndTurnBridge(this).PerishResidual(S(side)); }
            finally { calledPassiveDelayed = false; }
        }
        internal void SourceCalledClearMagicCoat(WildBattleSide side)
        {
            if (Get(side).CalledVolatile is not { MagicCoat: true } v) return;
            SetCalledVolatile(side, v with { MagicCoat = false }); SourceCalledVolatileEvent(side, "magic-coat", 1, 0, "turn-end");
        }
    }
}
