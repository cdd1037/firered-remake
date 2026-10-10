using System.Text.Json.Serialization;
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
        private readonly HashSet<WildBattleSide> calledSubstituteFaded = [];
        private readonly HashSet<WildBattleSide> calledSubstituteHit = [];
        private bool calledPassiveDelayed;
        private void SetCalledVolatile(WildBattleSide side, WildBattleCalledVolatileState next) =>
            Set(side, Get(side) with { CalledVolatile = next == new WildBattleCalledVolatileState() ? null : next });
        private bool SourceCalledHasSubstitute(WildBattleSide side) =>
            (Get(side).CalledVolatile?.SubstituteHp ?? 0) > 0 || calledSubstituteFaded.Contains(side);
        private bool SourceCalledSecondaryBlocked(WildBattleSide side) => SourceCalledHasSubstitute(side);
        private bool SourceCalledHitReachedPokemon(WildBattleSide side) => !calledSubstituteHit.Contains(side);
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
            if (move.Effect == "EFFECT_MAGIC_COAT")
            {
                activeInvocation = activeInvocation with { Target = side, Flags = activeInvocation.Flags | WildBattleInvocationFlags.PpNotAffectedByPressure };
                target = side;
                if (calledActionContext.ActionOrderIndex == 0)
                {
                    var coat = Get(side).CalledVolatile ?? new();
                    SetCalledVolatile(side, coat with { MagicCoat = true });
                    SourceCalledVolatileEvent(side, "magic-coat", coat.MagicCoat ? 1 : 0, 1);
                }
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
                    if (SourceCalledHasSubstitute(side)) { SourceCalledVolatileFail(side, side, move.Id, "already-substitute"); return; }
                    var mon = Get(side); int cost = Math.Max(1, mon.Pokemon.Stats.MaxHp / 4);
                    if (mon.Pokemon.CurrentHp <= cost) { SourceCalledVolatileFail(side, side, move.Id, "substitute-insufficient-hp"); return; }
                    int hp = mon.Pokemon.CurrentHp;
                    Set(side, mon with { Pokemon = mon.Pokemon with { CurrentHp = hp - cost }, BindTurnsRemaining = 0, BindingMove = null,
                        CalledVolatile = v with { SubstituteHp = cost } });
                    Add(n => new WildBattleHpChangedEvent(n, State.Turn, side, side, move.Id, hp, hp - cost, cost, false, 100));
                    SourceCalledVolatileEvent(side, "substitute", 0, cost);
                    if (mon.BindTurnsRemaining > 0) Add(n => new WildBattleBindingChangedEvent(n, State.Turn, side, mon.BindTurnsRemaining, 0, "substitute") { Move = mon.BindingMove });
                    return;
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
                    bool changed = false;
                    foreach (var recipient in new[] { WildBattleSide.Player, WildBattleSide.Opponent })
                    {
                        var rv = Get(recipient).CalledVolatile ?? new();
                        if (WildBattleEffective.Ability(Get(recipient)) == "ABILITY_SOUNDPROOF") continue;
                        if (rv.PerishSongTimer is not null) continue;
                        SetCalledVolatile(recipient, rv with { PerishSongTimer = 3 });
                        SourceCalledVolatileEvent(recipient, "perish-song", -1, 3); changed = true;
                    }
                    if (!changed) { SourceCalledVolatileFail(side, target, move.Id, "perish-song-already-active"); return; }
                    // The source's Soundproof reporting loop runs only after trysetperishsong succeeds.
                    foreach (var recipient in new[] { WildBattleSide.Player, WildBattleSide.Opponent })
                        if (WildBattleEffective.Ability(Get(recipient)) == "ABILITY_SOUNDPROOF")
                        { RecordAbility?.Invoke(recipient, "ABILITY_SOUNDPROOF"); Add(n => new WildBattleAbilityActivatedEvent(n, State.Turn, recipient, "ABILITY_SOUNDPROOF")); }
                    return;
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
                    if (v.Grudge) { SourceCalledVolatileFail(side, side, move.Id, "already-grudge"); return; }
                    SetCalledVolatile(side, v with { Grudge = true }); SourceCalledVolatileEvent(side, "grudge", 0, 1); return;
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
            calledSubstituteHit.Remove(target); lost = 0;
            var v = Get(target).CalledVolatile;
            if (v is null || v.SubstituteHp == 0 || damage <= 0) return false;
            int after = Math.Max(0, v.SubstituteHp - damage); lost = v.SubstituteHp - after;
            SetCalledVolatile(target, v with { SubstituteHp = after }); calledSubstituteHit.Add(target);
            if (after == 0) calledSubstituteFaded.Add(target);
            Add(n => new WildBattleSubstituteDamagedEvent(n, State.Turn, side, target, move, v.SubstituteHp, after, damage, critical, percent));
            if (critical) Add(n => new WildBattleCriticalHitEvent(n, State.Turn, side, target));
            return true;
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
                return substituteLost;
            if (applyEndure) damage = RockTunnelAdjustDamage(targetSide, damage);
            var target = Get(targetSide);
            int before = target.Pokemon.CurrentHp, after = Math.Max(0, before - damage);
            sourceContactDamage = (side, targetSide, move, before - after);
            Set(targetSide, target with { Pokemon = target.Pokemon with { CurrentHp = after } });
            Add(n => new WildBattleHpChangedEvent(n, State.Turn, side, targetSide, move, before, after, damage, critical, percent));
            if (critical && (damage > 0 || criticalMessageOnZero)) Add(n => new WildBattleCriticalHitEvent(n, State.Turn, side, targetSide));
            return before - after;
        }

        // These are explicit jumpifstatus2/command gates from the individual scripts. In particular,
        // ChangeStatBuffs has no substitute test: Haze, Psych Up, Tickle, Torment, Taunt and Attract pass.
        private bool SourceCalledSubstituteScriptBlocks(PokemonCatalog catalog, WildBattleSide side, int slot,
            PokemonMoveDefinition move)
        {
            if (activeInvocation is null) return false;
            WildBattleSide target = activeInvocation.Target;
            if (!SourceCalledHasSubstitute(target)) return false;
            string ability = WildBattleEffective.Ability(Get(target));
            bool directStage = move.Effect is "EFFECT_ATTACK_DOWN" or "EFFECT_DEFENSE_DOWN" or "EFFECT_SPEED_DOWN" or
                "EFFECT_SPECIAL_ATTACK_DOWN" or "EFFECT_SPECIAL_DEFENSE_DOWN" or "EFFECT_ACCURACY_DOWN" or "EFFECT_EVASION_DOWN" or
                "EFFECT_ATTACK_DOWN_2" or "EFFECT_DEFENSE_DOWN_2" or "EFFECT_SPEED_DOWN_2" or "EFFECT_SPECIAL_ATTACK_DOWN_2" or
                "EFFECT_SPECIAL_DEFENSE_DOWN_2" or "EFFECT_ACCURACY_DOWN_2" or "EFFECT_EVASION_DOWN_2";
            bool blocked = directStage || move.Effect switch {
                "EFFECT_SLEEP" or "EFFECT_LEECH_SEED" or "EFFECT_LOCK_ON" or "EFFECT_NIGHTMARE" or "EFFECT_WILL_O_WISP" or
                    "EFFECT_PAIN_SPLIT" or "EFFECT_DREAM_EATER" or "EFFECT_MEAN_LOOK" or "EFFECT_SWAGGER" or "EFFECT_FLATTER" => true,
                "EFFECT_POISON" or "EFFECT_TOXIC" => ability != "ABILITY_IMMUNITY",
                "EFFECT_CONFUSE" => ability != "ABILITY_OWN_TEMPO",
                "EFFECT_PARALYZE" => ability != "ABILITY_LIMBER",
                "EFFECT_YAWN" => ability is not ("ABILITY_VITAL_SPIRIT" or "ABILITY_INSOMNIA"),
                "EFFECT_CURSE" => WildBattleEffective.Types(catalog, Get(side)).Type1 == "TYPE_GHOST" ||
                    WildBattleEffective.Types(catalog, Get(side)).Type2 == "TYPE_GHOST",
                _ => false
            };
            if (!blocked) return false;
            SpendEarlyPartyPp(side, target, slot, move);
            if (move.Effect is "EFFECT_SWAGGER" or "EFFECT_FLATTER") SourceGenericMiss(side, target, move);
            else SourceCalledVolatileFail(side, target, move.Id, "substitute");
            return true;
        }

        private void SourceCalledBeginAction(WildBattleSide side)
        {
            calledSubstituteFaded.Clear(); calledSubstituteHit.Clear();
            if (Get(side).CalledVolatile is { Grudge: true } v)
            { SetCalledVolatile(side, v with { Grudge = false }); SourceCalledVolatileEvent(side, "grudge", 1, 0, "action-attempt"); }
        }
        private void SourceCalledMoveEndSubstitute() => calledSubstituteFaded.Clear();
        private bool SourceCalledTauntPreventsMove(PokemonCatalog catalog, WildBattleSide side, MoveId move)
        {
            if (Get(side).CalledVolatile is not { TauntTurnsRemaining: > 0 } || !catalog.Moves.TryGetValue(move, out var definition) || definition.Power != 0) return false;
            CancelRoute3Rollout(side, "taunt");
            SourceCalledVolatileFail(side, Other(side), move, "taunted"); return true;
        }
        private void SourceCalledApplyGrudge(WildBattleSide fainted)
        {
            if (calledPassiveDelayed || Get(fainted).CalledVolatile is not { Grudge: true } || activeInvocation is not { } invocation ||
                invocation.Actor == fainted || Get(invocation.Actor).Pokemon.CurrentHp == 0 || invocation.CurrentMove == StruggleMove) return;
            var actor = Get(invocation.Actor); int slot = invocation.ChosenSlot;
            if (slot < 0 || slot >= actor.Pokemon.Moves.Length) throw new UnsupportedAction("called_grudge_chosen_slot");
            int pp = actor.Pokemon.Moves[slot].CurrentPp;
            Set(invocation.Actor, actor with { Pokemon = actor.Pokemon with { Moves = actor.Pokemon.Moves.SetItem(slot, actor.Pokemon.Moves[slot] with { CurrentPp = 0 }) } });
            Add(n => new WildBattleGrudgePpEvent(n, State.Turn, fainted, invocation.Actor, actor.Pokemon.Moves[slot].Move, slot, pp));
        }
        private void SourceCalledClearVolatiles(WildBattleSide side)
        { Set(side, Get(side) with { CalledVolatile = null }); calledSubstituteFaded.Remove(side); calledSubstituteHit.Remove(side); }

        private bool SourceCalledReflectMove(PokemonCatalog catalog, ref WildBattleSide side, int slot, PokemonMoveDefinition move)
        {
            if (activeInvocation is not { } invocation || !move.Flags.Contains("FLAG_MAGIC_COAT_AFFECTED") ||
                Get(invocation.Target).CalledVolatile is not { MagicCoat: true } coat) return false;
            WildBattleSide original = side, reflector = invocation.Target;
            SetCalledVolatile(reflector, coat with { MagicCoat = false });
            SpendEarlyPartyPp(original, reflector, slot, move);
            Add(n => new WildBattleMagicCoatEvent(n, State.Turn, original, reflector, move.Id));
            activeInvocation = activeInvocation! with { Actor = reflector, Target = original, PpOwner = invocation.PpOwner,
                Flags = activeInvocation!.Flags | WildBattleInvocationFlags.AttackStringPrinted | WildBattleInvocationFlags.NoPpDeduct | WildBattleInvocationFlags.AllowNoPp };
            side = reflector;
            return true;
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
            var move = catalog.Moves[future.Move]; string purpose = $"wild-battle.{future.Attacker.ToString().ToLowerInvariant()}.future-attack";
            if (!SourceGenericAccuracy(future.Attacker, target, move, purpose))
            { SourceCalledVolatileFail(future.Attacker, target, move.Id, "future-accuracy"); return; }
            int percent = 100 - Draw(purpose + ".damage", 16) % 16;
            int damage = ScalarAdjust(target, move, Math.Max(1, checked(future.Damage * percent) / 100), purpose);
            calledPassiveDelayed = true;
            try
            {
                int lost = SourceGenericApplyDamage(future.Attacker, target, move, damage, false, percent);
                if (SourceCalledHitReachedPokemon(target)) RecordCalledDamage(target, future.Attacker, lost, "TYPE_NORMAL");
                if (Get(target).Pokemon.CurrentHp == 0) ForestFaint(target);
                // HandleAction_ActionFinished cleared gCurrentMove to MOVE_NONE; the source's
                // MOVEEND_RAGE case therefore fails its power != 0 test for this delayed script.
                VermilionMoveEndBerries();
                SourceCalledMoveEndSubstitute();
            }
            finally { calledPassiveDelayed = false; }
        }
        internal void SourceCalledPerishResidual(WildBattleSide side)
        {
            if (Get(side).CalledVolatile is not { PerishSongTimer: { } timer } v) return;
            SetCalledVolatile(side, v with { PerishSongTimer = timer == 0 ? null : timer - 1 });
            SourceCalledVolatileEvent(side, "perish-song", timer, timer == 0 ? -1 : timer - 1, timer == 0 ? "perished" : "countdown");
            if (timer != 0 || Get(side).Pokemon.CurrentHp == 0) return;
            int hp = Get(side).Pokemon.CurrentHp;
            calledPassiveDelayed = true;
            try {
                Set(side, Get(side) with { Pokemon = Get(side).Pokemon with { CurrentHp = 0 } });
                Add(n => new WildBattleHpChangedEvent(n, State.Turn, side, side, new("pret:MOVE_PERISH_SONG"), hp, 0, hp, false, 100));
                ForestFaint(side);
            } finally { calledPassiveDelayed = false; }
        }
        internal void SourceCalledClearMagicCoat(WildBattleSide side)
        {
            if (Get(side).CalledVolatile is not { MagicCoat: true } v) return;
            SetCalledVolatile(side, v with { MagicCoat = false }); SourceCalledVolatileEvent(side, "magic-coat", 1, 0, "turn-end");
        }
    }
}
