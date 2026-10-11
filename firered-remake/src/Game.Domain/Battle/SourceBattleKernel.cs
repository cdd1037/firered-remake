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
    public static string? ValidateAction(PokemonCatalog catalog, SourceBattleFrame frame, SourceResolvedMoveAction action)
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
        if (!frame.Slots.IsDefault && frame.Slots.Any(c => c?.Body is { } body && (body.Transform is not null || body.CopyMoves is not null)))
            return "source_kernel_volatile_not_migrated";
        if (SourceBattleIdentityRules.Validate(frame) is { } shape) return shape;
        if (frame.Layout == SourceBattleLayout.PresetDoubles && SourceMechanicsCatalog.For(catalog) is null)
            return "source_kernel_authenticated_catalog_required";
        foreach (var party in frame.Parties)
            foreach (var member in party.Members)
                if (SourcePokemonValidator.ValidateForBattle(catalog, member.Pokemon) is not null)
                    return "source_kernel_party_pokemon";
        if (frame.RulesVersion != WildBattleRules.FuchsiaRulesVersion) return "source_kernel_profile";
        if (action.Revision != frame.Revision || !frame.IsCurrent(action.Actor)) return "source_kernel_stale_action";
        if (!frame.CanAct(action.Actor.Slot) || !frame.CanAct(action.Target) || frame.Outcome != WildBattleOutcome.Ongoing ||
            !frame.PendingSwitches.IsEmpty || action.ActionOrderIndex < 0 || action.ActionOrderIndex >= frame.BattlerCount)
            return "source_kernel_action_phase";
        var actor = frame.Get(action.Actor.Slot).Body!;
        if (action.ChosenSlot < 0 || action.ChosenSlot >= actor.Pokemon.Moves.Length) return "source_kernel_move_slot";
        if (!catalog.Moves.TryGetValue(actor.Pokemon.Moves[action.ChosenSlot].Move, out var move) ||
            !MetronomeSourceMoveDefinition.Matches(move)) return "source_kernel_move_definition";
        var recharge = frame.Get(action.Actor.Slot).Registers.Turn?.Lock;
        if (recharge is not null)
        {
            if (!SupportsTurnLock(catalog, recharge) || action.ChosenSlot != recharge.PpSlot)
                return "source_kernel_recharge_selection";
            move = catalog.Moves[recharge.Move];
        }
        else if (!SupportsRecipe(move)) return "source_kernel_recipe_not_migrated";
        if (move.Effect == "EFFECT_HELPING_HAND" && frame.Layout == SourceBattleLayout.PresetDoubles &&
            frame.Get(action.Actor.Slot.Partner).Body is null) return "source_kernel_helping_hand_empty_partner_not_migrated";
        if (frame.Layout == SourceBattleLayout.Singles && frame.Slots.Any(c => c.Registers.HelpingHand))
            return "source_kernel_singles_helping_hand_register";
        if (SourceRecipeContext.HasSwitchRecipe(move.Effect) && frame.Layout == SourceBattleLayout.PresetDoubles && frame.FourTurn is null && frame.Queue.IsEmpty)
            return "source_kernel_switch_owner_required";
        if (move.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED" && action.Target != action.Actor.Slot ||
            move.Target == "MOVE_TARGET_SELECTED" && action.Target == action.Actor.Slot)
            return "source_kernel_target_mode";
        if (action.LightningRodRedirected && WildBattleEffective.Ability(frame.Get(action.Target).Body!) != "ABILITY_LIGHTNING_ROD")
            return "source_kernel_redirection_metadata";
        if (frame.Layout == SourceBattleLayout.PresetDoubles && move.Target == "MOVE_TARGET_BOTH" && action.Target.Side == action.Actor.Slot.Side)
            return "source_kernel_target_mode";
        if (frame.Layout == SourceBattleLayout.PresetDoubles && move.Target == "MOVE_TARGET_FOES_AND_ALLY")
        {
            if (frame.DeferredKoVacancyMask != 0) return "source_kernel_spread_deferred_target_not_migrated";
            if (SourceMoveTargeting.NumericScriptTargets(frame, action.Actor.Slot).Any(s => frame.Get(s).Body?.Pokemon.CurrentHp is not > 0))
                return "source_kernel_spread_zero_hp_target_not_migrated";
        }
        if (new[] { frame.Field.RainTurnsRemaining, frame.Field.SunTurnsRemaining, frame.Field.SandstormTurnsRemaining, frame.Field.HailTurnsRemaining }.Count(t => t > 0) > 1)
            return "source_kernel_weather_conflict";
        // These source branches still belong to the compatibility executor. Reject the whole
        // transaction before RNG, costs, cancellation or any recipe reaches them.
        foreach (var cell in frame.Slots.Take(frame.BattlerCount))
        {
            if (cell.Body is not { } body) continue;
            if (SourcePokemonValidator.ValidateForBattle(catalog, WildBattleTransformationRules.PersistentPokemon(body)) is { })
                return "source_kernel_pokemon";
            if (body.Status != 0 && !WildBattleStatus.IsSleeping(body.Status) && body.Status is not (WildBattleStatus.Poison or WildBattleStatus.Burn or WildBattleStatus.Freeze or WildBattleStatus.Paralysis) &&
                !WildBattleSourceFieldEffects.IsValidToxic(body.Status)) return "source_kernel_status";
            if (new[] { body.AttackStage, body.DefenseStage, body.SpeedStage, body.AccuracyStage, body.EvasionStage,
                6 + body.SpecialAttackStageDelta, 6 + body.SpecialDefenseStageDelta }.Any(stage => stage is < 0 or > 12))
                return "source_kernel_stat_stage";
            if (cell.Position.FocusBanded || body.Transform is not null || body.CopyMoves is not null || body.CalledVolatile is not null || !SourceFourEntryAbilities.OverlaySupported(body) || body.DestinyBond || body.Nightmare || body.Cursed || body.Rooted || body.YawnTurnsRemaining != 0 || body.ChargeTurnsRemaining != 0 || body.Imprison || body.SourcePartyStatusOverride is not null ||
                cell.Registers.Turn?.Lock is { Kind: not (WildBattleCalledLockKind.Recharge or WildBattleCalledLockKind.Rollout) } || body.Protected || body.Enduring || body.Rage ||
                body.BindTurnsRemaining != 0 ||
                body.RolloutTurnsRemaining != 0 || body.RampageTurnsRemaining != 0 || body.DigMoveSlot is not null ||
                body.EncoreMoveSlot is not null ||
                cell.Registers.LeechSeedSource is not null || cell.Registers.MeanLookSource is not null || cell.Registers.WrappedBy is not null || (body.HeldItem != "ITEM_NONE" && !(frame.Layout == SourceBattleLayout.PresetDoubles && SourceFourRewards.SupportsRewardHeldItem(body.HeldItem))) || body.WaterSport || body.MudSport)
                return "source_kernel_volatile_not_migrated";
            if (body.ConfusionTurnsRemaining is < 0 or > 5 || body.TruantLoafing && body.Pokemon.CurrentHp == 0 ||
                cell.Registers.Turn?.Lock is { } retainedLock && (!SupportsTurnLock(catalog, retainedLock) || body.Pokemon.CurrentHp == 0))
                return "source_kernel_canceller_state";
            if (SourceTurnMoveRules.Validate(body, catalog) is { } turnMoveError) return turnMoveError;
            string ability = WildBattleEffective.Ability(body);
            if (frame.Layout == SourceBattleLayout.PresetDoubles &&
                (frame.EntryAbilities is null && SourceFourEntryAbilities.HasEntryAbility(body.Pokemon.AbilityId) ||
                 frame.EntryAbilities is { } entry && entry.AppliedEntries[cell.Slot.Value] != cell.Occupant))
                return "source_kernel_ability_not_migrated";
            if (!FuchsiaBattleScope.SupportsAbility(ability) && ability is not ("ABILITY_HUGE_POWER" or "ABILITY_PURE_POWER" or "ABILITY_HUSTLE" or "ABILITY_MARVEL_SCALE" or "ABILITY_PRESSURE" or "ABILITY_VOLT_ABSORB" or "ABILITY_CLOUD_NINE" or "ABILITY_AIR_LOCK" or "ABILITY_LIGHTNING_ROD" or "ABILITY_PLUS" or "ABILITY_MINUS" or "ABILITY_SPEED_BOOST" or "ABILITY_RAIN_DISH" or "ABILITY_TRUANT" or "ABILITY_DRIZZLE" or "ABILITY_DROUGHT" or "ABILITY_SAND_STREAM" or "ABILITY_TRACE" or "ABILITY_FORECAST" or "ABILITY_HYPER_CUTTER" or "ABILITY_WHITE_SMOKE" or "ABILITY_MAGMA_ARMOR" or "ABILITY_SUCTION_CUPS" or "ABILITY_SHADOW_TAG" or "ABILITY_ARENA_TRAP" or "ABILITY_MAGNET_PULL" or "ABILITY_NATURAL_CURE" or "ABILITY_OWN_TEMPO" or "ABILITY_OBLIVIOUS"))
                return "source_kernel_ability_not_migrated";
            if (ability is "ABILITY_COLOR_CHANGE" or
                "ABILITY_HUSTLE" or "ABILITY_POISON_POINT" or "ABILITY_ROUGH_SKIN" or "ABILITY_FLAME_BODY" or "ABILITY_EFFECT_SPORE")
                return "source_kernel_ability_not_migrated";
        }
        if (move.Power > 0 && move.Type == "TYPE_FIRE" && frame.Slots.Any(c => c.Body?.Status == WildBattleStatus.Freeze))
            return "source_kernel_fire_thaw_move_end_not_migrated";
        if (frame.FourTurn is null && frame.Queue.IsEmpty && move.Effect is "EFFECT_FLINCH_HIT" or "EFFECT_FLINCH_MINIMIZE_HIT")
            return "source_kernel_flinch_order_not_migrated";
        return null;
    }

    internal static PokemonMoveDefinition ActionMove(PokemonCatalog catalog, SourceBattleSlot actor, int chosenSlot) =>
        catalog.Moves[actor.Registers.Turn?.Lock is { } recharge
            ? recharge.Move : actor.Body!.Pokemon.Moves[chosenSlot].Move];

    internal static bool SupportsTurnLock(PokemonCatalog catalog, SourceTurnLock locked) => SupportsRecharge(catalog, locked) ||
        locked.Kind == WildBattleCalledLockKind.Rollout && locked.TurnsRemaining is >= 1 and <= 4 &&
        locked.StoredDamage == 0 && locked.StoredDamageSource is null && !locked.UnableToUseMove &&
        catalog.Moves.TryGetValue(locked.Move, out var move) && move.Effect == "EFFECT_ROLLOUT" && MetronomeSourceMoveDefinition.Matches(move);

    internal static bool SupportsRecharge(PokemonCatalog catalog, SourceTurnLock recharge) =>
        recharge.Kind == WildBattleCalledLockKind.Recharge && recharge.TurnsRemaining is 1 or 2 &&
        recharge.StoredDamage == 0 && recharge.StoredDamageSource is null && !recharge.UnableToUseMove &&
        catalog.Moves.TryGetValue(recharge.Move, out var sourceMove) && sourceMove.Effect == "EFFECT_RECHARGE" &&
        MetronomeSourceMoveDefinition.Matches(sourceMove);

    public static bool SupportsRecipe(PokemonMoveDefinition move) => MetronomeSourceMoveDefinition.Matches(move) &&
        move.Target is "MOVE_TARGET_SELECTED" or "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED" or "MOVE_TARGET_BOTH" or "MOVE_TARGET_FOES_AND_ALLY" &&
        (SourceRecipeContext.HasGenericRecipe(move.Effect) && move.Effect is not
            ("EFFECT_CONFUSE" or "EFFECT_MEAN_LOOK" or "EFFECT_TRAP") ||
         SourceRecipeContext.HasStatRecoveryRecipe(move.Effect) || SourceRecipeContext.HasFieldSettingRecipe(move.Effect) ||
         SourceRecipeContext.HasAllySupportRecipe(move.Effect) || SourceRecipeContext.HasTurnMoveRecipe(move.Effect) || SourceRecipeContext.HasSwitchRecipe(move.Effect));

    public static SourceKernelResult ResolveAction(PokemonCatalog catalog, SourceBattleFrame frame, SourceResolvedMoveAction action)
    {
        if (ValidateAction(catalog, frame, action) is { } error) return new(frame, [], [], null, error);
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
