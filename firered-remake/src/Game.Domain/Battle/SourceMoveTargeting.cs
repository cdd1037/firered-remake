using System.Collections.Immutable;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

public sealed record SourceTargetResult(BattlerSlot? Target, RngState Rng,
    ImmutableArray<RandomTrace> Trace, bool LightningRodRedirected, string? Fault)
{
    public bool Succeeded => Fault is null;
}

/// <summary>Source action-time selection and GetMoveTarget are different entry points. This
/// resolver does not run a move, spend PP, or mutate presence. A null target is an explicit
/// no-target boundary. Source absence, HP and the approved deferred vacancy stay distinct.</summary>
public static class SourceMoveTargeting
{
    public static SourceTargetResult ResolveLearned(SourceBattleFrame frame, BattlerSlot actor,
        BattlerSlot selected, PokemonMoveDefinition move) => Resolve(frame, actor, selected, move, false);

    /// <summary>Only target selection for caller/forced-move transitions; this is not caller execution.</summary>
    public static SourceTargetResult ResolveCalled(SourceBattleFrame frame, BattlerSlot actor,
        PokemonMoveDefinition move, string? targetOverride = null) => Resolve(frame, actor, actor.Opposite, move, true, targetOverride);

    private static SourceTargetResult Resolve(SourceBattleFrame frame, BattlerSlot actor,
        BattlerSlot selected, PokemonMoveDefinition move, bool called, string? targetOverride = null)
    {
        SourceTargetResult Fail(string code) => new(null, frame.Rng, [], false, code);
        if (SourceBattleIdentityRules.Validate(frame) is { } invalid) return Fail(invalid);
        if (actor.Value >= frame.BattlerCount || selected.Value >= frame.BattlerCount || frame.Get(actor).Body is null ||
            !MetronomeSourceMoveDefinition.Matches(move)) return Fail("source_target_input");
        string targetMode = targetOverride ?? move.Target;
        if (targetMode is not ("MOVE_TARGET_SELECTED" or "MOVE_TARGET_RANDOM" or "MOVE_TARGET_BOTH" or
            "MOVE_TARGET_FOES_AND_ALLY" or "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED" or
            "MOVE_TARGET_DEPENDS" or "MOVE_TARGET_OPPONENTS_FIELD")) return Fail("source_target_mode");
        var rng = frame.Rng; var trace = ImmutableArray.CreateBuilder<RandomTrace>();
        // Applied only at this action's target-selection boundary. Never change SourceAbsentMask
        // or reuse this predicate for damage, ability scans, or script target traversal.
        bool Unavailable(BattlerSlot slot) => frame.IsSourceAbsent(slot) || frame.IsDeferredKoVacant(slot);
        bool HasBody(BattlerSlot slot) => frame.Get(slot).Body is not null;
        bool Rod(BattlerSlot slot) => frame.Get(slot).Body is { } b && WildBattleEffective.Ability(b) == "ABILITY_LIGHTNING_ROD";
        var oppositeSide = (BattleSide)((int)actor.Side ^ 1);
        var side = frame.Sides[(int)oppositeSide];
        bool FollowMe() => side.FollowMeTimer != 0 && side.FollowMeTarget is { } slot && frame.Get(slot).Body?.Pokemon.CurrentHp > 0;
        BattlerSlot FirstOpponent() => new((int)oppositeSide);
        BattlerSlot? Finish(BattlerSlot slot) => HasBody(slot) && !Unavailable(slot) ? slot : null;
        BattlerSlot? Fallback(BattlerSlot slot)
        {
            if (!Unavailable(slot)) return Finish(slot);
            if (slot.Side != actor.Side) return Finish(slot.Partner);
            slot = actor.Opposite;
            if (Unavailable(slot)) slot = slot.Partner;
            return Finish(slot);
        }
        ushort Draw(string kind, int bound)
        {
            var d = Gen3Rng.Next(rng, $"source-battle.slot-{actor.Value}.target.{(called ? "called" : "learned")}.{kind}", bound);
            rng = d.State; trace.Add(d.Trace); return d.Value;
        }
        BattlerSlot RandomOpponent() => new((int)oppositeSide + ((Draw("random-flank", 2) & 1) != 0 ? 0 : 2));
        bool HasOpponent() => Enumerable.Range(0, frame.BattlerCount).Select(i => new BattlerSlot(i)).Any(s => s.Side != actor.Side && !Unavailable(s));
        try
        {
            BattlerSlot? target; bool redirected = false;
            if (called)
            {
                switch (targetMode)
                {
                    case "MOVE_TARGET_SELECTED":
                        if (FollowMe()) target = side.FollowMeTarget;
                        else
                        {
                            // The approved deferred vacancy can make the original rejection loop
                            // have no solution. Detect that state before any random draw.
                            if (!HasOpponent()) return new(null, frame.Rng, [], false, null);
                            BattlerSlot candidate;
                            do { candidate = new(Draw("selected-rejection", frame.BattlerCount) % frame.BattlerCount); }
                            while (candidate == actor || candidate.Side == actor.Side || Unavailable(candidate));
                            if (move.Type == "TYPE_ELECTRIC" && Enumerable.Range(0, frame.BattlerCount).Select(i => new BattlerSlot(i)).Any(s => s.Side != actor.Side && Rod(s)) && !Rod(candidate))
                            { candidate = candidate.Partner; redirected = true; }
                            // Source Lightning Rod has no HP/absence eligibility filter.
                            target = candidate;
                        }
                        break;
                    case "MOVE_TARGET_RANDOM":
                        if (FollowMe()) target = side.FollowMeTarget;
                        else if (frame.Layout == SourceBattleLayout.PresetDoubles)
                        {
                            if (!HasOpponent()) return new(null, frame.Rng, [], false, null);
                            var candidate = RandomOpponent(); target = Unavailable(candidate) ? candidate.Partner : candidate;
                        }
                        else target = FirstOpponent();
                        break;
                    case "MOVE_TARGET_USER": case "MOVE_TARGET_USER_OR_SELECTED": target = actor; break;
                    default:
                        var first = FirstOpponent(); target = Unavailable(first) ? first.Partner : first; break;
                }
            }
            else
            {
                if (move.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED") selected = actor;
                if (move.Target == "MOVE_TARGET_SELECTED" && FollowMe()) target = side.FollowMeTarget;
                else
                {
                    BattlerSlot? rod = null;
                    if (frame.Layout == SourceBattleLayout.PresetDoubles && side.FollowMeTimer == 0 &&
                        (move.Power != 0 || move.Target != "MOVE_TARGET_USER") && !Rod(selected) && move.Type == "TYPE_ELECTRIC")
                    {
                        var rods = Enumerable.Range(0, frame.BattlerCount).Select(i => new BattlerSlot(i))
                            .Where(s => s.Side != actor.Side && s != selected && Rod(s)).ToArray();
                        if (rods.Length != 0)
                        {
                            if (frame.Queue.Length != frame.BattlerCount) return Fail("source_target_lightning_rod_order_required");
                            rod = frame.Queue.Select(a => a.Slot).First(s => rods.Contains(s));
                        }
                    }
                    if (rod is { } redirectedTarget) { target = redirectedTarget; redirected = true; }
                    else if (frame.Layout == SourceBattleLayout.PresetDoubles && move.Target == "MOVE_TARGET_RANDOM")
                    {
                        if (!HasOpponent()) return new(null, frame.Rng, [], false, null);
                        var candidate = RandomOpponent(); target = Unavailable(candidate) ? candidate.Partner : candidate;
                    }
                    else target = Fallback(selected);
                }
            }
            // Preserve original source-absent/zero-HP Lightning Rod result for its owner to gate;
            // only a deferred vacancy is the explicit new no-target boundary.
            if (target is { } resolved && (resolved.Value >= frame.BattlerCount || !HasBody(resolved) || frame.IsDeferredKoVacant(resolved))) target = null;
            return new(target, rng, trace.ToImmutable(), redirected, null);
        }
        catch (OverflowException) { return Fail("source_target_counter_exhausted"); }
    }

    /// <summary>CountAliveMonsInBattle(BATTLE_ALIVE_DEF_SIDE): source absence only, no HP test.</summary>
    public static int CountSourcePresentDefenders(SourceBattleFrame frame, BattlerSlot target) =>
        Enumerable.Range(0, frame.BattlerCount).Select(i => new BattlerSlot(i)).Count(s => s.Side == target.Side && !frame.IsSourceAbsent(s));

    internal static IEnumerable<BattlerSlot> NumericScriptTargets(SourceBattleFrame frame, BattlerSlot actor) =>
        Enumerable.Range(0, frame.BattlerCount).Select(i => new BattlerSlot(i)).Where(s => s != actor && !frame.IsSourceAbsent(s));
}
