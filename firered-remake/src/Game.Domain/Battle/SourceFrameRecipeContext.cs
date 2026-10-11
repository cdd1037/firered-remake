using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Rules;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal sealed class FrameRecipeContext : SourceRecipeContext
{
    private readonly PokemonCatalog catalog;
    private readonly SourceResolvedMoveAction action;
    private readonly SourceBattleFrame initial;
    private readonly PokemonMoveDefinition move;
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
        move = delayedMove ?? catalog.Moves[Get(action.Actor.Slot).Pokemon.Moves[action.ChosenSlot].Move];
        invocation = new(action.Actor.Slot, action.ChosenSlot, action.ChosenSlot, move.Id, move.Id, move.Id, null,
            action.Target, WildBattleAttackCancellerPhase.ConditionsPassed, WildBattleInvocationFlags.Obeys);
    }
    // BattleScript_MonTookFutureAttack reuses accuracycheck and the ordinary damage update.
    // Admission excludes Focus Band, Substitute, Endure and move-end item side effects.
    internal void RunFutureHit(SourceFutureAttack future, BattlerSlot target)
    {
        string purpose = $"source-battle.slot-{future.Attacker.Value}.future-attack";
        var hit = PrepareFutureHit(future.Attacker, target, move, future.Damage, purpose);
        if (!hit.Hit) return;
        int lost = SourceGenericApplyDamage(future.Attacker, target, move, hit.Damage, false, hit.Percent);
        var cell = Frame.Get(target); var prior = cell.Registers.Damage;
        Frame = Frame with { Slots = Frame.Slots.SetItem(target.Value, cell with { Registers = cell.Registers with {
            Damage = SourceMoveHistoryReducer.RecordDamage(prior, Turn, future.Attacker, lost, "TYPE_NORMAL") } }) };
    }
    // The approved deferred-KO policy can leave all target positions unavailable. Run the
    // original ButItFailedAtkStringPpReduce commands against the attempted retained slot;
    // there is no hit/accuracy/secondary-effect recipe and no newly selected recipient.
    internal void RunNoTarget()
    {
        BeginScriptTarget(action.Target);
        SpendEarlyPartyPp(action.Actor.Slot, action.Target, action.ChosenSlot, move);
        Add(n => new SourceMoveFailedEvent(n, Turn, action.Actor.Slot, action.Target, move.Id, "deferred-ko-no-target"));
        commandNoEffect = true;
        CompleteScriptTarget();
        FinishAction();
    }
    internal void Run()
    {
        string purpose = Frame.Layout == SourceBattleLayout.Singles
            ? $"wild-battle.{(action.Actor.Slot.Side == BattleSide.Player ? "player" : "opponent")}" : $"source-battle.slot-{action.Actor.Slot.Value}";
        var firstTarget = invocation.Target;
        ExecutePass(firstTarget);
        // MOVEEND_NEXT_TARGET uses HP only. It does not test source absence or restart action
        // ownership, so PP exhaustion and the attacker's zero HP cannot cancel the second pass.
        if (Frame.Layout == SourceBattleLayout.PresetDoubles && move.Target == "MOVE_TARGET_BOTH" &&
            Frame.Get(firstTarget.Partner).Body?.Pokemon.CurrentHp > 0)
        {
            invocation = invocation with { Flags = invocation.Flags | WildBattleInvocationFlags.NoAttackString };
            ExecutePass(firstTarget.Partner);
        }
        void ExecutePass(BattlerSlot target)
        {
            BeginScriptTarget(target);
            int completed = completedTargetPasses;
            if (WildBattleSourceCommands.IsSoundMove(move.Id) && WildBattleEffective.Ability(Get(target)) == "ABILITY_SOUNDPROOF")
            {
                if ((invocation.Flags & WildBattleInvocationFlags.NoAttackString) == 0)
                    invocation = invocation with { Flags = invocation.Flags & ~WildBattleInvocationFlags.Obeys };
                SpendEarlyPartyPp(invocation.Actor, target, invocation.PpSlot, move);
                Add(n => new SourceAbilityPreventedEvent(n, Turn, invocation.Actor, target, move.Id, "ABILITY_SOUNDPROOF"));
            }
            else if (Get(invocation.Actor).Pokemon.Moves[invocation.PpSlot].CurrentPp == 0 &&
                (invocation.Flags & WildBattleInvocationFlags.NoAttackString) == 0)
            {
                invocation = invocation with { Flags = WildBattleInvocationFlags.AttackStringPrinted };
                Add(n => new SourceAttackStringEvent(n, Turn, invocation.Actor, move.Id, move.Id));
                Add(n => new SourceMoveFailedEvent(n, Turn, invocation.Actor, target, move.Id, "no-pp"));
            }
            else
            {
                invocation = invocation with { Flags = invocation.Flags | WildBattleInvocationFlags.Obeys };
                if (action.LightningRodRedirected)
                    Add(n => new SourceAbilityActivatedEvent(n, Turn, target, "ABILITY_LIGHTNING_ROD"));
                bool handled = false;
                ExecuteFieldSetting(invocation.Actor, invocation.ChosenSlot, move, ref handled);
                if (!handled) ExecuteStatRecovery(catalog, invocation.Actor, invocation.ChosenSlot, move, purpose, ref handled);
                if (!handled) ExecuteGeneric(catalog, invocation.Actor, invocation.ChosenSlot, move, purpose, ref handled);
                if (!handled) throw new SourceRecipeUnsupported("source_kernel_recipe_not_migrated");
            }
            if (completed == completedTargetPasses) CompleteScriptTarget();
        }
        FinishAction();
    }
    private void FinishAction()
    {
        // Party storage stayed synchronized during each mutation; report the same action-end delta
        // once per actual member. Equal species never serve as the identity key.
        if (Frame.Layout == SourceBattleLayout.PresetDoubles || Frame.SinglesEncoding?.HasPartyExtension == true)
            foreach (var slot in ActiveSlots)
            {
                var key = Frame.Get(slot).Member!.Value;
                var before = initial.Party(key.Side).Members[key.Index]; var after = Frame.Party(key.Side).Members[key.Index];
                if (!WildBattleSourcePartyEffects.SameMember(before, after))
                    Add(n => new SourceCalledPartyChangedEvent(n, Turn, key.Side, key.Index, "active-sync", before, after));
            }
        Frame = Frame with { Revision = checked(Frame.Revision + 1) };
    }
    internal override bool SourceDoubleBattle => Frame.Layout == SourceBattleLayout.PresetDoubles;
    internal override int SourceDefenderCount(BattlerSlot target) => SourceMoveTargeting.CountSourcePresentDefenders(Frame, target);
    internal override IEnumerable<BattlerSlot> SourceNumericTargets(BattlerSlot actor, BattlerSlot initialTarget) =>
        SourceDoubleBattle ? SourceMoveTargeting.NumericScriptTargets(Frame, actor) : [initialTarget];
    internal override void BeginScriptTarget(BattlerSlot target)
    {
        invocation = invocation with { Target = target };
        commandNoEffect = false; skipsMoveEnd = false;
        targetEventStart = Events.Count;
    }
    internal override void CompleteScriptTarget()
    {
        if (skipsMoveEnd) Set(action.Actor.Slot, Get(action.Actor.Slot) with { LastUsedMove = initial.Get(action.Actor.Slot).Body!.LastUsedMove });
        else Frame = SourceMoveHistoryReducer.Apply(catalog, Frame, invocation,
            Events.Skip(targetEventStart).ToImmutableArray(), commandNoEffect);
        completedTargetPasses++;
    }
    internal override bool UproarPreventsSleep(BattlerSlot slot) => false;
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
        if (published.Event is SourceMoveUsedEvent used) Set(used.Actor, Get(used.Actor) with { LastUsedMove = used.Move });
    }
    internal override void RecordAbility(BattlerSlot slot, string ability) { } // Explicit events are retained for the future AI owner.
    internal override int FieldSettingTimer(BattleSide side, string effect) => new FrameEndTurnContext(Frame).GetTimer(side, effect);
    internal override void SetFieldSettingTimer(BattleSide side, string effect, int value)
    {
        var storage = new FrameEndTurnContext(Frame); storage.SetTimer(side, effect, value); Frame = storage.Frame;
    }
    internal override SourceFieldRegisters SettingWeather => Frame.Field;
    internal override void ReplaceSettingWeather(SourceFieldRegisters weather) => Frame = Frame with { Field = weather };
    internal override int SandstormTurnsRemaining => Frame.Field.SandstormTurnsRemaining;
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
    internal override bool SourceCalledSecondaryBlocked(BattlerSlot slot) => false; // Substitute branch is gated at admission.
    internal override bool SourceCalledHitReachedPokemon(BattlerSlot slot) => true;
    internal override bool SourceCalledSafeguardBlocks(BattlerSlot actor, BattlerSlot target, MoveId move)
    {
        if (Frame.Sides[(int)target.Side].SafeguardTurnsRemaining == 0) return false;
        skipsMoveEnd = true;
        SourceCalledProtectionFailure(actor, target, move, "safeguard"); return true;
    }
    internal override void SourceCalledProtectionFailure(BattlerSlot actor, BattlerSlot target, MoveId move, string reason) =>
        Add(n => new SourceMoveFailedEvent(n, Turn, actor, target, move, reason));
    internal override bool CalledSureHit(BattlerSlot actor, BattlerSlot target) => Frame.Get(target).Registers.Turn is { SureHitTurnsRemaining: > 0 } t && t.SureHitSource == actor;
    internal override bool BillUnderground(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move) => false;


    internal override void SpendEarlyPartyPp(BattlerSlot actor, BattlerSlot target, int slot, PokemonMoveDefinition move)
    {
        invocation = invocation with { Target = target };
        bool printed = (invocation.Flags & (WildBattleInvocationFlags.AttackStringPrinted | WildBattleInvocationFlags.NoAttackString)) == 0;
        if (printed)
        {
            Add(n => new SourceAttackStringEvent(n, Turn, actor, invocation.ChosenMove, move.Id));
            invocation = invocation with { Flags = invocation.Flags | WildBattleInvocationFlags.AttackStringPrinted };
        }
        var owner = Get(invocation.PpOwner); int before = owner.Pokemon.Moves[invocation.PpSlot].CurrentPp;
        int opponents = ActiveSlots.Count(s => s.Side != actor.Side && WildBattleEffective.Ability(Get(s)) == "ABILITY_PRESSURE");
        int others = ActiveSlots.Count(s => s != actor && WildBattleEffective.Ability(Get(s)) == "ABILITY_PRESSURE");
        var reduced = WildBattleSourceCommands.ReducePp(before, move.Target, actor, target, WildBattleEffective.Ability(Get(target)), opponents, others, invocation.Flags);
        Set(invocation.PpOwner, owner with { Pokemon = owner.Pokemon with { Moves = owner.Pokemon.Moves.SetItem(invocation.PpSlot,
            owner.Pokemon.Moves[invocation.PpSlot] with { CurrentPp = reduced.After }) } });
        invocation = invocation with { Flags = reduced.FlagsAfter };
        if (printed) Add(n => new SourceMoveUsedEvent(n, Turn, actor, target, slot, move.Id, before, reduced.After));
        Add(n => new SourcePpCommandEvent(n, Turn, actor, target, invocation.PpSlot, invocation.ChosenMove, move.Id, before, reduced.After, reduced.CommandCost, Frame.Rng.Cursor) { PpOwner = invocation.PpOwner });
    }

    internal override int SourceGenericApplyDamage(BattlerSlot actor, BattlerSlot target, MoveId move, int damage, bool critical, int percent)
    {
        var body = Get(target); int before = body.Pokemon.CurrentHp, after = Math.Max(0, before - damage);
        Set(target, body with { Pokemon = body.Pokemon with { CurrentHp = after } });
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
        Set(target, body with { Status = status, SourcePartyStatusOverride = null });
        Add(n => new SourceStatusChangedEvent(n, Turn, target, body.Status, status, reason));
    }
    internal override void ChangeStage(BattlerSlot actor, BattlerSlot target, WildBattleStat stat, int amount, MoveId move, bool secondary = false) =>
        SourceChangeStat(actor, target, stat, amount, move, secondary);
    internal override void ApplyConfusion(BattlerSlot target, string purpose, bool secondary)
    {
        var body = Get(target);
        if (body.Pokemon.CurrentHp == 0 || body.ConfusionTurnsRemaining > 0 || WildBattleEffective.Ability(body) == "ABILITY_OWN_TEMPO" ||
            secondary && (WildBattleEffective.Ability(body) == "ABILITY_SHIELD_DUST" || Frame.Sides[(int)target.Side].SafeguardTurnsRemaining > 0)) return;
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
        Set(slot, SourceFaintCommands.ClearCanonicalBody(Get(slot)));
        Frame = SourceBattleIdentityRules.ClearDepartureReferences(Frame, slot, SourceDepartureKind.Faint);
        ChangeStatus(slot, WildBattleStatus.None, "faint");
        Add(n => new SourceFaintedEvent(n, Turn, slot));
    }
    internal override void ForestOutcome(bool allowReplacement) { }
    internal override void MtMoonRageOnDamage(BattlerSlot actor, BattlerSlot target, MoveId move) { }
    internal override void SourceCalledMoveEndSubstitute() { }
    internal override bool Endured(BattlerSlot slot) => false;
    internal override void Route3ContactAbility(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, string purpose, int damage) { }
    internal override bool Route11Damp(BattlerSlot actor, PokemonMoveDefinition move)
    {
        // Cmd_tryexplosion scans stored abilities before selecting the numeric first target.
        var damp = ActiveSlots.Where(s => WildBattleEffective.Ability(Get(s)) == "ABILITY_DAMP").Cast<BattlerSlot?>().FirstOrDefault();
        if (damp is not { } holder) return false;
        Add(n => new SourceAbilityPreventedEvent(n, Turn, actor, holder, move.Id, "ABILITY_DAMP"));
        return true;
    }
    internal override void ApplyFlinch(BattlerSlot target) => throw new SourceRecipeUnsupported("source_kernel_flinch_order_not_migrated");
    internal override void ApplyBinding(BattlerSlot target, string purpose, MoveId move) => throw new SourceRecipeUnsupported("source_kernel_binding_not_migrated");
    internal override bool EarlyStarterPrimaryMove(PokemonCatalog catalog, BattlerSlot actor, int slot, PokemonMoveDefinition move, string purpose) => throw new SourceRecipeUnsupported("source_kernel_primary_status_not_migrated");
    internal override bool EarlyPartyScript(PokemonCatalog catalog, BattlerSlot actor, int slot, PokemonMoveDefinition move, string purpose) => throw new SourceRecipeUnsupported("source_kernel_legacy_script_not_migrated");
    internal override bool TowerScript(PokemonCatalog catalog, BattlerSlot actor, int slot, PokemonMoveDefinition move, string purpose) => throw new SourceRecipeUnsupported("source_kernel_legacy_script_not_migrated");
}
