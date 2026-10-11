using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Battle;
using Game.Domain.Rules;

namespace Game.Domain.WildBattle;

/// <summary>Battle-only type/ability registers. PokemonInstance remains the authenticated identity.</summary>
public sealed record WildBattleCalledOverlay
{
    public string RulesVersion { get; init; } = WildBattleRules.CalledMoveKernelRulesVersion;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Type1 { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Type2 { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Ability { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public SourceCastformForm? CastformForm { get; init; }
}
public enum WildBattleCalledBattleKind { Wild, Trainer }
public enum WildBattleCalledSwitchKind { Voluntary, BatonPass, FaintedReplacement, Forced }
/// <summary>Index identity is stable only within this battle. App ownership identity belongs to its adapter.</summary>
public sealed record WildBattleCalledPartyMember(PokemonInstance Pokemon, string HeldItem = "ITEM_NONE", uint Status = 0,
    bool IsEgg = false, string? KnockedOffItem = null)
{
    // Knock Off suppresses only the battle item. Recycle/consumption can subsequently change party data.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? PersistentHeldItemOverride { get; init; }
}
public sealed record WildBattleCalledPendingSwitch(WildBattleSide Side, WildBattleCalledSwitchKind Kind,
    ImmutableArray<int> LegalPartyIndices, WildBattleMoveInvocation? Invocation = null);
/// <summary>Actual supplied parties, never synthesized from an active battler or a catalog entry.</summary>
public sealed record WildBattleCalledPartyState(ImmutableArray<WildBattleCalledPartyMember> Player,
    ImmutableArray<WildBattleCalledPartyMember> Opponent, int PlayerActiveIndex, int OpponentActiveIndex,
    WildBattleCalledBattleKind BattleKind = WildBattleCalledBattleKind.Wild)
{
    public string RulesVersion { get; init; } = WildBattleRules.CalledMoveKernelRulesVersion;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int PlayerSpikes { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int OpponentSpikes { get; init; }
    // gBattleStruct->usedHeldItems belongs to battler slots, surviving a switch.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? PlayerUsedHeldItem { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? OpponentUsedHeldItem { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleCalledPendingSwitch? PendingSwitch { get; init; }
}
public sealed record WildBattleCalledPartySwitchAction(WildBattleSide Side, int PartyIndex,
    string RulesVersion = WildBattleRules.CalledMoveKernelRulesVersion);
public sealed record WildBattleCalledOverlayChangedEvent(long Sequence, int Turn, WildBattleSide Side,
    MoveId Move, WildBattleCalledOverlay? Before, WildBattleCalledOverlay? After) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleCalledPartyChangedEvent(long Sequence, int Turn, WildBattleSide Side, int PartyIndex,
    string Reason, WildBattleCalledPartyMember Before, WildBattleCalledPartyMember After) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleCalledPartySwitchRequiredEvent(long Sequence, int Turn,
    WildBattleCalledPendingSwitch Pending) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleCalledPartySwitchedEvent(long Sequence, int Turn, WildBattleSide Side,
    int OutgoingIndex, int IncomingIndex, WildBattleCalledSwitchKind Kind) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleCalledSpikesChangedEvent(long Sequence, int Turn, WildBattleSide Side,
    int Before, int After) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleCalledBeatUpMemberEvent(long Sequence, int Turn, WildBattleSide Side,
    int PartyIndex, SpeciesId Species) : WildBattleEvent(Sequence, Turn);

/// <summary>Shared battle mechanics must read these registers rather than permanent species/ability.</summary>
public static class WildBattleEffective
{
    public static string Ability(WildBattleCombatant mon) => mon.CalledOverlay?.Ability ?? mon.Pokemon.AbilityId;
    public static (string Type1, string Type2) Types(PokemonCatalog catalog, WildBattleCombatant mon)
    {
        var species = catalog.Species[mon.Pokemon.Species];
        return (mon.CalledOverlay?.Type1 ?? species.Type1, mon.CalledOverlay?.Type2 ?? species.Type2);
    }
    public static bool HasType(PokemonCatalog catalog, WildBattleCombatant mon, string type)
    { var pair = Types(catalog, mon); return pair.Type1 == type || pair.Type2 == type; }
    public static bool IsSourceType(string? type) => type is "TYPE_NORMAL" or "TYPE_FIGHTING" or "TYPE_FLYING" or
        "TYPE_POISON" or "TYPE_GROUND" or "TYPE_ROCK" or "TYPE_BUG" or "TYPE_GHOST" or "TYPE_STEEL" or
        "TYPE_FIRE" or "TYPE_WATER" or "TYPE_GRASS" or "TYPE_ELECTRIC" or "TYPE_PSYCHIC" or "TYPE_ICE" or "TYPE_DRAGON" or "TYPE_DARK";
}
public static class WildBattleSourcePartyEffects
{
    public static ImmutableArray<WildBattleCalledPartyMember> Party(WildBattleCalledPartyState state, WildBattleSide side) =>
        side == WildBattleSide.Player ? state.Player : state.Opponent;
    public static int ActiveIndex(WildBattleCalledPartyState state, WildBattleSide side) =>
        side == WildBattleSide.Player ? state.PlayerActiveIndex : state.OpponentActiveIndex;
    public static ImmutableArray<int> LegalReserveIndices(WildBattleCalledPartyState state, WildBattleSide side) =>
        Party(state, side).Select((mon, index) => (mon, index)).Where(x => x.index != ActiveIndex(state, side) &&
            !x.mon.IsEgg && x.mon.Pokemon.CurrentHp > 0).Select(x => x.index).ToImmutableArray();
    public static bool HasLivingMember(WildBattleCalledPartyState state, WildBattleSide side) =>
        Party(state, side).Any(mon => !mon.IsEgg && mon.Pokemon.CurrentHp > 0);
    public static ImmutableArray<int> BeatUpIndices(WildBattleCalledPartyState state, WildBattleSide side) =>
        Party(state, side).Select((mon, index) => (mon, index)).Where(x => !x.mon.IsEgg && x.mon.Pokemon.CurrentHp > 0 &&
            x.mon.Status == 0).Select(x => x.index).ToImmutableArray();
    public static string PersistentHeldItem(WildBattleCalledPartyMember mon) => mon.PersistentHeldItemOverride ?? mon.KnockedOffItem ?? mon.HeldItem;
    internal static WildBattleCalledPartyMember WithPersistentHeldItem(WildBattleCalledPartyMember mon, string item) =>
        mon with { PersistentHeldItemOverride = item == (mon.KnockedOffItem ?? mon.HeldItem) ? null : item };
    internal static bool SamePokemon(PokemonInstance a, PokemonInstance b) =>
        a with { Moves = [] } == b with { Moves = [] } && a.Moves.SequenceEqual(b.Moves);
    // ImmutableArray record equality compares backing-array identity. State restored from JSON
    // has different arrays even when the permanent party data is unchanged.
    internal static bool SameMember(WildBattleCalledPartyMember a, WildBattleCalledPartyMember b) =>
        a with { Pokemon = b.Pokemon } == b && SamePokemon(a.Pokemon, b.Pokemon);
    public static int SpikesDamage(int maxHp, int layers) => layers is >= 1 and <= 3 && maxHp > 0
        ? Math.Max(1, maxHp / ((5 - layers) * 2)) : throw new ArgumentOutOfRangeException(nameof(layers));
    public static int BeatUpBaseDamage(PokemonCatalog catalog, WildBattleCalledPartyMember contributor,
        WildBattleCombatant target, int power) =>
        catalog.Species[contributor.Pokemon.Species].BaseStats.Attack * power * (contributor.Pokemon.Level * 2 / 5 + 2) /
        catalog.Species[target.Pokemon.Species].BaseStats.Defense / 50 + 2;
    /// <summary>Modern Conversion2: distinct resistant/immune types in stable Gen III source-ID order.</summary>
    public static ImmutableArray<string> Conversion2Candidates(string attackType, string type1, string type2)
    {
        if (!attackType.StartsWith("TYPE_", StringComparison.Ordinal) ||
            !Enum.TryParse<ElementType>(attackType[5..], true, out var attack) || !Enum.IsDefined(attack) ||
            !WildBattleEffective.IsSourceType(type1) || !WildBattleEffective.IsSourceType(type2))
            throw new ArgumentOutOfRangeException(nameof(attackType));
        return Enum.GetValues<ElementType>().Where(type => type != ElementType.Mystery && TypeRules.EffectivenessTenths(attack, type) <= 5)
            .Select(type => "TYPE_" + type.ToString().ToUpperInvariant()).Where(type => type != type1 && type != type2).ToImmutableArray();
    }
    /// <summary>One standard engine uint16 draw. Bucket sizes differ by at most one out of65536 values.</summary>
    public static int Conversion2ChoiceIndex(ushort random, int candidateCount) => candidateCount is >= 1 and <= 17
        ? random % candidateCount : throw new ArgumentOutOfRangeException(nameof(candidateCount));
    public static string CamouflageType(WildBattleTerrain terrain) => terrain switch {
        WildBattleTerrain.Grass or WildBattleTerrain.LongGrass => "TYPE_GRASS",
        WildBattleTerrain.Sand => "TYPE_GROUND",
        WildBattleTerrain.Underwater or WildBattleTerrain.Water or WildBattleTerrain.Pond => "TYPE_WATER",
        WildBattleTerrain.Mountain or WildBattleTerrain.Cave => "TYPE_ROCK",
        WildBattleTerrain.Building or WildBattleTerrain.Plain => "TYPE_NORMAL",
        _ => throw new ArgumentOutOfRangeException(nameof(terrain)) };
    internal static WildBattleCalledPartyState SetParty(WildBattleCalledPartyState state, WildBattleSide side,
        ImmutableArray<WildBattleCalledPartyMember> party) => side == WildBattleSide.Player ? state with { Player = party } : state with { Opponent = party };
    internal static WildBattleCalledPartyState Synchronize(WildBattleCalledPartyState state, WildBattleSide side, WildBattleCombatant mon)
    {
        var party = Party(state, side); int index = ActiveIndex(state, side); var before = party[index];
        string persistentItem = before.HeldItem != mon.HeldItem && before.KnockedOffItem == mon.KnockedOffItem ? mon.HeldItem : PersistentHeldItem(before);
        var after = before with { Pokemon = WildBattleTransformationRules.PersistentPokemon(mon),
            Status = WildBattleTransformationRules.PersistentStatus(mon), HeldItem = mon.HeldItem, KnockedOffItem = mon.KnockedOffItem };
        after = WithPersistentHeldItem(after, persistentItem);
        return SameMember(before, after) ? state : SetParty(state, side, party.SetItem(index, after));
    }
}

public static partial class WildBattleRules
{
    private static bool HasCalledPartyRecipe(string effect) => effect is "EFFECT_CONVERSION" or "EFFECT_CONVERSION_2" or
        "EFFECT_SPIKES" or "EFFECT_HEAL_BELL" or "EFFECT_BATON_PASS" or "EFFECT_BEAT_UP" or "EFFECT_MEMENTO" or
        "EFFECT_ROLE_PLAY" or "EFFECT_RECYCLE" or "EFFECT_SKILL_SWAP" or "EFFECT_CAMOUFLAGE" or "EFFECT_TEETER_DANCE" or
        "EFFECT_TRICK" or "EFFECT_THIEF";
    private static void AddCalledPartyEffectSupport(PokemonMoveDefinition move, ref bool supported)
    { if (MetronomeSourceMoveDefinition.Matches(move) && HasCalledPartyRecipe(move.Effect)) supported = true; }
    private static bool CalledPartyRequired(PokemonMoveDefinition move) => move.Effect is "EFFECT_SPIKES" or "EFFECT_HEAL_BELL" or
        "EFFECT_BATON_PASS" or "EFFECT_BEAT_UP" or "EFFECT_RECYCLE" or "EFFECT_TRICK" or "EFFECT_THIEF";
    private static string? ValidateCalledPartyState(PokemonCatalog catalog, WildBattleState state)
    {
        var context = state.CalledParty;
        if (state.Player?.Pokemon is null || state.Opponent?.Pokemon is null) return "called_party_combatant";
        IEnumerable<string> PresentAbilities()
        {
            foreach (var mon in new[] { state.Player, state.Opponent })
            {
                yield return WildBattleTransformationRules.PersistentPokemon(mon).AbilityId;
                if (mon.Transform is { } transform) yield return transform.CopiedPokemon.AbilityId;
            }
            if (context is null) yield break;
            foreach (var member in context.Player.Concat(context.Opponent)) if (member is not null) yield return member.Pokemon.AbilityId;
        }
        if (context is not null)
        {
            if (state.AwaitingReplacement) return "called_party_legacy_replacement_phase";
            if (context.RulesVersion != CalledMoveKernelRulesVersion) return "called_party_version";
            if (!Enum.IsDefined(context.BattleKind)) return "called_party_kind";
            if (context.Player.IsDefault || context.Opponent.IsDefault || context.Player.Length is < 1 or > 6 ||
                context.Opponent.Length is < 1 or > 6) return "called_party_size";
            if (context.BattleKind == WildBattleCalledBattleKind.Wild && context.Opponent.Length != 1) return "called_party_wild_opponent_size";
            if (context.PlayerSpikes is < 0 or > 3 || context.OpponentSpikes is < 0 or > 3) return "called_party_spikes";
            foreach (var item in new[] { context.PlayerUsedHeldItem, context.OpponentUsedHeldItem })
                if (item is not null && (item == "ITEM_NONE" || !FuchsiaBattleScope.SupportsHeldItem(item))) return "called_party_used_item";
            foreach (var side in new[] { WildBattleSide.Player, WildBattleSide.Opponent })
            {
                var party = WildBattleSourcePartyEffects.Party(context, side); int active = WildBattleSourcePartyEffects.ActiveIndex(context, side);
                if (active < 0 || active >= party.Length) return "called_party_active_index";
                foreach (var mon in party)
                {
                    if (mon?.Pokemon is null || SourcePokemonValidator.ValidateForBattle(catalog, mon.Pokemon) is not null) return "called_party_member";
                    if (SourceBattleAdmission.ValidatePokemonCapabilities(catalog, mon.Pokemon) is { } capabilityError) return capabilityError;
                    if (!SourceBattleAdmission.SupportsHeldItem(catalog, mon.HeldItem) || !SourceBattleAdmission.SupportsHeldItem(catalog, WildBattleSourcePartyEffects.PersistentHeldItem(mon))) return "called_party_item";
                    if (mon.KnockedOffItem is { } knocked && (knocked == "ITEM_NONE" || !SourceBattleAdmission.SupportsHeldItem(catalog, knocked))) return "called_party_knocked_off";
                    if (mon.Status != 0 && mon.Status is not (WildBattleStatus.Poison or WildBattleStatus.Burn or WildBattleStatus.Freeze or WildBattleStatus.Paralysis) &&
                        !WildBattleStatus.IsSleeping(mon.Status) && mon.Status != SourcePartyStatusRules.Toxic) return "called_party_status";
                }
                var live = side == WildBattleSide.Player ? state.Player : state.Opponent; var entry = party[active];
                if (entry.IsEgg || !WildBattleSourcePartyEffects.SamePokemon(entry.Pokemon, WildBattleTransformationRules.PersistentPokemon(live)) ||
                    entry.Status != WildBattleTransformationRules.PersistentStatus(live) || entry.HeldItem != live.HeldItem ||
                    entry.KnockedOffItem != live.KnockedOffItem) return "called_party_active_mismatch";
            }
            bool playerSurvives = context.Player.Any(m => !m.IsEgg && m.Pokemon.CurrentHp > 0);
            bool opponentSurvives = context.Opponent.Any(m => !m.IsEgg && m.Pokemon.CurrentHp > 0);
            if (state.Outcome == WildBattleOutcome.Ongoing && (!playerSurvives || !opponentSurvives ||
                (state.Player.Pokemon.CurrentHp == 0 || state.Opponent.Pokemon.CurrentHp == 0) && context.PendingSwitch is null)) return "called_party_outcome";
            if (state.Outcome is WildBattleOutcome.Won or WildBattleOutcome.Lost or WildBattleOutcome.Draw &&
                state.Outcome != (!playerSurvives && !opponentSurvives ? WildBattleOutcome.Draw : !opponentSurvives ? WildBattleOutcome.Won : !playerSurvives ? WildBattleOutcome.Lost : WildBattleOutcome.Ongoing)) return "called_party_outcome";
            foreach (var side in new[] { WildBattleSide.Player, WildBattleSide.Opponent })
            {
                var wish = side == WildBattleSide.Player ? state.CalledDelayed?.PlayerWish : state.CalledDelayed?.OpponentWish;
                if (wish is null) continue;
                var party = WildBattleSourcePartyEffects.Party(context, side);
                if (wish.SourcePartyIndex < 0 || wish.SourcePartyIndex >= party.Length) return "called_party_wish_attribution";
                var source = party[wish.SourcePartyIndex].Pokemon;
                if (wish.SourceSpecies != source.Species || wish.SourceOriginalTrainerId != source.OriginalTrainerId || wish.SourcePersonality != source.Personality) return "called_party_wish_attribution";
            }
            if (context.PendingSwitch is { } pending)
            {
                if (state.Outcome != WildBattleOutcome.Ongoing) return "called_party_pending_terminal";
                if (!Enum.IsDefined(pending.Side) || pending.Kind is not (WildBattleCalledSwitchKind.BatonPass or WildBattleCalledSwitchKind.FaintedReplacement) ||
                    pending.LegalPartyIndices.IsDefaultOrEmpty ||
                    !pending.LegalPartyIndices.SequenceEqual(WildBattleSourcePartyEffects.LegalReserveIndices(context, pending.Side))) return "called_party_pending_switch";
                var active = pending.Side == WildBattleSide.Player ? state.Player : state.Opponent;
                if (pending.Kind == WildBattleCalledSwitchKind.FaintedReplacement && pending.Invocation is not null) return "called_party_pending_invocation";
                if ((pending.Kind == WildBattleCalledSwitchKind.FaintedReplacement) != (active.Pokemon.CurrentHp == 0)) return "called_party_pending_hp";
                if (pending.Kind == WildBattleCalledSwitchKind.BatonPass && (pending.Invocation is not { } invocation || invocation.Actor != pending.Side || invocation.PpOwner != pending.Side ||
                    invocation.CurrentMove.Value != "pret:MOVE_BATON_PASS" || invocation.Target != pending.Side ||
                    invocation.PpSlot < 0 || invocation.PpSlot >= active.Pokemon.Moves.Length || invocation.ChosenSlot < 0 || invocation.ChosenSlot >= active.Pokemon.Moves.Length ||
                    active.Pokemon.Moves[invocation.ChosenSlot].Move != invocation.OriginalChosenMove ||
                    !catalog.Moves.TryGetValue(invocation.OriginalChosenMove, out var chosen) || !MetronomeSourceMoveDefinition.Matches(chosen) ||
                    !invocation.Flags.HasFlag(WildBattleInvocationFlags.Obeys) || invocation.CancellerPhase != WildBattleAttackCancellerPhase.ConditionsPassed)) return "called_party_pending_invocation";
            }
        }
        var allowedAbilities = PresentAbilities().ToHashSet(StringComparer.Ordinal);
        foreach (var mon in new[] { state.Player, state.Opponent })
        {
            if (mon.CalledOverlay is not { } overlay) continue;
            if (overlay.RulesVersion != CalledMoveKernelRulesVersion) return "called_overlay_version";
            if ((overlay.Type1 is null) != (overlay.Type2 is null) || overlay.Type1 is not null &&
                (!WildBattleEffective.IsSourceType(overlay.Type1) || !WildBattleEffective.IsSourceType(overlay.Type2))) return "called_overlay_type";
            if (overlay.Ability is { } ability && !allowedAbilities.Contains(ability)) return "called_overlay_ability_provenance";
        }
        return null;
    }

    private static WildBattleState CalledPartyValidationProjection(WildBattleState state)
    {
        if (state.CalledParty is null) return state;
        // Base v1 has no opponent-party replacement phase; validate its existing combatant invariants
        // against the HP projection after the called contract checked the genuine whole-party phase.
        var outcome = state.Outcome;
        if (state.CalledParty is not null && outcome == WildBattleOutcome.Ongoing)
        {
            if (state.Player.Pokemon.CurrentHp == 0 && state.Opponent.Pokemon.CurrentHp == 0) outcome = WildBattleOutcome.Draw;
            else if (state.Player.Pokemon.CurrentHp == 0) outcome = WildBattleOutcome.Lost;
            else if (state.Opponent.Pokemon.CurrentHp == 0) outcome = WildBattleOutcome.Won;
        }
        return state with { CalledParty = null, Outcome = outcome, AwaitingReplacement = false };
    }

    /// <summary>One genuine switch boundary. It does not schedule the opposing action or residual turn.</summary>
    public static WildBattleResult ResolveCalledPartySwitch(PokemonCatalog catalog, WildBattleState state,
        WildBattleCalledPartySwitchAction action)
    {
        if (state is null || action is null) return Fail(state, "called_party_switch_input");
        string? error = action.RulesVersion != CalledMoveKernelRulesVersion ? "called_party_switch_version" : ValidateCalledMoveActionState(catalog, state);
        var context = state.CalledParty; var pending = context?.PendingSwitch;
        if (error is null && (context is null || !Enum.IsDefined(action.Side))) error = "called_party_switch_context";
        if (error is null && (state.Outcome != WildBattleOutcome.Ongoing ||
            pending is not null && pending.Side != action.Side)) error = "called_party_switch_phase";
        if (error is null && !WildBattleSourcePartyEffects.LegalReserveIndices(context!, action.Side).Contains(action.PartyIndex)) error = "called_party_switch_candidate";
        var actor = action.Side == WildBattleSide.Player ? state.Player : state.Opponent;
        if (error is null && pending is null && (actor.Pokemon.CurrentHp == 0 || actor.Rooted || actor.MeanLookSource is not null || actor.BindTurnsRemaining > 0 ||
            actor.CalledTurnState?.Lock is not null || ForcedMoveSlot(actor) is not null)) error = "called_party_switch_locked";
        if (error is null && pending is null)
        {
            var other = action.Side == WildBattleSide.Player ? state.Opponent : state.Player;
            string opposingAbility = WildBattleEffective.Ability(other);
            if (SourceShadowTagTrapped(state, action.Side) || other.Pokemon.CurrentHp > 0 && (opposingAbility == "ABILITY_MAGNET_PULL" && WildBattleEffective.HasType(catalog, actor, "TYPE_STEEL") ||
                opposingAbility == "ABILITY_ARENA_TRAP" && WildBattleEffective.Ability(actor) != "ABILITY_LEVITATE" && !WildBattleEffective.HasType(catalog, actor, "TYPE_FLYING")))
                error = "called_party_switch_ability_trapped";
        }
        if (error is not null) return Fail(state, error);
        return Execute(state, state, execution => {
            execution.SourceCalledPartySwitch(catalog, action.Side, action.PartyIndex, pending?.Kind ?? WildBattleCalledSwitchKind.Voluntary, pending?.Invocation);
            execution.State = execution.State with { Revision = checked(state.Revision + 1) };
            execution.SourceCalledSynchronizeParties();
        });
    }

    internal sealed partial class Execution
    {
        internal Action<WildBattleSide, WildBattleCalledSwitchKind>? CalledPartySwitchCommitted { get; set; }
        private bool CalledPartyTrainer => State.CalledParty?.BattleKind == WildBattleCalledBattleKind.Trainer;
        internal void SourceCalledPreparePartyExecution(WildBattleSide side)
        {
            if (State.CalledParty is { } actual) calledActionContext = calledActionContext with { ActorPartyIndex = WildBattleSourcePartyEffects.ActiveIndex(actual, side) };
            if (!CalledPartyTrainer) return;
            CanForceOut = target => !WildBattleSourcePartyEffects.LegalReserveIndices(State.CalledParty!, target).IsEmpty;
            ForceOut = SourceCalledForceOut;
        }
        private void SourceCalledForceOut(PokemonCatalog catalog, WildBattleSide side, MoveId move)
        {
            SourceCalledSynchronizeParties(); var party = State.CalledParty!;
            var legal = WildBattleSourcePartyEffects.LegalReserveIndices(party, side);
            if (legal.IsEmpty) throw new UnsupportedAction("called_party_force_out_no_reserve");
            int next;
            do { next = Draw("wild-battle.force-out.choose-party", 6) % 6; } while (!legal.Contains(next));
            SourceCalledPartySwitch(catalog, side, next, WildBattleCalledSwitchKind.Forced, null);
            // switchindataupdate calls SwitchInClearSetData, which clears the current-move register.
            if (activeInvocation is not null) activeInvocation = activeInvocation with { CurrentMove = new("pret:MOVE_NONE") };
        }
        private readonly Dictionary<WildBattleSide, string> calledChangedItems = [];
        private void SourceCalledApplyChangedItems()
        {
            if (State.SourceMechanicsRulesVersion == BattleRulesProfile.CanonicalSourceSinglesVersion && activeInvocation is { } invocation)
                Set(invocation.Actor, SourceBattleHeldItems.FinishChoice(Get(invocation.Actor), invocation.ChosenMove,
                    invocation.Flags.HasFlag(WildBattleInvocationFlags.Obeys), choiceBatonPassFailed));
            foreach (var side in new[] { WildBattleSide.Player, WildBattleSide.Opponent })
                if (calledChangedItems.Remove(side, out var item)) Set(side, Get(side) with { HeldItem = item });
        }
        private void SourceCalledWritePartyItem(WildBattleSide side, string item, string reason)
        {
            var context = State.CalledParty!; int index = WildBattleSourcePartyEffects.ActiveIndex(context, side);
            var party = WildBattleSourcePartyEffects.Party(context, side); var before = party[index]; var mon = Get(side);
            var after = WildBattleSourcePartyEffects.WithPersistentHeldItem(before with {
                Pokemon = WildBattleTransformationRules.PersistentPokemon(mon), Status = WildBattleTransformationRules.PersistentStatus(mon),
                HeldItem = mon.HeldItem, KnockedOffItem = mon.KnockedOffItem }, item);
            State = State with { CalledParty = WildBattleSourcePartyEffects.SetParty(context, side, party.SetItem(index, after)) };
            if (!WildBattleSourcePartyEffects.SameMember(before, after)) Add(n => new WildBattleCalledPartyChangedEvent(n, State.Turn, side, index, reason, before, after));
        }
        internal void SourceCalledSynchronizeParties()
        {
            if (State.CalledParty is not { } party) return;
            foreach (var side in new[] { WildBattleSide.Player, WildBattleSide.Opponent })
            {
                int index = WildBattleSourcePartyEffects.ActiveIndex(party, side);
                var before = WildBattleSourcePartyEffects.Party(party, side)[index];
                party = WildBattleSourcePartyEffects.Synchronize(party, side, Get(side));
                var after = WildBattleSourcePartyEffects.Party(party, side)[index];
                if (!WildBattleSourcePartyEffects.SameMember(before, after)) Add(n => new WildBattleCalledPartyChangedEvent(n, State.Turn, side, index, "active-sync", before, after));
            }
            State = State with { CalledParty = party };
        }
        private void SourceCalledClearSpikes(WildBattleSide side)
        {
            if (State.CalledParty is not { } party) return;
            int before = side == WildBattleSide.Player ? party.PlayerSpikes : party.OpponentSpikes;
            if (before == 0) return;
            State = State with { CalledParty = side == WildBattleSide.Player ? party with { PlayerSpikes = 0 } : party with { OpponentSpikes = 0 } };
            Add(n => new WildBattleCalledSpikesChangedEvent(n, State.Turn, side, before, 0));
        }
        private void SourceCalledRecordKnockOff(WildBattleSide side, string item)
        {
            if (State.CalledParty is not { } context) return;
            int index = WildBattleSourcePartyEffects.ActiveIndex(context, side);
            var party = WildBattleSourcePartyEffects.Party(context, side); var before = party[index];
            var after = WildBattleSourcePartyEffects.WithPersistentHeldItem(before with { HeldItem = "ITEM_NONE", KnockedOffItem = item },
                WildBattleSourcePartyEffects.PersistentHeldItem(before));
            State = State with { CalledParty = WildBattleSourcePartyEffects.SetParty(context, side, party.SetItem(index, after)) };
            Add(n => new WildBattleCalledPartyChangedEvent(n, State.Turn, side, index, "knock-off-battle-only", before, after));
        }
        private void SourceCalledRecordConsumedItem(WildBattleSide side, string item)
        {
            if (State.CalledParty is not { } party) return;
            State = State with { CalledParty = side == WildBattleSide.Player ? party with { PlayerUsedHeldItem = item } : party with { OpponentUsedHeldItem = item } };
            SourceCalledWritePartyItem(side, "ITEM_NONE", "consumed-item");
        }
        private void SourceCalledRequireParty(PokemonMoveDefinition move)
        { if (CalledPartyRequired(move) && State.CalledParty is null) throw new UnsupportedAction("called_party_context_required"); }
        private void CalledPartyFailure(WildBattleSide side, WildBattleSide target, PokemonMoveDefinition move, string reason) =>
            Add(n => new WildBattleMoveFailedEvent(n, State.Turn, side, target, move.Id, reason));
        private void SetCalledOverlay(WildBattleSide side, PokemonMoveDefinition move, WildBattleCalledOverlay overlay)
        {
            var before = Get(side).CalledOverlay; Set(side, Get(side) with { CalledOverlay = overlay });
            Add(n => new WildBattleCalledOverlayChangedEvent(n, State.Turn, side, move.Id, before, overlay));
        }
        private void SetCalledType(WildBattleSide side, PokemonMoveDefinition move, string type) =>
            SetCalledOverlay(side, move, (Get(side).CalledOverlay ?? new()) with { Type1 = type, Type2 = type });
        private void ExecuteCalledPartyEffect(PokemonCatalog catalog, WildBattleSide side, int slot,
            PokemonMoveDefinition move, string purpose, ref bool handled)
        {
            if (!HasCalledPartyRecipe(move.Effect)) return;
            SourceCalledRequireParty(move); handled = true;
            var target = activeInvocation!.Target;
            switch (move.Effect)
            {
                case "EFFECT_CONVERSION": CalledConversion(catalog, side, move, purpose); break;
                case "EFFECT_CONVERSION_2": CalledConversion2(catalog, side, move, purpose); break;
                case "EFFECT_SPIKES": CalledSpikes(side, move); break;
                case "EFFECT_HEAL_BELL": CalledPartyHeal(side, move); break;
                case "EFFECT_BATON_PASS": CalledBatonPass(side, move); break;
                case "EFFECT_BEAT_UP": CalledBeatUp(catalog, side, target, move, purpose); break;
                case "EFFECT_MEMENTO": CalledMemento(side, target, move); break;
                case "EFFECT_ROLE_PLAY":
                case "EFFECT_SKILL_SWAP": CalledAbilityChange(side, target, move); break;
                case "EFFECT_RECYCLE": CalledRecycle(side, move); break;
                case "EFFECT_CAMOUFLAGE":
                    SpendInvocationPp(side, side, move);
                    var type = WildBattleSourcePartyEffects.CamouflageType(State.Terrain ?? throw new UnsupportedAction("called_camouflage_terrain"));
                    if (WildBattleEffective.HasType(catalog, Get(side), type)) CalledPartyFailure(side, side, move, "already-type"); else SetCalledType(side, move, type);
                    break;
                case "EFFECT_TEETER_DANCE": CalledTeeterDance(side, Other(side), move, purpose); break;
                case "EFFECT_TRICK": CalledTrick(side, target, move, purpose); break;
                case "EFFECT_THIEF": CalledThief(catalog, side, target, move, purpose); break;
            }
        }
        private void CalledConversion(PokemonCatalog catalog, WildBattleSide side, PokemonMoveDefinition move, string purpose)
        {
            SpendInvocationPp(side, side, move); var actor = Get(side);
            string MoveType(int index)
            {
                string type = catalog.Moves[actor.Pokemon.Moves[index].Move].Type;
                return type == "TYPE_MYSTERY" ? WildBattleEffective.HasType(catalog, actor, "TYPE_GHOST") ? "TYPE_GHOST" : "TYPE_NORMAL" : type;
            }
            if (!Enumerable.Range(0, actor.Pokemon.Moves.Length).Any(i => !WildBattleEffective.HasType(catalog, actor, MoveType(i))))
            { CalledPartyFailure(side, side, move, "no-different-move-type"); return; }
            int index; string chosen;
            do { do { index = Draw($"{purpose}.conversion-slot", 4) & 3; } while (index >= actor.Pokemon.Moves.Length); chosen = MoveType(index); }
            while (WildBattleEffective.HasType(catalog, actor, chosen));
            SetCalledType(side, move, chosen);
        }
        private void CalledConversion2(PokemonCatalog catalog, WildBattleSide side, PokemonMoveDefinition move, string purpose)
        {
            SpendInvocationPp(side, side, move); var actor = Get(side);
            if (actor.LastLandedMove is not { } landed || landed.Value is "pret:MOVE_NONE" or "pret:MOVE_UNAVAILABLE" || actor.SourceMoveHistory?.LastHitByType is not { } attackType)
            { CalledPartyFailure(side, side, move, "no-landed-move"); return; }
            if (actor.LastHitBy is { } source && catalog.Moves[landed].Effect is "EFFECT_RAZOR_WIND" or "EFFECT_SKULL_BASH" or "EFFECT_SKY_ATTACK" or "EFFECT_SOLAR_BEAM" or "EFFECT_SEMI_INVULNERABLE" or "EFFECT_BIDE" &&
                Get(source).CalledTurnState?.Lock is { Kind: not WildBattleCalledLockKind.Recharge })
            { CalledPartyFailure(side, side, move, "source-still-multiple-turns"); return; }
            var types = WildBattleEffective.Types(catalog, actor);
            var candidates = WildBattleSourcePartyEffects.Conversion2Candidates(attackType, types.Type1, types.Type2);
            if (candidates.IsEmpty) { CalledPartyFailure(side, side, move, "no-resistant-type"); return; }
            int index = WildBattleSourcePartyEffects.Conversion2ChoiceIndex(Draw($"{purpose}.conversion2-candidate", candidates.Length), candidates.Length);
            SetCalledType(side, move, candidates[index]);
        }
        private void CalledAbilityChange(WildBattleSide side, WildBattleSide target, PokemonMoveDefinition move)
        {
            SpendInvocationPp(side, target, move);
            if (!ScalarNoAccuracy(side, target, move)) { CalledPartyFailure(side, target, move, "semi-invulnerable"); return; }
            string own = WildBattleEffective.Ability(Get(side)), theirs = WildBattleEffective.Ability(Get(target));
            bool swap = move.Effect == "EFFECT_SKILL_SWAP";
            if (theirs == "ABILITY_WONDER_GUARD" || (swap ? own == "ABILITY_WONDER_GUARD" || own == "ABILITY_NONE" && theirs == "ABILITY_NONE" : theirs == "ABILITY_NONE"))
            { CalledPartyFailure(side, target, move, "uncopyable-ability"); return; }
            SetCalledOverlay(side, move, (Get(side).CalledOverlay ?? new()) with { Ability = theirs });
            if (swap) SetCalledOverlay(target, move, (Get(target).CalledOverlay ?? new()) with { Ability = own });
        }
        private void CalledSpikes(WildBattleSide side, PokemonMoveDefinition move)
        {
            var target = Other(side); var party = State.CalledParty!;
            int before = target == WildBattleSide.Player ? party.PlayerSpikes : party.OpponentSpikes;
            if (before == 3) activeInvocation = activeInvocation! with { Flags = activeInvocation!.Flags | WildBattleInvocationFlags.PpNotAffectedByPressure };
            else { State = State with { CalledParty = target == WildBattleSide.Player ? party with { PlayerSpikes = before + 1 } : party with { OpponentSpikes = before + 1 } };
                Add(n => new WildBattleCalledSpikesChangedEvent(n, State.Turn, target, before, before + 1)); }
            SpendInvocationPp(side, target, move);
            if (before == 3) CalledPartyFailure(side, target, move, "spikes-at-maximum");
        }
        private void CalledPartyHeal(WildBattleSide side, PokemonMoveDefinition move)
        {
            SpendInvocationPp(side, side, move); SourceCalledSynchronizeParties();
            var context = State.CalledParty!; var party = WildBattleSourcePartyEffects.Party(context, side);
            int active = WildBattleSourcePartyEffects.ActiveIndex(context, side); bool bell = move.Id.Value == "pret:MOVE_HEAL_BELL";
            for (int index = 0; index < party.Length; index++)
            {
                var before = party[index]; string ability = index == active ? WildBattleEffective.Ability(Get(side)) : before.Pokemon.AbilityId;
                if (bell && (before.IsEgg || ability == "ABILITY_SOUNDPROOF"))
                {
                    if (index == active && ability == "ABILITY_SOUNDPROOF") { RecordAbility?.Invoke(side, ability); Add(n => new WildBattleAbilityPreventedEvent(n, State.Turn, side, side, move.Id, ability)); }
                    continue;
                }
                var after = before with { Status = 0 }; party = party.SetItem(index, after);
                if (!WildBattleSourcePartyEffects.SameMember(before, after)) Add(n => new WildBattleCalledPartyChangedEvent(n, State.Turn, side, index, bell ? "heal-bell" : "aromatherapy", before, after));
                if (index == active) { ChangeStatus(side, 0, bell ? "heal-bell" : "aromatherapy"); Set(side, Get(side) with { Nightmare = false, Transform = Get(side).Transform is { } form ? form with { PartyStatusOverride = null } : null }); }
            }
            State = State with { CalledParty = WildBattleSourcePartyEffects.SetParty(State.CalledParty!, side, party) };
        }
        private void CalledBatonPass(WildBattleSide side, PokemonMoveDefinition move)
        {
            SpendInvocationPp(side, side, move); SourceCalledSynchronizeParties();
            var legal = WildBattleSourcePartyEffects.LegalReserveIndices(State.CalledParty!, side);
            if (legal.IsEmpty) { choiceBatonPassFailed = true; CalledPartyFailure(side, side, move, "no-eligible-reserve"); return; }
            var pending = new WildBattleCalledPendingSwitch(side, WildBattleCalledSwitchKind.BatonPass, legal, activeInvocation);
            State = State with { CalledParty = State.CalledParty! with { PendingSwitch = pending } };
            Add(n => new WildBattleCalledPartySwitchRequiredEvent(n, State.Turn, pending));
        }
        private void CalledBeatUp(PokemonCatalog catalog, WildBattleSide side, WildBattleSide target, PokemonMoveDefinition move, string purpose)
        {
            bool hit = SourceGenericAccuracy(side, target, move, purpose); SpendInvocationPp(side, target, move);
            if (!hit) { SourceGenericMiss(side, target, move); return; }
            SourceCalledSynchronizeParties(); var context = State.CalledParty!; var party = WildBattleSourcePartyEffects.Party(context, side);
            var candidates = WildBattleSourcePartyEffects.BeatUpIndices(context, side);
            if (candidates.IsEmpty) { CalledPartyFailure(side, target, move, "no-eligible-contributor"); return; }
            foreach (int index in candidates)
            {
                if (Get(target).Pokemon.CurrentHp == 0) break;
                Add(n => new WildBattleCalledBeatUpMemberEvent(n, State.Turn, side, index, party[index].Pokemon.Species));
                int damage = WildBattleSourcePartyEffects.BeatUpBaseDamage(catalog, party[index], Get(target), move.Power);
                bool critical = SourceGenericCritical(Get(side), Get(target), move, purpose); if (critical) damage *= 2;
                int percent = 100 - Draw($"{purpose}.damage", 16) % 16; damage = Math.Max(1, damage * percent / 100);
                damage = ScalarAdjust(target, move, damage, purpose);
                int lost = SourceGenericApplyDamage(side, target, move, damage, critical, percent);
                if (lost > 0 && SourceCalledHitReachedPokemon(target)) MtMoonRageOnDamage(side, target, move.Id);
                if (Get(target).Pokemon.CurrentHp == 0) ForestFaint(target);
                SourceColorChange(catalog, side, target, move);
                VermilionImmunityAbilities(); SourceCalledApplyChangedItems(); VermilionMoveEndBerries();
                SourceCalledMoveEndSubstitute();
            }
            ForestOutcome(true);
        }
        private void CalledMemento(WildBattleSide side, WildBattleSide target, PokemonMoveDefinition move)
        {
            SpendInvocationPp(side, target, move); var foe = Get(target);
            if (!foe.Protected && foe.AttackStage == 0 && foe.SpecialAttackStageDelta == -6)
            { CalledPartyFailure(side, target, move, "stats-at-minimum"); return; }
            var actor = Get(side); int hp = actor.Pokemon.CurrentHp;
            Set(side, actor with { Pokemon = actor.Pokemon with { CurrentHp = 0 } });
            Add(n => new WildBattleHpChangedEvent(n, State.Turn, side, side, move.Id, hp, 0, hp, false, 100));
            if (foe.Protected) CalledPartyFailure(side, target, move, "protected");
            else if (SourceCalledHasSubstitute(target)) CalledPartyFailure(side, target, move, "substitute");
            else { bool shown = false; SourceChangeStat(side, target, WildBattleStat.Attack, -2, move.Id, ref shown); SourceChangeStat(side, target, WildBattleStat.SpecialAttack, -2, move.Id, ref shown); }
            ForestFaint(side); ForestOutcome(true);
        }
        private void CalledRecycle(WildBattleSide side, PokemonMoveDefinition move)
        {
            SpendInvocationPp(side, side, move); var context = State.CalledParty!;
            string? item = side == WildBattleSide.Player ? context.PlayerUsedHeldItem : context.OpponentUsedHeldItem;
            if (item is null || Get(side).HeldItem != "ITEM_NONE") { CalledPartyFailure(side, side, move, "no-recyclable-item"); return; }
            Set(side, Get(side) with { HeldItem = item });
            State = State with { CalledParty = side == WildBattleSide.Player ? context with { PlayerUsedHeldItem = null } : context with { OpponentUsedHeldItem = null } };
        }
        private void CalledTeeterDance(WildBattleSide side, WildBattleSide target, PokemonMoveDefinition move, string purpose)
        {
            SpendInvocationPp(side, target, move); var mon = Get(target);
            if (WildBattleEffective.Ability(mon) == "ABILITY_OWN_TEMPO")
            { RecordAbility?.Invoke(target, "ABILITY_OWN_TEMPO"); Add(n => new WildBattleAbilityPreventedEvent(n, State.Turn, side, target, move.Id, "ABILITY_OWN_TEMPO")); return; }
            if (SourceCalledHasSubstitute(target)) { CalledPartyFailure(side, target, move, "substitute"); return; }
            if (mon.ConfusionTurnsRemaining > 0) { CalledPartyFailure(side, target, move, "already-confused"); return; }
            if (!SourceGenericAccuracy(side, target, move, purpose)) { SourceGenericMiss(side, target, move); return; }
            if (SourceCalledSafeguardBlocks(side, target, move.Id)) return;
            if (mon.Pokemon.CurrentHp > 0) ChangeConfusion(target, 2 + (Draw($"{purpose}.confusion-duration", 4) & 3), "teeter-dance");
        }
        private void CalledTrick(WildBattleSide side, WildBattleSide target, PokemonMoveDefinition move, string purpose)
        {
            SpendInvocationPp(side, target, move);
            if (SourceCalledHasSubstitute(target)) { CalledPartyFailure(side, target, move, "substitute"); return; }
            if (!SourceGenericAccuracy(side, target, move, purpose)) { CalledPartyFailure(side, target, move, "missed"); return; }
            var actor = Get(side); var foe = Get(target);
            if (side == WildBattleSide.Opponent || actor.KnockedOffItem is not null || foe.KnockedOffItem is not null ||
                actor.HeldItem == "ITEM_NONE" && foe.HeldItem == "ITEM_NONE" || actor.HeldItem == "ITEM_ENIGMA_BERRY" ||
                foe.HeldItem == "ITEM_ENIGMA_BERRY" || PokemonStorageRules.IsMailItem(actor.HeldItem) || PokemonStorageRules.IsMailItem(foe.HeldItem))
            { CalledPartyFailure(side, target, move, "item-swap-not-allowed"); return; }
            if (WildBattleEffective.Ability(foe) == "ABILITY_STICKY_HOLD")
            { RecordAbility?.Invoke(target, "ABILITY_STICKY_HOLD"); Add(n => new WildBattleAbilityPreventedEvent(n, State.Turn, side, target, move.Id, "ABILITY_STICKY_HOLD")); return; }
            Set(side, actor with { HeldItem = "ITEM_NONE", SourceChoiceMove = null }); Set(target, foe with { HeldItem = actor.HeldItem, SourceChoiceMove = null });
            if (foe.HeldItem != "ITEM_NONE") calledChangedItems[side] = foe.HeldItem;
            SourceCalledWritePartyItem(side, foe.HeldItem, "trick-party-write");
            SourceCalledWritePartyItem(target, actor.HeldItem, "trick-party-write");
        }
        private void CalledThief(PokemonCatalog catalog, WildBattleSide side, WildBattleSide target, PokemonMoveDefinition move, string purpose)
        {
            bool hit = SourceGenericAccuracy(side, target, move, purpose); SpendInvocationPp(side, target, move);
            if (!hit) { SourceGenericMiss(side, target, move); return; }
            bool critical = SourceGenericCritical(Get(side), Get(target), move, purpose);
            int percent = 100 - Draw($"{purpose}.damage", 16) % 16;
            int damage = SourceGenericDamage(catalog, Get(side), Get(target), target, move, critical, percent);
            int lost = SourceGenericApplyDamage(side, target, move, ScalarAdjust(target, move, damage, purpose), critical, percent);
            SourceGenericEffectiveness(catalog, side, target, move);
            // seteffectwithchance runs its own random command even for the 100% steal effect.
            int chance = move.SecondaryEffectChance * (WildBattleEffective.Ability(Get(side)) == "ABILITY_SERENE_GRACE" ? 2 : 1);
            if (Draw($"{purpose}.secondary-effect", 100) % 100 <= chance && damage > 0 && !SourceCalledHasSubstitute(target))
                SourceCalledStealItem(side, target, move);
            if (Get(target).Pokemon.CurrentHp == 0) ForestFaint(target);
            if (lost > 0 && SourceCalledHitReachedPokemon(target)) MtMoonRageOnDamage(side, target, move.Id);
            SourceGenericContact(catalog, side, target, move, purpose, damage); ForestOutcome(true);
        }
        internal bool SourceCalledPartyOutcome()
        {
            if (State.CalledParty is null) return false;
            SourceCalledSynchronizeParties(); var party = State.CalledParty!;
            bool playerAlive = WildBattleSourcePartyEffects.HasLivingMember(party, WildBattleSide.Player);
            bool opponentAlive = WildBattleSourcePartyEffects.HasLivingMember(party, WildBattleSide.Opponent);
            if (!playerAlive || !opponentAlive)
            {
                var result = !playerAlive && !opponentAlive ? WildBattleOutcome.Draw : !opponentAlive ? WildBattleOutcome.Won : WildBattleOutcome.Lost;
                State = State with { Outcome = result, AwaitingReplacement = false, CalledParty = party with { PendingSwitch = null },
                    ForestPlayerMoveSlot = null, ForestOpponentMoveSlot = null, ForestResidualsFinished = false };
                Add(n => new WildBattleEndedEvent(n, State.Turn, result)); return true;
            }
            if (party.PendingSwitch is not null) return true;
            foreach (var side in new[] { WildBattleSide.Player, WildBattleSide.Opponent })
            {
                if (Get(side).Pokemon.CurrentHp != 0) continue;
                var legal = WildBattleSourcePartyEffects.LegalReserveIndices(party, side);
                var pending = new WildBattleCalledPendingSwitch(side, WildBattleCalledSwitchKind.FaintedReplacement,
                    legal);
                State = State with { Outcome = WildBattleOutcome.Ongoing, AwaitingReplacement = false, CalledParty = party with { PendingSwitch = pending } };
                Add(n => new WildBattleCalledPartySwitchRequiredEvent(n, State.Turn, pending)); return true;
            }
            return true;
        }
        internal void SourceCalledPartySwitch(PokemonCatalog catalog, WildBattleSide side, int index,
            WildBattleCalledSwitchKind kind, WildBattleMoveInvocation? suspended)
        {
            var departing = Get(side); bool baton = kind == WildBattleCalledSwitchKind.BatonPass;
            if (WildBattleEffective.Ability(departing) == "ABILITY_NATURAL_CURE")
            {
                ChangeStatus(side, 0, "natural-cure");
                if (Get(side).Transform is { } form) Set(side, Get(side) with { Transform = form with { PartyStatusOverride = null } });
            }
            // Source party status keeps the poison kind; only the active toxic counter is reset on entry.
            SourceCalledSynchronizeParties(); var context = State.CalledParty!;
            int outgoingIndex = WildBattleSourcePartyEffects.ActiveIndex(context, side);
            var incoming = WildBattleSourcePartyEffects.Party(context, side)[index];
            var next = new WildBattleCombatant(incoming.Pokemon, HeldItem: incoming.KnockedOffItem is not null ? "ITEM_NONE" : WildBattleSourcePartyEffects.PersistentHeldItem(incoming),
                Status: WildBattleSourceFieldEffects.ResetToxicCounter(incoming.Status), AttackBadgeBoost: departing.AttackBadgeBoost)
                { SpeedBadgeBoost = departing.SpeedBadgeBoost, DefenseBadgeBoost = departing.DefenseBadgeBoost,
                    FirstTurnCounter = 2, KnockedOffItem = incoming.KnockedOffItem, CalledDamage = departing.CalledDamage };
            if (baton)
            {
                next = next with {
                    AttackStage = departing.AttackStage, DefenseStage = departing.DefenseStage,
                    SpeedStage = departing.SpeedStage, AccuracyStage = departing.AccuracyStage, EvasionStage = departing.EvasionStage,
                    SpecialAttackStageDelta = departing.SpecialAttackStageDelta, SpecialDefenseStageDelta = departing.SpecialDefenseStageDelta,
                    ConfusionTurnsRemaining = departing.ConfusionTurnsRemaining, FocusEnergy = departing.FocusEnergy,
                    MeanLookSource = departing.MeanLookSource, Cursed = departing.Cursed, LeechSeedSource = departing.LeechSeedSource,
                    Rooted = departing.Rooted, MudSport = departing.MudSport, WaterSport = departing.WaterSport,
                    CalledVolatile = departing.CalledVolatile is { } v && (v.SubstituteHp > 0 || v.PerishSongTimer is not null)
                        ? new() { SubstituteHp = v.SubstituteHp, PerishSongTimer = v.PerishSongTimer } : null,
                    CalledTurnState = departing.CalledTurnState is { SureHitTurnsRemaining: > 0 } turn
                        ? new() { SureHitTurnsRemaining = turn.SureHitTurnsRemaining, SureHitSource = turn.SureHitSource } : null
                };
            }
            var otherSide = Other(side); var other = Get(otherSide);
            // Battle slot source references survive Baton except infatuation/binding, which always clear.
            var history = other.SourceMoveHistory;
            if (history is not null) history = side == WildBattleSide.Player
                ? history with { LastTakenMove = null, LastTakenFromPlayer = null }
                : history with { LastTakenMove = null, LastTakenFromOpponent = null };
            Set(otherSide, other with {
                InfatuationSource = other.InfatuationSource == side ? null : other.InfatuationSource,
                BindTurnsRemaining = 0, BindingMove = null,
                MeanLookSource = !baton && other.MeanLookSource == side ? null : other.MeanLookSource,
                SourceMoveHistory = history,
                CalledTurnState = other.CalledTurnState is { SureHitSource: { } sureSource } sure && sureSource == side
                    ? sure with { SureHitSource = baton ? side : null, SureHitTurnsRemaining = baton ? 2 : 0 } : other.CalledTurnState
            });
            // Clearing local hit bookkeeping cannot erase the captured Baton subset or global delayed slots.
            SourceCalledClearVolatiles(side);
            State = State with { CalledParty = side == WildBattleSide.Player
                ? State.CalledParty! with { PlayerActiveIndex = index, PendingSwitch = null }
                : State.CalledParty! with { OpponentActiveIndex = index, PendingSwitch = null }, AwaitingReplacement = false };
            var selectedParty = WildBattleSourcePartyEffects.Party(State.CalledParty!, side);
            var selected = WildBattleSourcePartyEffects.WithPersistentHeldItem(incoming with { HeldItem = next.HeldItem }, WildBattleSourcePartyEffects.PersistentHeldItem(incoming));
            State = State with { CalledParty = WildBattleSourcePartyEffects.SetParty(State.CalledParty!, side, selectedParty.SetItem(index, selected)) };
            Set(side, next);
            if (WildBattleSourceFieldEffects.IsValidToxic(next.Status) && State.CalledField is null) State = State with { CalledField = new() };
            Add(n => new WildBattleCalledPartySwitchedEvent(n, State.Turn, side, outgoingIndex, index, kind));
            CalledPartySwitchCommitted?.Invoke(side, kind);
            int spikes = side == WildBattleSide.Player ? State.CalledParty.PlayerSpikes : State.CalledParty.OpponentSpikes;
            if (spikes > 0 && !WildBattleEffective.HasType(catalog, next, "TYPE_FLYING") && WildBattleEffective.Ability(next) != "ABILITY_LEVITATE")
            {
                int before = next.Pokemon.CurrentHp, damage = WildBattleSourcePartyEffects.SpikesDamage(next.Pokemon.Stats.MaxHp, spikes), after = Math.Max(0, before - damage);
                Set(side, Get(side) with { Pokemon = next.Pokemon with { CurrentHp = after } });
                Add(n => new WildBattleHpChangedEvent(n, State.Turn, otherSide, side, new("pret:MOVE_SPIKES"), before, after, damage, false, 100));
                if (after == 0) ForestFaint(side);
            }
            if (Get(side).Pokemon.CurrentHp > 0)
            {
                SourceTurnCycleSwitchIn(side);
                ApplyRoute3EntryAbility(side);
            }
            // BattleScript_ActionSwitch explicitly runs only the immunity and Mirror Move end cases.
            if (kind == WildBattleCalledSwitchKind.Voluntary) VermilionImmunityAbilities();
            if (suspended is not null)
            {
                // SwitchInClearSetData writes gCurrentMove=NONE. Cmd_moveend still sees chosen Metronome.
                activeInvocation = suspended with { CurrentMove = new("pret:MOVE_NONE") };
                VermilionImmunityAbilities(); SourceCalledApplyChangedItems(); VermilionMoveEndBerries();
                FinishCalledMoveHistory(catalog, 0);
            }
            SourceCalledSynchronizeParties(); ForestOutcome(true);
            SourceTraceForecastFaintedActions();
        }

        private void SourceCalledStealItem(WildBattleSide side, WildBattleSide target, PokemonMoveDefinition move)
        {
            var actor = Get(side); var foe = Get(target);
            if (side == WildBattleSide.Opponent || actor.KnockedOffItem is not null) return;
            if (foe.HeldItem != "ITEM_NONE" && WildBattleEffective.Ability(foe) == "ABILITY_STICKY_HOLD")
            { RecordAbility?.Invoke(target, "ABILITY_STICKY_HOLD"); Add(n => new WildBattleAbilityPreventedEvent(n, State.Turn, side, target, move.Id, "ABILITY_STICKY_HOLD")); return; }
            if (actor.HeldItem != "ITEM_NONE" || foe.HeldItem is "ITEM_NONE" or "ITEM_ENIGMA_BERRY" || PokemonStorageRules.IsMailItem(foe.HeldItem)) return;
            Set(target, foe with { HeldItem = "ITEM_NONE", SourceChoiceMove = null }); calledChangedItems[side] = foe.HeldItem;
            SourceCalledWritePartyItem(side, foe.HeldItem, "steal-party-write");
            SourceCalledWritePartyItem(target, "ITEM_NONE", "steal-party-write");
            Add(n => new WildBattleHeldItemStolenEvent(n, State.Turn, side, target, move.Id, foe.HeldItem));
        }
    }
}
