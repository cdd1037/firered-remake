using System.Collections.Immutable;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

public sealed record SourceTurnOrderResult(ImmutableArray<BattlerSlot> Order, RngState Rng,
    ImmutableArray<RandomTrace> Trace, string? Fault);

/// <summary>Source GetWhoStrikesFirst and SetActionsAndBattlersTurnOrder traversal. This is
/// ordering only: item/switch execution, entry effects and recipe admission belong to the owner.</summary>
public static class SourceBattleTurnOrder
{
    // GetWhoStrikesFirst's one shared integer recipe. The caller owns weather suppression,
    // turn-number lifetime, item admission and random low-bit tie draws.
    internal static uint EffectiveSpeed(WildBattleCombatant mon, bool weatherBoosted, bool playerSide, ushort randomTurnNumber)
    {
        if (mon.SpeedStage is < 0 or > 12 || mon.Pokemon.Stats.Speed is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(mon));
        uint basis = (uint)mon.Pokemon.Stats.Speed * (weatherBoosted ? 2u : 1u);
        uint speed = mon.SpeedStage < 6 ? basis * 2 / (uint)(8 - mon.SpeedStage) : basis * (uint)(mon.SpeedStage - 4) / 2;
        if (mon.SpeedBadgeBoost && playerSide) speed = speed * 110 / 100;
        var item = SourceBattleHeldItems.OrderEffect(mon.HeldItem);
        if (item?.HoldEffect == "HOLD_EFFECT_MACHO_BRACE") speed /= 2;
        if (mon.Status == WildBattleStatus.Paralysis) speed /= 4;
        if (item?.HoldEffect == "HOLD_EFFECT_QUICK_CLAW" && randomTurnNumber < 65535 * item.Parameter / 100) speed = uint.MaxValue;
        return speed;
    }

    public static SourceTurnOrderResult Resolve(PokemonCatalog catalog, SourceBattleFrame frame,
        WildBattleOrderPhase phase)
    {
        SourceTurnOrderResult Fail(string error) => new([], frame.Rng, [], error);
        if (SourceBattleIdentityRules.Validate(frame) is { } invalid) return Fail(invalid);
        if (frame.Queue.Length != frame.BattlerCount || !Enum.IsDefined(phase)) return Fail("source_order_phase");
        if (frame.Slots.Take(frame.BattlerCount).Any(c => c.Body is null)) return Fail("source_order_body_required");
        if (frame.Slots.Take(frame.BattlerCount).Any(c => c.Body!.HeldItem is not ("ITEM_NONE" or "ITEM_MACHO_BRACE" or "ITEM_QUICK_CLAW") &&
            !(frame.Layout == SourceBattleLayout.PresetDoubles && SourceFourRewards.SupportsRewardHeldItem(c.Body.HeldItem))))
            return Fail("source_order_held_effect_unsupported");
        var trace = ImmutableArray.CreateBuilder<RandomTrace>(); var rng = frame.Rng;
        // WEATHER_HAS_EFFECT uses source CHECK_ON_FIELD: hp != 0, with no absent-mask filter.
        bool suppressWeather = SourceWeatherRules.IsSuppressed(frame);
        uint Speed(BattlerSlot slot)
        {
            var mon = frame.Get(slot).Body!;
            bool boosted = !suppressWeather && (WildBattleEffective.Ability(mon) == "ABILITY_SWIFT_SWIM" && SourceWeatherRules.HasRain(frame.Field) ||
                WildBattleEffective.Ability(mon) == "ABILITY_CHLOROPHYLL" && SourceWeatherRules.HasSun(frame.Field));
            return EffectiveSpeed(mon, boosted, slot.Side == BattleSide.Player, frame.RandomTurnNumber);
        }
        int Priority(SourceQueuedAction action) => (action.Kind != SourceActionKind.Move && !(phase == WildBattleOrderPhase.EndTurn && action.Kind == SourceActionKind.Cancelled && action.Token is not null)) || action.MoveSlot is null
            ? 0 : action.MoveSlot == -1 ? catalog.Moves[new("pret:MOVE_STRUGGLE")].Priority
            : catalog.Moves[frame.Get(action.Slot).Body!.Pokemon.Moves[action.MoveSlot.Value].Move].Priority;
        try
        {
            // Source resets the numeric battler IDs before each sort, including end turn.
            var queue = frame.Queue.OrderBy(a => a.Slot.Value).ToArray();
            if (phase == WildBattleOrderPhase.Actions)
                queue = queue.Where(a => a.Kind is SourceActionKind.Item or SourceActionKind.Switch)
                    .Concat(queue.Where(a => a.Kind is not (SourceActionKind.Item or SourceActionKind.Switch))).ToArray();
            for (int i = 0; i < queue.Length - 1; i++)
                for (int j = i + 1; j < queue.Length; j++)
                {
                    if (phase == WildBattleOrderPhase.Actions && (queue[i].Kind is SourceActionKind.Item or SourceActionKind.Switch ||
                        queue[j].Kind is SourceActionKind.Item or SourceActionKind.Switch)) continue;
                    int left = phase == WildBattleOrderPhase.Entry ? 0 : Priority(queue[i]), right = phase == WildBattleOrderPhase.Entry ? 0 : Priority(queue[j]);
                    uint a = Speed(queue[i].Slot), b = Speed(queue[j].Slot);
                    bool swap = left < right;
                    if (left == right)
                    {
                        if (a == b)
                        {
                            var draw = Gen3Rng.Next(rng, $"source-battle.{phase.ToString().ToLowerInvariant()}.speed-tie", 2);
                            rng = draw.State; trace.Add(draw.Trace); swap = (draw.Value & 1) != 0;
                        }
                        else swap = a < b;
                    }
                    if (swap) (queue[i], queue[j]) = (queue[j], queue[i]);
                }
            return new(queue.Select(a => a.Slot).ToImmutableArray(), rng, trace.ToImmutable(), null);
        }
        catch (OverflowException) { return Fail("source_order_counter_exhausted"); }
        catch (ArgumentException) { return Fail("source_order_invalid_value"); }
        catch (KeyNotFoundException) { return Fail("source_order_unknown_move"); }
        catch (IndexOutOfRangeException) { return Fail("source_order_move_slot"); }
    }
}
