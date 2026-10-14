using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

/// <summary>An action after target resolution. Target selection/redirection and the four-action owner
/// are separate contracts; callers must not infer a unique opposing battler.</summary>
public sealed record SourceResolvedMoveAction(OccupantToken Actor, long Revision, int ChosenSlot,
    BattlerSlot Target, int ActionOrderIndex = 0)
{
    public bool LightningRodRedirected { get; init; }
}
public sealed record SourceKernelResult(SourceBattleFrame State, ImmutableArray<SourceBattleEvent> Events,
    ImmutableArray<RandomTrace> Trace, SourceMoveInvocation? Invocation, string? Fault)
{
    public bool Succeeded => Fault is null;
}

/// <summary>Bounded source action kernel, not general doubles/campaign admission. Every rejected
/// branch returns the identical immutable input and empty event/RNG arrays.</summary>
public static class SourceBattleKernel
{
    public const string Version = "firered-shared-recipe-slice-v1";
    public static string? ValidateAction(PokemonCatalog catalog, SourceBattleFrame frame, SourceResolvedMoveAction action) =>
        ValidateAction(catalog, frame, action, null);

    internal static string? ValidateCalledAction(PokemonCatalog catalog, SourceBattleFrame frame, SourceResolvedMoveAction action, PokemonMoveDefinition current) =>
        ValidateAction(catalog, frame, action, current);

    private static string? ValidateAction(PokemonCatalog catalog, SourceBattleFrame frame, SourceResolvedMoveAction action, PokemonMoveDefinition? called)
    {
        // Do not enter permanent-state projection/SequenceEqual with truncated deserialized
        // nested objects. The structural identity validator is intentionally not a move validator.
        if (catalog is null || catalog.Species is null || catalog.Moves is null || frame is null || action is null)
            return "source_kernel_missing_input";
        static bool PokemonShape(PokemonInstance? pokemon) => pokemon is not null && pokemon.Stats is not null &&
            pokemon.Ivs is not null && pokemon.Evs is not null && !pokemon.Moves.IsDefault && pokemon.Moves.All(m => m is not null);
        if (!frame.Parties.IsDefault && frame.Parties.Any(p => p is not null && !p.Members.IsDefault && p.Members.Any(m => m is not null && !PokemonShape(m.Pokemon))) ||
            !frame.Slots.IsDefault && frame.Slots.Any(c => c?.Body is { } body && !PokemonShape(body.Pokemon)))
            return "source_kernel_nested_pokemon_shape";
        if (SourceBattleIdentityRules.Validate(frame) is { } shape) return shape;
        if (SourceTrainerTowerEncounter.Validate(catalog, frame) is { } towerError) return towerError;
        if (frame.SourceMechanicsRulesVersion == Version && SourceMechanicsCatalog.For(catalog) is null)
            return "source_kernel_authenticated_catalog_required";
        if (SourcePokemonValidator.ValidateFrameParties(catalog, frame) is not null)
            return "source_kernel_party_pokemon";
        if (frame.SourceMechanicsRulesVersion == Version && SourceItemTransferRules.Validate(catalog, frame) is { } itemError) return itemError;
        if (frame.RulesVersion != WildBattleRules.FuchsiaRulesVersion) return "source_kernel_profile";
        if (action.Revision != frame.Revision || !frame.IsCurrent(action.Actor)) return "source_kernel_stale_action";
        if (!frame.CanAct(action.Actor.Slot) || action.Target.Value >= frame.BattlerCount || frame.Get(action.Target).Body is null || frame.Outcome != WildBattleOutcome.Ongoing ||
            !frame.PendingSwitches.IsEmpty || action.ActionOrderIndex < 0 || action.ActionOrderIndex >= frame.BattlerCount)
            return "source_kernel_action_phase";
        var actor = frame.Get(action.Actor.Slot).Body!;
        bool struggle = action.ChosenSlot == WildBattleRules.StruggleSlot;
        if (action.ChosenSlot < WildBattleRules.StruggleSlot || action.ChosenSlot >= actor.Pokemon.Moves.Length) return "source_kernel_move_slot";
        bool earned = frame.FourTurn is not null && action.ActionOrderIndex < frame.Queue.Length &&
            frame.Queue[action.ActionOrderIndex] is { Kind: SourceActionKind.Move, MoveSlot: WildBattleRules.StruggleSlot } queued &&
            queued.Token?.Owner == action.Actor && queued.OriginalChosenMove == WildBattleRules.StruggleMove;
        if (struggle && !earned && !SourceFourActionTurns.RequiresStruggle(catalog, frame, action.Actor.Slot))
            return "source_kernel_struggle_not_required";
        if (struggle && action.Target.Side == action.Actor.Slot.Side) return "source_kernel_target_mode";
        if (!catalog.Moves.TryGetValue(struggle ? WildBattleRules.StruggleMove : actor.Pokemon.Moves[action.ChosenSlot].Move, out var move) ||
            !MetronomeSourceMoveDefinition.Matches(move)) return "source_kernel_move_definition";
        var recharge = frame.Get(action.Actor.Slot).Registers.Turn?.Lock;
        if (called is not null) move = called;
        else if (recharge is not null)
        {
            if (!SupportsTurnLock(catalog, recharge) || action.ChosenSlot != recharge.PpSlot)
                return "source_kernel_recharge_selection";
            move = catalog.Moves[recharge.Move];
        }
        else if (!struggle && actor.EncoreMoveSlot is { } encore &&
            (action.ChosenSlot != encore || SourceMoveRestrictionRules.EncoreOverwritten(actor)))
            return "source_kernel_encore_selection_owner_required";
        if (recharge is null && !SupportsRecipe(move)) return "source_kernel_recipe_not_migrated";
        if (called is null && !struggle && !SourceBattleHeldItems.ChoiceAllows(actor, move.Id)) return "source_kernel_choice_locked";
        if (!frame.CanAct(action.Target) && move.Effect != "EFFECT_SPIKES") return "source_kernel_action_phase";
        if (move.Effect == "EFFECT_SPIKES" && frame.Layout == SourceBattleLayout.PresetDoubles && frame.EntryAbilities is null)
            return "source_kernel_spikes_entry_owner_required";
        if (move.Effect == "EFFECT_SECRET_POWER" && frame.Field.Terrain is null) return "source_kernel_secret_power_terrain";
        if (move.Effect == "EFFECT_TEETER_DANCE" && frame.Slots.Take(frame.BattlerCount).Any(c => c.Body is null)) return "source_kernel_teeter_dance_body_required";
        if ((SourceRecipeContext.HasProtectionRecipe(move.Effect) || move.Effect is "EFFECT_SNATCH" or "EFFECT_MAGIC_COAT") &&
            (frame.SourceMechanicsRulesVersion != Version || frame.Queue.Length != frame.BattlerCount))
            return "source_kernel_protection_order_required";
        if (move.Effect == "EFFECT_HELPING_HAND" && frame.Layout == SourceBattleLayout.PresetDoubles &&
            frame.Get(action.Actor.Slot.Partner).Body is null) return "source_kernel_helping_hand_empty_partner_not_migrated";
        if (frame.Layout == SourceBattleLayout.Singles && frame.Slots.Any(c => c.Registers.HelpingHand))
            return "source_kernel_singles_helping_hand_register";
        if (SourceRecipeContext.HasSwitchRecipe(move.Effect) && frame.Layout == SourceBattleLayout.PresetDoubles && frame.FourTurn is null && frame.Queue.IsEmpty)
            return "source_kernel_switch_owner_required";
        if (move.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED" && action.Target != action.Actor.Slot ||
            move.Target == "MOVE_TARGET_SELECTED" && action.Target == action.Actor.Slot && !(move.Effect == "EFFECT_CURSE" && !WildBattleEffective.HasType(catalog, actor, "TYPE_GHOST")) ||
            move.Target == "MOVE_TARGET_OPPONENTS_FIELD" && action.Target.Side == action.Actor.Slot.Side)
            return "source_kernel_target_mode";
        if (move.Target == "MOVE_TARGET_RANDOM" && action.Target.Side == action.Actor.Slot.Side) return "source_kernel_target_mode";
        if (action.LightningRodRedirected && WildBattleEffective.Ability(frame.Get(action.Target).Body!) != "ABILITY_LIGHTNING_ROD")
            return "source_kernel_redirection_metadata";
        if (frame.Layout == SourceBattleLayout.PresetDoubles && move.Target == "MOVE_TARGET_BOTH" && action.Target.Side == action.Actor.Slot.Side)
            return "source_kernel_target_mode";
        if (frame.Layout == SourceBattleLayout.PresetDoubles && move.Target == "MOVE_TARGET_FOES_AND_ALLY" && move.Effect != "EFFECT_TEETER_DANCE")
        {
            if (SourceMoveTargeting.NumericScriptTargetsAfterDeferredKo(frame, action.Actor.Slot).Any(s => frame.Get(s).Body?.Pokemon.CurrentHp is not > 0))
                return "source_kernel_spread_zero_hp_target_not_migrated";
        }
        if (new[] { frame.Field.RainTurnsRemaining, frame.Field.SunTurnsRemaining, frame.Field.SandstormTurnsRemaining, frame.Field.HailTurnsRemaining }.Count(t => t > 0) > 1)
            return "source_kernel_weather_conflict";
        // These source branches still belong to the compatibility executor. Reject the whole
        // transaction before RNG, costs, cancellation or any recipe reaches them.
        foreach (var cell in frame.Slots.Take(frame.BattlerCount))
        {
            if (cell.Body is not { } body) continue;
            if (SourceMoveRestrictionRules.Validate(catalog, body) is { } restrictionError) return restrictionError;
            if (SourceCopyStateRules.Validate(catalog, body, frame, cell.Slot.Side) is { } copyError) return copyError;
            if (cell.Registers.History?.LastHitByType is { } hitType && hitType != "TYPE_MYSTERY" && !WildBattleEffective.IsSourceType(hitType) ||
                body.LastLandedMove is { } landed && landed.Value is not ("pret:MOVE_NONE" or "pret:MOVE_UNAVAILABLE") && !catalog.Moves.ContainsKey(landed))
                return "source_kernel_type_change_history";
            if (body.Status != 0 && !WildBattleStatus.IsSleeping(body.Status) && body.Status is not (WildBattleStatus.Poison or WildBattleStatus.Burn or WildBattleStatus.Freeze or WildBattleStatus.Paralysis) &&
                !WildBattleSourceFieldEffects.IsValidToxic(body.Status)) return "source_kernel_status";
            if (new[] { body.AttackStage, body.DefenseStage, body.SpeedStage, body.AccuracyStage, body.EvasionStage,
                6 + body.SpecialAttackStageDelta, 6 + body.SpecialDefenseStageDelta }.Any(stage => stage is < 0 or > 12))
                return "source_kernel_stat_stage";
            if (!SourceSubstituteRules.Supported(SourcePreparedMoveRules.WithoutPrepared(body)) || !SourceFourEntryAbilities.OverlaySupported(catalog, body) || body.ChargeTurnsRemaining is < 0 or > 2 || body.SourcePartyStatusOverride is not null &&
                !(frame.SourceMechanicsRulesVersion == Version && frame.IsSourceAbsent(cell.Slot) && body.SourcePartyStatusOverride == 0) ||
                cell.Registers.Turn?.Lock is { Kind: not (WildBattleCalledLockKind.Recharge or WildBattleCalledLockKind.Rollout or WildBattleCalledLockKind.Charge or WildBattleCalledLockKind.Rampage or WildBattleCalledLockKind.Bide or WildBattleCalledLockKind.Uproar) } ||
                body.RolloutTurnsRemaining != 0 || body.RampageTurnsRemaining != 0 || body.DigMoveSlot is not null ||
                (body.HeldItem != "ITEM_NONE" && !(frame.SourceMechanicsRulesVersion == Version && SourceFrameBerryItems.SupportsHeldItem(body.HeldItem, catalog))))
                return "source_kernel_volatile_not_migrated";
            if ((body.Rage || body.FuryCutterCounter > 0) && body.Pokemon.CurrentHp == 0 || body.FuryCutterCounter is < 0 or > 5 || body.FirstTurnCounter is < 0 or > 2 || body.ConfusionTurnsRemaining is < 0 or > 5 || body.TruantLoafing && body.Pokemon.CurrentHp == 0 ||
                cell.Registers.Turn is { SureHitTurnsRemaining: > 0 } && body.Pokemon.CurrentHp == 0 ||
                cell.Registers.Turn?.Lock is { } retainedLock && (!SupportsTurnLock(catalog, retainedLock) || body.Pokemon.CurrentHp == 0))
                return "source_kernel_canceller_state";
            if (SourceSleepResidualRules.Validate(frame, cell) is { } sleepResidualError) return sleepResidualError;
            if (SourceResidualStatusRules.Validate(frame, cell) is { } residualError) return residualError;
            if (SourceVolatileTargetRules.Validate(catalog, frame, cell) is { } volatileError) return volatileError;
            if (SourceProtectionRules.Validate(body) is { } protectionError) return protectionError;
            if (frame.SourceMechanicsRulesVersion != Version && (body.Protected || body.Enduring))
                return "source_kernel_volatile_not_migrated";
            if (SourceTurnMoveRules.Validate(body, catalog) is { } turnMoveError) return turnMoveError;
            string ability = WildBattleEffective.Ability(body);
            if (frame.SourceMechanicsRulesVersion == Version &&
                (frame.EntryAbilities is null && SourceFourEntryAbilities.HasEntryAbility(WildBattleTransformationRules.PersistentPokemon(body).AbilityId) ||
                 frame.EntryAbilities is { } entry && entry.AppliedEntries[cell.Slot.Value] != cell.Occupant))
                return "source_kernel_ability_not_migrated";
            if (!FuchsiaBattleScope.SupportsAbility(ability) && ability is not ("ABILITY_NONE" or "ABILITY_WONDER_GUARD" or "ABILITY_SERENE_GRACE" or "ABILITY_HUGE_POWER" or "ABILITY_PURE_POWER" or "ABILITY_HUSTLE" or "ABILITY_MARVEL_SCALE" or "ABILITY_PRESSURE" or "ABILITY_VOLT_ABSORB" or "ABILITY_ROUGH_SKIN" or "ABILITY_FLAME_BODY" or "ABILITY_CLOUD_NINE" or "ABILITY_AIR_LOCK" or "ABILITY_LIGHTNING_ROD" or "ABILITY_PLUS" or "ABILITY_MINUS" or "ABILITY_SPEED_BOOST" or "ABILITY_RAIN_DISH" or "ABILITY_TRUANT" or "ABILITY_DRIZZLE" or "ABILITY_DROUGHT" or "ABILITY_SAND_STREAM" or "ABILITY_TRACE" or "ABILITY_FORECAST" or "ABILITY_HYPER_CUTTER" or "ABILITY_WHITE_SMOKE" or "ABILITY_MAGMA_ARMOR" or "ABILITY_BATTLE_ARMOR" or "ABILITY_SUCTION_CUPS" or "ABILITY_SHADOW_TAG" or "ABILITY_ARENA_TRAP" or "ABILITY_MAGNET_PULL" or "ABILITY_NATURAL_CURE" or "ABILITY_OWN_TEMPO" or "ABILITY_OBLIVIOUS" or "ABILITY_COLOR_CHANGE"))
                return "source_kernel_ability_not_migrated";
        }
        if (frame.FourTurn is null && frame.Queue.IsEmpty && (actor.HeldItem == "ITEM_KINGS_ROCK" ||
            move.Effect == "EFFECT_SECRET_POWER" && frame.Field.Terrain == WildBattleTerrain.Cave ||
            move.Effect is "EFFECT_FAKE_OUT" or "EFFECT_TWISTER" or "EFFECT_SNORE" or "EFFECT_FLINCH_HIT" or "EFFECT_FLINCH_MINIMIZE_HIT" or "EFFECT_SKY_ATTACK"))
            return "source_kernel_flinch_order_not_migrated";
        return null;
    }

    internal static string? SelectedTargetOverride(PokemonCatalog catalog, SourceBattleFrame frame, BattlerSlot actor, PokemonMoveDefinition move) =>
        move.Effect == "EFFECT_CURSE" && !WildBattleEffective.HasType(catalog, frame.Get(actor).Body!, "TYPE_GHOST") ? "MOVE_TARGET_USER" : null;

    internal static PokemonMoveDefinition ActionMove(PokemonCatalog catalog, SourceBattleSlot actor, int chosenSlot) =>
        catalog.Moves[actor.Registers.Turn?.Lock is { } recharge
            ? recharge.Move : chosenSlot == WildBattleRules.StruggleSlot ? WildBattleRules.StruggleMove : actor.Body!.Pokemon.Moves[chosenSlot].Move];

    internal static bool SupportsTurnLock(PokemonCatalog catalog, SourceTurnLock locked) => SupportsRecharge(catalog, locked) ||
        locked.Kind == WildBattleCalledLockKind.Bide && locked.TurnsRemaining is >= 1 and <= 2 && !locked.UnableToUseMove &&
        (locked.StoredDamage == 0) == (locked.StoredDamageSource is null) &&
        catalog.Moves.TryGetValue(locked.Move, out var bideMove) && bideMove.Effect == "EFFECT_BIDE" && MetronomeSourceMoveDefinition.Matches(bideMove) ||
        locked.Kind == WildBattleCalledLockKind.Uproar && locked.TurnsRemaining is >= 1 and <= 5 &&
        locked.StoredDamage == 0 && locked.StoredDamageSource is null &&
        catalog.Moves.TryGetValue(locked.Move, out var uproarMove) && uproarMove.Effect == "EFFECT_UPROAR" && MetronomeSourceMoveDefinition.Matches(uproarMove) ||
        locked.Kind == WildBattleCalledLockKind.Rampage && locked.TurnsRemaining is >= 1 and <= 3 &&
        locked.StoredDamage == 0 && locked.StoredDamageSource is null &&
        catalog.Moves.TryGetValue(locked.Move, out var rampageMove) && rampageMove.Effect == "EFFECT_RAMPAGE" && MetronomeSourceMoveDefinition.Matches(rampageMove) ||
        locked.Kind == WildBattleCalledLockKind.Charge && locked.TurnsRemaining == 1 &&
        locked.StoredDamage == 0 && locked.StoredDamageSource is null && !locked.UnableToUseMove &&
        catalog.Moves.TryGetValue(locked.Move,out var chargeMove) && SourceRecipeContext.HasChargeRecipe(chargeMove.Effect) && MetronomeSourceMoveDefinition.Matches(chargeMove) ||
        locked.Kind == WildBattleCalledLockKind.Rollout && locked.TurnsRemaining is >= 1 and <= 4 &&
        locked.StoredDamage == 0 && locked.StoredDamageSource is null && !locked.UnableToUseMove &&
        catalog.Moves.TryGetValue(locked.Move, out var move) && move.Effect == "EFFECT_ROLLOUT" && MetronomeSourceMoveDefinition.Matches(move);

    internal static bool SupportsRecharge(PokemonCatalog catalog, SourceTurnLock recharge) =>
        recharge.Kind == WildBattleCalledLockKind.Recharge && recharge.TurnsRemaining is 1 or 2 &&
        recharge.StoredDamage == 0 && recharge.StoredDamageSource is null && !recharge.UnableToUseMove &&
        catalog.Moves.TryGetValue(recharge.Move, out var sourceMove) && sourceMove.Effect == "EFFECT_RECHARGE" &&
        MetronomeSourceMoveDefinition.Matches(sourceMove);

    public static bool SupportsRecipe(PokemonMoveDefinition move) => MetronomeSourceMoveDefinition.Matches(move) &&
        move.Target is "MOVE_TARGET_SELECTED" or "MOVE_TARGET_RANDOM" or "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED" or "MOVE_TARGET_BOTH" or "MOVE_TARGET_FOES_AND_ALLY" or "MOVE_TARGET_OPPONENTS_FIELD" or "MOVE_TARGET_DEPENDS" &&
        (SourceRecipeContext.HasFieldUtilityRecipe(move.Effect) || SourceRecipeContext.HasMiscTargetRecipe(move.Effect) || SourceRecipeContext.HasPursuitBeatUpRecipe(move.Effect) || SourceRecipeContext.HasStockpileChargeRecipe(move.Effect) || SourceRecipeContext.HasDelayedRecipe(move.Effect) || move.Effect == "EFFECT_HEAL_BELL" || SourceRecipeContext.HasPreparedMoveRecipe(move.Effect) || SourceRecipeContext.HasConditionalHitRecipe(move.Effect) || SourceRecipeContext.HasAbilityExchangeRecipe(move.Effect) || SourceRecipeContext.HasLateTurnRecipe(move.Effect) || SourceRecipeContext.HasCallerRecipe(move.Effect) || SourceRecipeContext.HasWeatherHitRecipe(move.Effect) || SourceRecipeContext.HasUtilityHpRecipe(move.Effect) || SourceRecipeContext.HasMoveRestrictionRecipe(move.Effect) || SourceRecipeContext.HasGroundSpreadRecipe(move.Effect) || SourceRecipeContext.HasSleepAttackRecipe(move.Effect) || SourceRecipeContext.HasCopyStateRecipe(move.Effect) || SourceRecipeContext.HasRechargeRampageRecipe(move.Effect) || SourceRecipeContext.HasTargetMarkRecipe(move.Effect) || move.Effect == "EFFECT_SUBSTITUTE" || SourceRecipeContext.HasSleepResidualRecipe(move.Effect) || SourceRecipeContext.HasChargeRecipe(move.Effect) || SourceRecipeContext.HasResidualStatusRecipe(move.Effect) || SourceRecipeContext.HasBasicStatRecipe(move.Effect) || SourceRecipeContext.HasAdditionalPrimaryStatusRecipe(move.Effect) || move.Effect == "EFFECT_SPIKES" || move.Id == WildBattleRules.StruggleMove || SourceRecipeContext.HasGenericRecipe(move.Effect) ||
         SourceRecipeContext.HasTypeChangeRecipe(move.Effect) || SourceRecipeContext.HasRetaliationRecipe(move.Effect) || SourceRecipeContext.HasFixedDamageRecipe(move.Effect) ||
        SourceRecipeContext.HasHitSequenceRecipe(move.Effect) || SourceRecipeContext.HasItemTransferRecipe(move.Effect) || SourceRecipeContext.HasRecoilRecipe(move.Effect) || SourceRecipeContext.HasDynamicTypeRecipe(move.Effect) || SourceRecipeContext.HasDynamicPowerRecipe(move.Effect) ||
         SourceRecipeContext.HasStatRecoveryRecipe(move.Effect) || SourceRecipeContext.HasFieldSettingRecipe(move.Effect) ||
         SourceRecipeContext.HasProtectionRecipe(move.Effect) || SourceRecipeContext.HasAllySupportRecipe(move.Effect) || SourceRecipeContext.HasTurnMoveRecipe(move.Effect) || SourceRecipeContext.HasSwitchRecipe(move.Effect));

    public static SourceKernelResult ResolveAction(PokemonCatalog catalog, SourceBattleFrame frame, SourceResolvedMoveAction action)
    {
        if (ValidateAction(catalog, frame, action) is { } error) return new(frame, [], [], null, error);
        // Preflight validates unsorted selections; execution must use the real retained owner.
        // Otherwise a standalone caller could invent a favorable non-last action index.
        if ((SourceRecipeContext.HasProtectionRecipe(ActionMove(catalog, frame.Get(action.Actor.Slot), action.ChosenSlot).Effect) ||
             ActionMove(catalog, frame.Get(action.Actor.Slot), action.ChosenSlot).Effect is "EFFECT_SNATCH" or "EFFECT_MAGIC_COAT" ||
             frame.Get(action.Actor.Slot).Body!.HeldItem == "ITEM_KINGS_ROCK") &&
            (frame.FourTurn?.Phase != SourceFourActionPhase.Actions || frame.NextAction != action.ActionOrderIndex ||
             frame.Queue[action.ActionOrderIndex].Token?.Owner != action.Actor ||
             frame.Queue[action.ActionOrderIndex].Kind != SourceActionKind.Move))
            return new(frame, [], [], null, frame.Get(action.Actor.Slot).Body!.HeldItem == "ITEM_KINGS_ROCK"
                ? "source_kernel_flinch_order_not_migrated" : "source_kernel_protection_order_required");
        try
        {
            var execution = new FrameRecipeContext(catalog, frame, action);
            execution.Run();
            if (SourceBattleIdentityRules.Validate(execution.Frame) is { } invalid)
                return new(frame, [], [], null, "source_kernel_result_" + invalid);
            return new(execution.Frame, execution.Events.ToImmutable(), execution.Trace.ToImmutable(), execution.Invocation, null);
        }
        catch (SourceRecipeUnsupported ex) { return new(frame, [], [], null, ex.Message); }
        catch (OverflowException) { return new(frame, [], [], null, "source_kernel_counter_exhausted"); }
        catch (ArgumentException) { return new(frame, [], [], null, "source_kernel_invalid_calculation"); }
    }
}
