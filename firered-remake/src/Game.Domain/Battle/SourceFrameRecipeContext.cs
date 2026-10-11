using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Rules;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal sealed partial class FrameRecipeContext : SourceRecipeContext
{
    private readonly PokemonCatalog catalog;
    private readonly SourceResolvedMoveAction action;
    private readonly SourceBattleFrame initial;
    private PokemonMoveDefinition move;
    private bool currentLightningRodRedirected;
    private bool calledMoveExecuted;
    private SourceMoveInvocation invocation;
    private bool commandNoEffect;
    private bool skipsMoveEnd;
    private int targetEventStart;
    private int completedTargetPasses;
    internal SourceBattleFrame Frame { get; private set; }
    internal readonly ImmutableArray<SourceBattleEvent>.Builder Events = ImmutableArray.CreateBuilder<SourceBattleEvent>();
    internal readonly ImmutableArray<RandomTrace>.Builder Trace = ImmutableArray.CreateBuilder<RandomTrace>();
    internal FrameRecipeContext(PokemonCatalog catalog, SourceBattleFrame frame, SourceResolvedMoveAction action, PokemonMoveDefinition? delayedMove = null)
    {
        this.catalog = catalog; this.action = action; Frame = initial = frame;
        currentLightningRodRedirected = action.LightningRodRedirected;
        move = delayedMove ?? SourceBattleKernel.ActionMove(catalog, frame.Get(action.Actor.Slot), action.ChosenSlot);
        bool struggle = action.ChosenSlot == WildBattleRules.StruggleSlot;
        invocation = new(action.Actor.Slot, struggle ? frame.Get(action.Actor.Slot).Registers.LastMoveSlot : action.ChosenSlot,
            action.ChosenSlot, move.Id, move.Id, move.Id, null, action.Target, WildBattleAttackCancellerPhase.Start,
            struggle ? WildBattleInvocationFlags.NoPpDeduct : WildBattleInvocationFlags.None);
    }
    // BattleScript_MonTookFutureAttack reuses accuracycheck and the ordinary damage update.
    // Protection has already expired before delayed work; Focus Band uses the shared adjustment, with the same Substitute boundary as direct damage.
    internal void RunFutureHit(SourceFutureAttack future, BattlerSlot target)
    {
        string purpose = $"source-battle.slot-{future.Attacker.Value}.future-attack";
        SetHitItemReceipt(target, HitItemReceipt(target) with { Damage = 0xFFFF });
        var hit = PrepareFutureHit(future.Attacker, target, move, future.Damage, purpose);
        if (!hit.Hit) return;
        delayedHitItemsPending = true;
        int adjusted = AdjustHeldDamage(target, move.Id, hit.Damage, purpose, normal2: true);
        delayedContactDamage = true;
        int lost = SourceGenericApplyDamage(future.Attacker, target, move, adjusted, false, hit.Percent);
        delayedContactDamage = false;
        SourceDamageResultMessage(future.Attacker, target, move);
        if (!SubstituteHitReachedPokemon(target)) return;
        var cell = Frame.Get(target); var prior = cell.Registers.Damage;
        Frame = Frame with { Slots = Frame.Slots.SetItem(target.Value, cell with { Registers = cell.Registers with {
            Damage = SourceMoveHistoryReducer.RecordDamage(prior, Turn, future.Attacker, lost, "TYPE_NORMAL") } }) };
    }
    // The approved deferred-KO policy can leave all target positions unavailable. Run the
    // original ButItFailedAtkStringPpReduce commands against the attempted retained slot;
    // there is no hit/accuracy/secondary-effect recipe and no newly selected recipient.
    internal void RunNoTarget()
    {
        string purpose = Frame.Layout == SourceBattleLayout.Singles
            ? $"wild-battle.{(action.Actor.Slot.Side == BattleSide.Player ? "player" : "opponent")}" : $"source-battle.slot-{action.Actor.Slot.Value}";
        BeginLateTurnAction();
        PrepareCharge(invocation.Actor,move);
        PrepareDynamicPower(invocation.Actor, move);
        PrepareDynamicType(move);
        if (ActionPreventsMove(catalog, invocation.Actor, move.Id, purpose))
        {
            CompleteScriptTarget();
            FinishAction();
            return;
        }
        invocation = invocation with { CancellerPhase = WildBattleAttackCancellerPhase.ConditionsPassed,
            Flags = invocation.Flags | WildBattleInvocationFlags.Obeys };
        BeginScriptTarget(action.Target);
        ReleaseChargeWithoutTarget(invocation.Actor,move);
        SpendEarlyPartyPp(action.Actor.Slot, action.Target, action.ChosenSlot, move);
        Add(n => new SourceMoveFailedEvent(n, Turn, action.Actor.Slot, action.Target, move.Id, "deferred-ko-no-target"));
        commandNoEffect = true;
        CompleteScriptTarget();
        FinishAction();
    }
    internal void Run()
    {
        RunCurrentMove(false);
        FinishAction();
    }
    private void RunCurrentMove(bool resumed)
    {
        var scriptMove = move;
        string purpose = Frame.Layout == SourceBattleLayout.Singles
            ? $"wild-battle.{(action.Actor.Slot.Side == BattleSide.Player ? "player" : "opponent")}" : $"source-battle.slot-{action.Actor.Slot.Value}";
        var firstTarget = invocation.Target;
        if (!resumed) BeginLateTurnAction();
        PrepareCharge(invocation.Actor,move);
        PrepareDynamicPower(invocation.Actor, move);
        PrepareDynamicType(move);
        bool blocked = !resumed && ActionPreventsMove(catalog, invocation.Actor, move.Id, purpose);
        if (blocked) CompleteScriptTarget();
        else
        {
            invocation = invocation with { CancellerPhase = WildBattleAttackCancellerPhase.ConditionsPassed };
            ExecutePass(firstTarget);
        }
        // MOVEEND_NEXT_TARGET uses HP only. It does not test source absence or restart action
        // ownership, so PP exhaustion and the attacker's zero HP cannot cancel the second pass.
        if (!blocked && !ChargeFirstTurn(scriptMove) && Frame.Layout == SourceBattleLayout.PresetDoubles && scriptMove.Target == "MOVE_TARGET_BOTH" &&
            Frame.Get(invocation.Target.Partner).Body?.Pokemon.CurrentHp > 0)
        {
            invocation = invocation with { Flags = invocation.Flags | WildBattleInvocationFlags.NoAttackString };
            ExecutePass(invocation.Target.Partner);
        }
        void ExecutePass(BattlerSlot target)
        {
            BeginScriptTarget(target);
            int completed = completedTargetPasses;
            if (WildBattleSourceCommands.IsSoundMove(move.Id) && WildBattleEffective.Ability(Get(target)) == "ABILITY_SOUNDPROOF")
            {
                if ((invocation.Flags & WildBattleInvocationFlags.NoAttackString) == 0)
                    invocation = invocation with { Flags = invocation.Flags & ~WildBattleInvocationFlags.Obeys };
                if (UproarLocked(invocation.Actor)) invocation = invocation with { Flags = invocation.Flags | WildBattleInvocationFlags.NoPpDeduct };
                SpendEarlyPartyPp(invocation.Actor, target, invocation.PpSlot, move);
                Add(n => new SourceAbilityPreventedEvent(n, Turn, invocation.Actor, target, move.Id, "ABILITY_SOUNDPROOF"));
            }
            else if ((invocation.Flags & (WildBattleInvocationFlags.AllowNoPp | WildBattleInvocationFlags.NoAttackString)) == 0 &&
                invocation.PpSlot >= 0 && Get(invocation.Actor).Pokemon.Moves[invocation.PpSlot].CurrentPp == 0 &&
                Frame.Get(invocation.Actor).Registers.Turn?.Lock is not { Kind: WildBattleCalledLockKind.Rollout or WildBattleCalledLockKind.Charge or WildBattleCalledLockKind.Rampage or WildBattleCalledLockKind.Uproar })
            {
                PrintFrameAttackString();
                Add(n => new SourceMoveFailedEvent(n, Turn, invocation.Actor, target, move.Id, "no-pp"));
            }
            else
            {
                invocation = invocation with { Flags = (invocation.Flags | WildBattleInvocationFlags.Obeys) & ~WildBattleInvocationFlags.AllowNoPp };
                while (TryPreparedInterception(move))
                {
                    target = invocation.Target;
                    purpose = Frame.Layout == SourceBattleLayout.Singles
                        ? $"wild-battle.{(invocation.Actor.Side == BattleSide.Player ? "player" : "opponent")}" : $"source-battle.slot-{invocation.Actor.Value}";
                    if (WildBattleSourceCommands.IsSoundMove(move.Id) && WildBattleEffective.Ability(Get(target)) == "ABILITY_SOUNDPROOF")
                    {
                        // The intercepted invocation already passed obedience; Soundproof does
                        // not undo that earlier source flag when the canceller is re-entered.
                        SpendEarlyPartyPp(invocation.Actor, target, invocation.PpSlot, move);
                        Add(n => new SourceAbilityPreventedEvent(n, Turn, invocation.Actor, target, move.Id, "ABILITY_SOUNDPROOF"));
                        CompleteScriptTarget(); return;
                    }
                    invocation = invocation with { Flags = invocation.Flags & ~WildBattleInvocationFlags.AllowNoPp };
                }
                if (currentLightningRodRedirected && target == firstTarget)
                    Add(n => new SourceAbilityActivatedEvent(n, Turn, target, "ABILITY_LIGHTNING_ROD"));
                if (Get(target).Protected && move.Flags.Contains("FLAG_PROTECT_AFFECTED") && !ChargeFirstTurn(move))
                {
                    // attackcanceler sets the miss before the script's own PP/accuracy commands.
                    // Keep those commands in their original per-target positions (not an early return).
                    MarkProtectionMiss(target);
                    CancellerCancelMultiTurn(invocation.Actor, "protected");
                }
                bool handled = false;
                if (HasCallerRecipe(move.Effect)) { ExecuteFrameCaller(purpose); handled = true; }
                handled = handled || !HasSleepAttackRecipe(move.Effect) && !HasSleepResidualRecipe(move.Effect) &&
                    !HasUtilityHpRecipe(move.Effect) && SubstituteScriptBlocks(catalog, invocation.Actor, invocation.ChosenSlot, move);
                if (!handled) ExecutePreparedMove(catalog, invocation.Actor, invocation.ChosenSlot, move, purpose, ref handled);
                if (!handled && move.Effect == "EFFECT_SUBSTITUTE") { ExecuteFrameSubstitute(); handled = true; }
                if (!handled && HasProtectionRecipe(move.Effect))
                {
                    SpendEarlyPartyPp(invocation.Actor, target, invocation.PpSlot, move);
                    var previous = initial.Get(invocation.Actor);
                    ApplyProtection(invocation.Actor, target, move, purpose,
                        previous.Registers.History?.LastResultingMove ?? previous.Body!.LastCalledMove ?? previous.Body.LastUsedMove,
                        action.ActionOrderIndex != Frame.BattlerCount - 1);
                    handled = true;
                }
                if (!handled && HasDelayedRecipe(move.Effect)) { ExecuteDelayed(catalog, invocation.Actor, target, invocation.ChosenSlot, move); handled = true; }
                if (!handled && HasStockpileChargeRecipe(move.Effect)) { ExecuteStockpileCharge(catalog, invocation.Actor, target, invocation.PpSlot, move, purpose); handled = true; }
                if (!handled && HasUtilityHpRecipe(move.Effect)) { ExecuteUtilityHp(catalog, invocation.Actor, target, invocation.ChosenSlot, move); handled = true; }
                if (!handled) ExecuteAllySupport(invocation.Actor, invocation.ChosenSlot, move, ref handled);
                if (!handled && HasSwitchRecipe(move.Effect)) { ExecuteSwitch(catalog, invocation.Actor, invocation.ChosenSlot, move, purpose); handled = true; }
                if (!handled && move.Effect == "EFFECT_HEAL_BELL") { ExecutePartyHealing(invocation.Actor, invocation.ChosenSlot, move); handled = true; }
                if (!handled && move.Effect == "EFFECT_SPIKES") { ExecuteSpikes(invocation.Actor, invocation.ChosenSlot, move); handled = true; }
                if (!handled && HasResidualStatusRecipe(move.Effect))
                {
                    if (move.Effect == "EFFECT_INGRAIN") ExecuteIngrain(invocation.Actor, invocation.ChosenSlot, move);
                    else ExecuteLeechSeed(catalog, invocation.Actor, target, invocation.ChosenSlot, move, purpose);
                    handled = true;
                }
                if (!handled && HasSleepAttackRecipe(move.Effect)) { ExecuteSleepAttack(catalog, invocation.Actor, target, invocation.ChosenSlot, move, purpose); handled = true; }
                if (!handled && HasSleepResidualRecipe(move.Effect)) { ExecuteSleepResidual(catalog, invocation.Actor, target, invocation.ChosenSlot, move); handled = true; }
                if (!handled && HasTargetMarkRecipe(move.Effect)) { ExecuteTargetMark(invocation.Actor, target, invocation.ChosenSlot, move, purpose); handled = true; }
                if (!handled && HasMoveRestrictionRecipe(move.Effect))
                { ExecuteMoveRestriction(invocation.Actor, target, invocation.PpSlot, move, purpose); handled = true; }
                if (!handled && HasAbilityExchangeRecipe(move.Effect))
                { ExecuteAbilityExchange(invocation.Actor, target, invocation.PpSlot, move); handled = true; }
                if (!handled && HasCopyStateRecipe(move.Effect))
                { ExecuteCopyState(catalog, invocation.Actor, target, invocation.PpSlot, move); handled = true; }
                if (!handled) ExecuteTypeChange(catalog, invocation.Actor, invocation.ChosenSlot, move, purpose, ref handled);
                if (!handled) ExecuteFieldSetting(invocation.Actor, invocation.ChosenSlot, move, ref handled);
                if (!handled && HasFieldUtilityRecipe(move.Effect)) { ExecuteFieldUtility(catalog, invocation.Actor, target, invocation.PpSlot, move, purpose); handled = true; }
                if (!handled && HasRechargeRampageRecipe(move.Effect))
                { ExecuteRechargeRampage(catalog, invocation.Actor, move, purpose); handled = true; }
                if (!handled) ExecuteCharge(catalog,invocation.Actor,move,purpose,ref handled);
                if (!handled && HasLateTurnRecipe(move.Effect)) { ExecuteLateTurn(invocation.Actor, target, invocation.PpSlot, move); handled = true; }
                if (!handled) ExecuteTurnMove(catalog, invocation.Actor, invocation.ChosenSlot, move, purpose, ref handled);
                if (!handled) ExecuteBasicStat(invocation.Actor, invocation.ChosenSlot, move, purpose, ref handled);
                if (!handled) ExecuteAdditionalPrimaryStatus(catalog, invocation.Actor, invocation.ChosenSlot, move, purpose, ref handled);
                if (!handled) ExecuteStatRecovery(catalog, invocation.Actor, invocation.ChosenSlot, move, purpose, ref handled);
                if (!handled) ExecuteRetaliation(catalog, invocation.Actor, invocation.ChosenSlot, move, purpose, ref handled);
                if (!handled && HasWeatherHitRecipe(move.Effect)) { ExecuteWeatherHit(catalog, invocation.Actor, target, invocation.ChosenSlot, move, purpose); handled = true; }
                if (!handled && HasGroundSpreadRecipe(move.Effect)) { ExecuteGroundSpread(catalog, invocation.Actor, target, invocation.ChosenSlot, move, purpose); handled = true; }
                if (!handled && HasMiscTargetRecipe(move.Effect)) { ExecuteMiscTarget(catalog, invocation.Actor, target, invocation.ChosenSlot, move, purpose); handled = true; }
                if (!handled && HasDynamicTypeRecipe(move.Effect)) { ExecuteDynamicType(catalog, invocation.Actor, target, invocation.ChosenSlot, move, purpose); handled = true; }
                if (!handled && HasConditionalHitRecipe(move.Effect)) { ExecuteConditionalHit(catalog, invocation.Actor, target, invocation.ChosenSlot, move, purpose); handled = true; }
                if (!handled && HasDynamicPowerRecipe(move.Effect)) { ExecuteDynamicPower(catalog, invocation.Actor, target, invocation.ChosenSlot, move, purpose); handled = true; }
                if (!handled && HasItemTransferRecipe(move.Effect)) { ExecuteItemTransfer(catalog, invocation.Actor, target, invocation.ChosenSlot, move, purpose); handled = true; }
                if (!handled && HasHitSequenceRecipe(move.Effect)) { ExecuteHitSequence(catalog, invocation.Actor, target, invocation.ChosenSlot, move, purpose); handled = true; }
                if (!handled && HasRecoilRecipe(move.Effect)) { ExecuteRecoil(catalog, invocation.Actor, target, invocation.ChosenSlot, move, purpose); handled = true; }
                if (!handled && HasFixedDamageRecipe(move.Effect)) { ExecuteFixedDamage(catalog, invocation.Actor, target, invocation.ChosenSlot, move, purpose); handled = true; }
                if (!handled && HasPursuitBeatUpRecipe(move.Effect)) { ExecutePursuitBeatUp(catalog, invocation.Actor, target, invocation.ChosenSlot, move, purpose); handled = true; }
                if (!handled) ExecuteGeneric(catalog, invocation.Actor, invocation.ChosenSlot, move, purpose, ref handled);
                if (!handled) throw new SourceRecipeUnsupported("source_kernel_recipe_not_migrated");
            }
            if (completed == completedTargetPasses) CompleteScriptTarget();
        }
    }
    private void FinishAction()
    {
        RecordRampageInability();
        ClearContactReceipts();
        // Party storage stayed synchronized during each mutation; report the same action-end delta
        // once per actual member. Equal species never serve as the identity key.
        if (Frame.Layout == SourceBattleLayout.PresetDoubles || Frame.SinglesEncoding?.HasPartyExtension == true)
            foreach (var slot in ActiveSlots)
            {
                var key = Frame.Get(slot).Member!.Value;
                // A forced switch can already report its entrant's hazard delta. Continue from
                // that last reported member, so later move-end item changes remain distinct.
                var before = Events.OfType<SourceCalledPartyChangedEvent>()
                    .LastOrDefault(e => e.Side == key.Side && e.PartyIndex == key.Index)?.After
                    ?? initial.Party(key.Side).Members[key.Index];
                var after = Frame.Party(key.Side).Members[key.Index];
                if (!WildBattleSourcePartyEffects.SameMember(before, after))
                    Add(n => new SourceCalledPartyChangedEvent(n, Turn, key.Side, key.Index, "active-sync", before, after));
            }
        Frame = Frame with { Revision = checked(Frame.Revision + 1) };
    }
    internal override bool CancellerHasRecharge(BattlerSlot slot) => Frame.Get(slot).Registers.Turn?.Lock?.Kind == WildBattleCalledLockKind.Recharge;
    internal override void CancellerClearRecharge(BattlerSlot slot)
    {
        var cell = Frame.Get(slot); var turn = cell.Registers.Turn!; var locked = turn.Lock!;
        Frame = Frame with { Slots = Frame.Slots.SetItem(slot.Value, cell with { Registers = cell.Registers with {
            Turn = turn.SureHitTurnsRemaining == 0 ? null : turn with { Lock = null } } }) };
        Add(n => new SourceMtMoonVolatileEvent(n, Turn, slot, "called-recharge", locked.TurnsRemaining, 0, "recharged"));
    }
    internal override bool HelpingHand(BattlerSlot slot) => Frame.Get(slot).Registers.HelpingHand;
    internal override bool AllySupportAbsent(BattlerSlot slot) => Frame.IsSourceAbsent(slot);
    internal override void SetHelpingHand(BattlerSlot slot)
    {
        var cell = Frame.Get(slot);
        Frame = Frame with { Slots = Frame.Slots.SetItem(slot.Value, cell with { Registers = cell.Registers with { HelpingHand = true } }) };
    }
    internal override int FollowMeTimer(BattleSide side) => Frame.Sides[(int)side].FollowMeTimer;
    internal override void SetFollowMe(BattlerSlot slot) => Frame = Frame with {
        Sides = Frame.Sides.SetItem((int)slot.Side, Frame.Sides[(int)slot.Side] with { FollowMeTimer = 1, FollowMeTarget = slot }) };
    internal override void SetAllySupportTarget(BattlerSlot slot) => invocation = invocation with { Target = slot };
    internal override bool SourceDoubleBattle => Frame.Layout == SourceBattleLayout.PresetDoubles;
    internal override int SourceDefenderCount(BattlerSlot target) => SourceMoveTargeting.CountSourcePresentDefenders(Frame, target);
    internal override IEnumerable<BattlerSlot> SourceNumericTargets(BattlerSlot actor, BattlerSlot initialTarget) =>
        SourceDoubleBattle ? SourceMoveTargeting.NumericScriptTargetsAfterDeferredKo(Frame, actor) : [initialTarget];
    internal override void BeginScriptTarget(BattlerSlot target)
    {
        invocation = invocation with { Target = target };
        commandNoEffect = false; hitItemsNoEffect = false; skipsMoveEnd = false;
        targetEventStart = Events.Count;
    }
    internal override void CompleteScriptTarget() => CompleteScriptTarget(historyOnly: false);
    private void CompleteScriptTarget(bool historyOnly)
    {
        if (Frame.PendingSwitches.Any(p => p.Kind == WildBattleCalledSwitchKind.BatonPass && p.Slot == action.Actor.Slot))
        {
            Set(action.Actor.Slot, Get(action.Actor.Slot) with { LastUsedMove = initial.Get(action.Actor.Slot).Body!.LastUsedMove });
            completedTargetPasses++;
            return;
        }
        if (skipsMoveEnd) Set(action.Actor.Slot, Get(action.Actor.Slot) with { LastUsedMove = initial.Get(action.Actor.Slot).Body!.LastUsedMove });
        else
        {
            if (!historyOnly)
            {
                FireHitThaw(invocation.Actor, invocation.Target, move);
                var immunity = SourceFrameImmunityRules.Apply(Frame);
                Frame = immunity.State; Events.AddRange(immunity.Events);
                FinishChangedItems();
                var berries = SourceFrameBerryItems.Apply(Frame, SourceBerryPhase.MoveEnd);
                Frame = berries.State; Events.AddRange(berries.Events);
                ApplyFrameHitItems();
                EndSubstitutePass();
            }
            FinishPreparedRegisters();
            Frame = SourceMoveHistoryReducer.Apply(catalog, Frame, invocation,
                Events.Skip(targetEventStart).ToImmutableArray(), commandNoEffect, DynamicHitType(move), wasCalled: calledMoveExecuted);
        }
        completedTargetPasses++;
    }
    internal override bool UproarPreventsSleep(BattlerSlot slot) => SharedUproarPreventsSleep(slot);
    internal override void SynchronizeStatus(PokemonCatalog catalog, BattlerSlot target, uint status)
    {
        if (WildBattleEffective.Ability(Get(target)) != "ABILITY_SYNCHRONIZE") return;
        Add(n => new SourceAbilityActivatedEvent(n, Turn, target, "ABILITY_SYNCHRONIZE"));
        var source = target == invocation.Actor ? invocation.Target : invocation.Actor;
        InflictStatus(catalog, source, status, true, "synchronize");
    }
    internal override int Turn => Frame.Turn;
    internal override SourceMoveInvocation? Invocation => invocation;
    internal override IEnumerable<BattlerSlot> ActiveSlots => Enumerable.Range(0, Frame.BattlerCount).Select(i => new BattlerSlot(i)).Where(s => Frame.Get(s).Body is not null);
    internal override WildBattleCombatant Get(BattlerSlot slot) => Frame.Get(slot).Body ?? throw new SourceRecipeUnsupported("source_kernel_absent_body");
    internal override void Set(BattlerSlot slot, WildBattleCombatant body)
    {
        var cell = Frame.Get(slot); var key = cell.Member!.Value; var party = Frame.Party(key.Side);
        var member = party.Members[key.Index] with { Pokemon = WildBattleTransformationRules.PersistentPokemon(body),
            Status = WildBattleTransformationRules.PersistentStatus(body), HeldItem = body.HeldItem, KnockedOffItem = body.KnockedOffItem };
        Frame = Frame with { Slots = Frame.Slots.SetItem(slot.Value, cell with { Body = body }),
            Parties = Frame.Parties.SetItem((int)key.Side, party with { Members = party.Members.SetItem(key.Index, member) }) };
    }
    internal override ushort Draw(string purpose, int bound = 65536)
    {
        var next = Gen3Rng.Next(Frame.Rng, purpose, bound); Frame = Frame with { Rng = next.State }; Trace.Add(next.Trace); return next.Value;
    }
    internal override void Add(Func<long, SourceBattleEvent> create)
    {
        var published = SourceBattleEventHooks.Publish(Frame, (n, _) => create(n)); Frame = published.State;
        Events.Add(published.Event);
        // Canonical event first: bookkeeping never recovers an actor from a side/species.
        if (published.Event is SourceHpChangedEvent hp && hp.Actor != hp.Target && hp.HpAfter < hp.HpBefore)
            Set(hp.Target, Get(hp.Target) with { DamagedByOpponentThisTurn = true });
        if (published.Event is SourceHpChangedEvent lateHit) RecordLateTurnDamage(lateHit);
        if (published.Event is SourceMoveUsedEvent used) Set(used.Actor, Get(used.Actor) with { LastUsedMove = used.Move });
    }
    internal override void RecordAbility(BattlerSlot slot, string ability) { } // Explicit events are retained for the future AI owner.
    internal override void SpikesResultMessage(BattlerSlot actor) =>
        Add(n => new SourceMtMoonVolatileEvent(n, Turn, actor, "spikes", 0, 1, "move"));
    internal override int SpikesLayers(BattleSide side) => Frame.Sides[(int)side].Spikes;
    internal override void SetSpikesLayers(BattleSide side, int layers) => Frame = Frame with {
        Sides = Frame.Sides.SetItem((int)side, Frame.Sides[(int)side] with { Spikes = layers }) };
    internal override void SpikesSuppressPressure() => invocation = invocation with {
        Flags = invocation.Flags | WildBattleInvocationFlags.PpNotAffectedByPressure };
    internal override int FieldSettingTimer(BattleSide side, string effect) => new FrameEndTurnContext(Frame).GetTimer(side, effect);
    internal override void SetFieldSettingTimer(BattleSide side, string effect, int value)
    {
        var storage = new FrameEndTurnContext(Frame); storage.SetTimer(side, effect, value); Frame = storage.Frame;
    }
    internal override SourceFieldRegisters SettingWeather => Frame.Field;
    internal override void ReplaceSettingWeather(SourceFieldRegisters weather) => Frame = Frame with { Field = weather };
    internal override void SettingWeatherChanged()
    {
        var entry = new FrameEntryContext(Frame); entry.ForecastAll();
        Frame = entry.Frame; Events.AddRange(entry.Events); Trace.AddRange(entry.Trace);
    }
    internal override int SandstormTurnsRemaining => Frame.Field.SandstormTurnsRemaining;
    internal override bool StruggleSandstorm => SourceWeatherRules.HasSandstorm(Frame.Field);
    internal override bool Safeguard(BattlerSlot slot) => Frame.Sides[(int)slot.Side].SafeguardTurnsRemaining > 0;
    internal override int RainTurnsRemaining => Frame.Field.RainTurnsRemaining;
    internal override bool SourceCalledRain => SourceWeatherRules.HasRain(Frame.Field);
    internal override bool SourceCalledSun => SourceWeatherRules.HasSun(Frame.Field);
    internal override bool SourceCalledWeatherPresent => SourceCalledSun || SourceCalledRain || Frame.Field.HailTurnsRemaining > 0 || SourceWeatherRules.HasSandstorm(Frame.Field);
    internal override bool SourceCalledWeatherSuppressed => SourceWeatherRules.IsSuppressed(Frame);
    internal override bool SourceDamageWeatherSuppressed => SourceWeatherRules.IsSuppressed(Frame, includeFainted: true);
    internal override bool RockTunnelReflects(BattlerSlot slot) => Frame.Sides[(int)slot.Side].ReflectTurnsRemaining > 0;
    internal override bool SourceCalledLightScreen(BattlerSlot slot) => Frame.Sides[(int)slot.Side].LightScreenTurnsRemaining > 0;
    internal override bool SourceCalledMist(BattlerSlot slot) => Frame.Sides[(int)slot.Side].MistTurnsRemaining > 0;
    internal override bool SourceCalledSecondaryBlocked(BattlerSlot slot) => SubstitutePresent(slot);
    internal override bool SourceCalledHitReachedPokemon(BattlerSlot slot) => SubstituteHitReachedPokemon(slot);
    internal override bool SourceCalledSafeguardBlocks(BattlerSlot actor, BattlerSlot target, MoveId move)
    {
        if (Frame.Sides[(int)target.Side].SafeguardTurnsRemaining == 0) return false;
        skipsMoveEnd = true;
        SourceCalledProtectionFailure(actor, target, move, "safeguard"); return true;
    }
    internal override void SourceCalledProtectionFailure(BattlerSlot actor, BattlerSlot target, MoveId move, string reason) =>
        Add(n => new SourceMoveFailedEvent(n, Turn, actor, target, move, reason));
    internal override bool CalledSureHit(BattlerSlot actor, BattlerSlot target) => Frame.Get(target).Registers.Turn is { SureHitTurnsRemaining: > 0 } t && t.SureHitSource == actor;
    internal override bool BillUnderground(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move) =>
        actor != target && !CalledSureHit(actor,target) && SourceChargeRules.Misses(SemiInvulnerable(target),move);


    internal override void SpendEarlyPartyPp(BattlerSlot actor, BattlerSlot target, int slot, PokemonMoveDefinition move) => SpendFramePp(actor,target,move,true);
    private void SpendFramePp(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, bool attackString)
    {
        invocation = invocation with { Target = target };
        bool printed = attackString && (invocation.Flags & (WildBattleInvocationFlags.AttackStringPrinted | WildBattleInvocationFlags.NoAttackString)) == 0;
        if (printed)
        {
            Add(n => new SourceAttackStringEvent(n, Turn, actor, invocation.ChosenMove, move.Id));
            invocation = invocation with { Flags = invocation.Flags | WildBattleInvocationFlags.AttackStringPrinted };
        }
        var owner = Get(invocation.PpOwner); bool realSlot = invocation.PpSlot >= 0;
        int before = realSlot ? owner.Pokemon.Moves[invocation.PpSlot].CurrentPp : 0;
        int opponents = ActiveSlots.Count(s => s.Side != actor.Side && WildBattleEffective.Ability(Get(s)) == "ABILITY_PRESSURE");
        int others = ActiveSlots.Count(s => s != actor && WildBattleEffective.Ability(Get(s)) == "ABILITY_PRESSURE");
        var reduced = WildBattleSourceCommands.ReducePp(before, move.Target, actor, target, WildBattleEffective.Ability(Get(target)), opponents, others, invocation.Flags);
        if (realSlot) Set(invocation.PpOwner, owner with { Pokemon = owner.Pokemon with { Moves = owner.Pokemon.Moves.SetItem(invocation.PpSlot,
            owner.Pokemon.Moves[invocation.PpSlot] with { CurrentPp = reduced.After }) } });
        invocation = invocation with { Flags = reduced.FlagsAfter };
        if (printed) Add(n => new SourceMoveUsedEvent(n, Turn, actor, target, invocation.PpSlot, move.Id, before, reduced.After));
        Add(n => new SourcePpCommandEvent(n, Turn, actor, target, invocation.PpSlot, invocation.ChosenMove, move.Id, before, reduced.After, reduced.CommandCost, Frame.Rng.Cursor) { PpOwner = invocation.PpOwner });
    }

    internal override int SourceGenericApplyDamage(BattlerSlot actor, BattlerSlot target, MoveId move, int damage, bool critical, int percent)
    {
        if (ConsumeSubstituteDamage(actor, target, move, damage, critical, percent, out int substituteLost,
            noEffect: fixedDamageNoEffect ?? (damage == 0 && !HeldDamageEffectiveZero(target))))
        { RecordContactDamage(target, move, damage, substituteLost, substitute: true); return substituteLost; }
        var body = Get(target); int before = body.Pokemon.CurrentHp, after = Math.Max(0, before - damage);
        Set(target, body with { Pokemon = body.Pokemon with { CurrentHp = after } });
        RecordContactDamage(target, move, damage, before - after);
        Add(n => new SourceHpChangedEvent(n, Turn, actor, target, move, before, after, damage, critical, percent));
        if (critical && damage > 0) Add(n => new SourceCriticalHitEvent(n, Turn, actor, target));
        return before - after;
    }
    internal override bool Route11WaterAbsorb(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move)
    {
        string ability = WildBattleEffective.Ability(Get(target));
        if (move.Power == 0 || !(ability == "ABILITY_WATER_ABSORB" && move.Type == "TYPE_WATER" || ability == "ABILITY_VOLT_ABSORB" && move.Type == "TYPE_ELECTRIC")) return false;
        commandNoEffect = true;
        Add(n => new SourceAbilityActivatedEvent(n, Turn, target, ability));
        Add(n => new SourceAbilityPreventedEvent(n, Turn, actor, target, move.Id, ability));
        var body = Get(target); int before = body.Pokemon.CurrentHp, after = Math.Min(body.Pokemon.Stats.MaxHp, before + Math.Max(1, body.Pokemon.Stats.MaxHp / 4));
        if (after != before) { Set(target, body with { Pokemon = body.Pokemon with { CurrentHp = after } }); Add(n => new SourceRecoveredEvent(n, Turn, target, move.Id, before, after, after - before)); }
        return true;
    }
    internal override bool SsAnneFlashFire(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move)
    {
        if (move.Type != "TYPE_FIRE" || WildBattleEffective.Ability(Get(target)) != "ABILITY_FLASH_FIRE" || Get(target).Status == WildBattleStatus.Freeze) return false;
        Set(target, Get(target) with { FlashFireBoost = true });
        Add(n => new SourceAbilityActivatedEvent(n, Turn, target, "ABILITY_FLASH_FIRE"));
        Add(n => new SourceAbilityPreventedEvent(n, Turn, actor, target, move.Id, "ABILITY_FLASH_FIRE")); return true;
    }
    internal override void ChangeStatus(BattlerSlot target, uint status, string reason)
    {
        var body = Get(target); if (body.Status == status) return;
        if (body.Status == 0 && (WildBattleStatus.IsSleeping(status) || status == WildBattleStatus.Freeze))
            CancellerCancelMultiTurn(target, status == WildBattleStatus.Freeze ? "freeze" : "sleep");
        if (body.Nightmare && !WildBattleStatus.IsSleeping(status))
        {
            Set(target, Get(target) with { Nightmare = false });
            Add(n => new SourceTowerVolatileEvent(n, Turn, target, "nightmare", false, reason));
        }
        Set(target, Get(target) with { Status = status, SourcePartyStatusOverride = null });
        Add(n => new SourceStatusChangedEvent(n, Turn, target, body.Status, status, reason));
    }
    internal override void ChangeStage(BattlerSlot actor, BattlerSlot target, WildBattleStat stat, int amount, MoveId move, bool secondary = false) =>
        SourceChangeStat(actor, target, stat, amount, move, secondary);
    internal override void ApplyConfusion(BattlerSlot target, string purpose, bool secondary)
    {
        var body = Get(target);
        if (body.Pokemon.CurrentHp == 0 || body.ConfusionTurnsRemaining > 0 || WildBattleEffective.Ability(body) == "ABILITY_OWN_TEMPO" ||
            secondary && (SourceCalledSecondaryBlocked(target) || WildBattleEffective.Ability(body) == "ABILITY_SHIELD_DUST" || Frame.Sides[(int)target.Side].SafeguardTurnsRemaining > 0)) return;
        int turns = 2 + (Draw($"{purpose}.confusion-duration", 4) & 3); Set(target, body with { ConfusionTurnsRemaining = turns });
        Add(n => new SourceConfusionChangedEvent(n, Turn, target, 0, turns, "move"));
    }
    internal override bool CeruleanScript(PokemonCatalog catalog, BattlerSlot actor, int slot, PokemonMoveDefinition move)
    {
        if (move.Effect != "EFFECT_RESTORE_HP") throw new SourceRecipeUnsupported("source_kernel_script_not_migrated");
        SpendEarlyPartyPp(actor, actor, slot, move); SourceStatRecoveryHeal(actor, move, Math.Max(1, Get(actor).Pokemon.Stats.MaxHp / 2)); return true;
    }
    internal override void ForestFaint(BattlerSlot slot)
    {
        // Historical singles preserve their existing cleanup/outcome adapter. Doubles uses
        // source FaintClearSetData here; only replacement scheduling is deferred by M03.
        if (Frame.Layout == SourceBattleLayout.Singles) throw new SourceRecipeUnsupported("source_kernel_singles_faint_compatibility");
        if (Get(slot).Pokemon.CurrentHp != 0) throw new SourceRecipeUnsupported("source_kernel_faint_requires_zero_hp");
        if (Frame.IsSourceAbsent(slot)) return; // Cmd_tryfaintmon tests absence, independently of HP.
        ResolveLateTurnFaint(slot);
        Frame = SourceBattleIdentityRules.ClearDepartureReferences(Frame, slot, SourceDepartureKind.Faint);
        Set(slot, SourceFaintCommands.ClearCanonicalBody(Get(slot)));
        ChangeStatus(slot, WildBattleStatus.None, "faint");
        Add(n => new SourceFaintedEvent(n, Turn, slot));
    }
    internal override void ForestOutcome(bool allowReplacement) { }
    internal override void MtMoonRageOnDamage(BattlerSlot actor, BattlerSlot target, MoveId move) => BuildRage(actor, target, move);
    internal override void SourceCalledMoveEndSubstitute() => EndSubstitutePass();
    internal override bool SourceHeldDamageEnabled => Frame.Layout == SourceBattleLayout.PresetDoubles;
    internal override bool Endured(BattlerSlot slot) => HeldDamageEndured(slot);
    internal override void SourceDamageResultMessage(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move)
    {
        if (commandNoEffect) return;
        // Cmd_resultmessage follows HP update, never the earlier adjustment command.
        // Endure takes precedence; each successful target pass has its own result flags.
        // Explosion can adjust then miss, and must not print a survival result for that pass.
        if (HeldDamageEndured(target))
            Add(n => new SourceMtMoonVolatileEvent(n, Turn, target, "endure", 1, 1, "endured"));
        else if (HeldDamageHungOn(target))
            Add(n => new SourceHeldItemSurvivalEvent(n, Turn, target, move.Id, Get(target).HeldItem));
    }
    internal override void SourceGenericMiss(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move)
    {
        commandNoEffect = true;
        if (Get(target).Protected && move.Flags.Contains("FLAG_PROTECT_AFFECTED") && !ChargeFirstTurn(move))
        {
            MarkProtectionMiss(target);
            Add(n => new SourceMoveFailedEvent(n, Turn, actor, target, move.Id, "protected"));
        }
        else base.SourceGenericMiss(actor, target, move);
    }
    private void MarkProtectionMiss(BattlerSlot target)
    {
        commandNoEffect = true;
        var cell = Frame.Get(target);
        // attackcanceler/JumpIfMoveFailed clear the source last-hit type to numeric 0.
        // MOVEEND later writes MOVE_UNAVAILABLE as the failed last-landed move.
        Frame = Frame with { Slots = Frame.Slots.SetItem(target.Value, cell with { Registers = cell.Registers with {
            History = (cell.Registers.History ?? new()) with { LastHitByType = "TYPE_NORMAL" } } }) };
    }
    internal override bool Route11Damp(BattlerSlot actor, PokemonMoveDefinition move)
    {
        // Cmd_tryexplosion scans stored abilities before selecting the numeric first target.
        var damp = ActiveSlots.Where(s => WildBattleEffective.Ability(Get(s)) == "ABILITY_DAMP").Cast<BattlerSlot?>().FirstOrDefault();
        if (damp is not { } holder) return false;
        Add(n => new SourceAbilityPreventedEvent(n, Turn, actor, holder, move.Id, "ABILITY_DAMP"));
        return true;
    }
    internal override void ApplyFlinch(BattlerSlot target)
    {
        if (Frame.FourTurn?.Phase != SourceFourActionPhase.Actions || action.ActionOrderIndex != Frame.NextAction ||
            Frame.Queue[Frame.NextAction].Token?.Owner != action.Actor)
            throw new SourceRecipeUnsupported("source_kernel_flinch_order_not_migrated");
        var body = Get(target);
        bool alreadyActed = Frame.Queue.Take(Frame.NextAction + 1).Any(a => a.Slot == target);
        if (SourceCalledSecondaryBlocked(target) || body.Pokemon.CurrentHp == 0 || alreadyActed || WildBattleEffective.Ability(body) is "ABILITY_SHIELD_DUST" or "ABILITY_INNER_FOCUS") return;
        Set(target, body with { Flinched = true });
        Add(n => new SourceFlinchEvent(n, Turn, target, true));
    }
    internal override bool EarlyStarterPrimaryMove(PokemonCatalog catalog, BattlerSlot actor, int slot, PokemonMoveDefinition move, string purpose) => throw new SourceRecipeUnsupported("source_kernel_primary_status_not_migrated");
    internal override bool EarlyPartyScript(PokemonCatalog catalog, BattlerSlot actor, int slot, PokemonMoveDefinition move, string purpose) => throw new SourceRecipeUnsupported("source_kernel_legacy_script_not_migrated");
    internal override bool TowerScript(PokemonCatalog catalog, BattlerSlot actor, int slot, PokemonMoveDefinition move, string purpose) => throw new SourceRecipeUnsupported("source_kernel_legacy_script_not_migrated");
}
