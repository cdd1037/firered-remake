using System.Collections.Immutable;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;

namespace Game.Application;

public enum CampaignGeneralAnimationBoundary { BeforeText, AfterText }
public sealed record CampaignGeneralAnimationDispatch(string BattleId, long Sequence, long ReceiptSequence,
    BattlerSlot Actor, BattlerSlot Target, string GeneralLabel, CampaignGeneralAnimationBoundary Boundary,
    ImmutableArray<bool> SpriteVisibility);

/// <summary>Named general scripts authenticate the current committed result, independently
/// of move invocations. Inputs describe the current display, never the final battle frame.</summary>
public static class CampaignGeneralAnimationPresentation
{
    private static void ValidateInputs(IReadOnlyList<MoveId?> entry, IReadOnlyList<bool> substitutes, int count)
    {
        ArgumentNullException.ThrowIfNull(entry); ArgumentNullException.ThrowIfNull(substitutes);
        if (entry.Count != count) throw new ArgumentException("One pre-command charge move is required for each battle position.", nameof(entry));
        if (substitutes.Count != count) throw new ArgumentException("One current displayed Substitute flag is required for each battle position.", nameof(substitutes));
    }

    public static CampaignGeneralAnimationDispatch? AtResult(CampaignWildBattle battle, bool sourceRules,
        SourceBattleScene scene, IReadOnlyList<MoveId?> entryChargeMoves, IReadOnlyList<bool> behindSubstitutes)
    {
        ValidateInputs(entryChargeMoves, behindSubstitutes, 2);
        if (!sourceRules || battle.Phase != CampaignBattlePhase.Events ||
            !CampaignResultAnimationPolicy.Allows(CampaignResultAnimationKind.General, scene)) return null;
        return Match(battle.Id, battle.Events,
            battle.EventIndex, behindSubstitutes, slot => CampaignHeldItemAnimationPresentation.SpriteVisibleAtResult(
                battle.Events, battle.EventIndex, slot, entryChargeMoves));
    }

    public static CampaignGeneralAnimationDispatch? AtResult(CampaignDoubleBattle battle, SourceBattleScene scene,
        IReadOnlyList<MoveId?> entryChargeMoves, IReadOnlyList<bool> behindSubstitutes)
    {
        ValidateInputs(entryChargeMoves, behindSubstitutes, 4);
        if (battle.Stage != CampaignDoubleBattleStage.Combat ||
            !CampaignResultAnimationPolicy.Allows(CampaignResultAnimationKind.General, scene)) return null;
        return Match(battle.Id, battle.Events, battle.EventIndex, behindSubstitutes,
            slot => CampaignHeldItemAnimationPresentation.SpriteVisibleAtResult(battle.Events, battle.EventIndex, slot, entryChargeMoves));
    }

    private static CampaignGeneralAnimationDispatch? Match(string id, IReadOnlyList<SourceBattleEvent> events,
        int index, IReadOnlyList<bool> substitutes, Func<BattlerSlot, bool?> visible)
    {
        if (index < 0 || index >= events.Count) return null;
        var current = events[index];
        if (current.Sequence < 0 || current.Turn < 0) return null;
        for (int i = 1; i <= index; i++) if (events[i].Sequence <= events[i - 1].Sequence) return null;
        BattlerSlot actor, target; string label; long receipt; CampaignGeneralAnimationBoundary boundary;
        bool weather = false;
        switch (current)
        {
            case SourceFocusPunchPreparedEvent focus:
                if (focus.Actor.Value >= substitutes.Count || focus.ChosenSlot is < 0 or > 3 || focus.PartyIndex is < 0 or > 5 ||
                    events.Take(index).OfType<SourceFocusPunchPreparedEvent>().Any(e => e.Turn == focus.Turn && e.Actor == focus.Actor)) return null;
                actor = target = focus.Actor; receipt = focus.Sequence;
                label = "General_FocusPunchSetUp"; boundary = CampaignGeneralAnimationBoundary.BeforeText;
                break;
            case SourceCalledDelayedEvent delayed when delayed.Move.Value is "pret:MOVE_FUTURE_SIGHT" or "pret:MOVE_DOOM_DESIRE":
                var hit = FutureImpact(events, index, delayed, substitutes.Count);
                if (hit is null) return null;
                actor = hit.Value.Actor; target = hit.Value.Target; receipt = hit.Value.Sequence;
                label = delayed.Move.Value == "pret:MOVE_FUTURE_SIGHT" ? "General_FutureSightHit" : "General_DoomDesireHit";
                boundary = CampaignGeneralAnimationBoundary.AfterText;
                break;
            case SourceMtMoonVolatileEvent or SourceWeatherContinuedEvent:
                var continuation = CampaignWeatherContinuationPresentation.At(events, index);
                if (continuation is null || continuation.Controller.Value >= substitutes.Count) return null;
                actor = target = continuation.Controller; receipt = continuation.ReceiptSequence;
                label = continuation.Weather switch {
                    "rain" => "General_Rain", "sandstorm" => "General_Sandstorm",
                    "sun" => "General_Sun", "hail" => "General_Hail", _ => throw new InvalidOperationException()
                };
                weather = true; boundary = CampaignGeneralAnimationBoundary.AfterText;
                break;
            default: return null;
        }
        var visibility = ImmutableArray.CreateBuilder<bool>(substitutes.Count);
        for (int slot = 0; slot < substitutes.Count; slot++)
        {
            bool? value = visible(new(slot)); if (value is null) return null;
            visibility.Add(value.Value);
        }
        var snapshot = visibility.MoveToImmutable();
        // Cmd_playanimation checks its controller battler before launching. The
        // delayed script then remaps its target; that target's Substitute is valid.
        if (!weather && (!snapshot[actor.Value] || substitutes[actor.Value])) return null;
        return new(id, current.Sequence, receipt, actor, target, label, boundary, snapshot);
    }

    private readonly record struct Hit(long Sequence, int Turn, BattlerSlot Actor, BattlerSlot Target,
        MoveId Move, int Before, int After, int Damage, bool Critical, int Percent, bool Substitute);
    private static Hit? FutureImpact(IReadOnlyList<SourceBattleEvent> events, int index, SourceCalledDelayedEvent delayed, int count)
    {
        if (delayed.Reason != "resolved" || delayed.Before != 1 || delayed.After != 0 || delayed.SourcePartyIndex is not null ||
            delayed.Target.Value >= count || events.Take(index).OfType<SourceCalledDelayedEvent>().Any(e =>
                e.Turn == delayed.Turn && e.Target == delayed.Target && e.Reason == "resolved" &&
                e.Move.Value is "pret:MOVE_FUTURE_SIGHT" or "pret:MOVE_DOOM_DESIRE")) return null;
        int next = index + 1;
        SourceDamageAdjustedEvent? adjusted = next < events.Count ? events[next] as SourceDamageAdjustedEvent : null;
        if (adjusted is not null)
        {
            if (adjusted.Sequence != delayed.Sequence + 1 || adjusted.Turn != delayed.Turn ||
                adjusted.Target != delayed.Target || adjusted.Move != delayed.Move) return null;
            next++;
        }
        if (next >= events.Count) return null;
        Hit? parsed = events[next] switch {
            SourceHpChangedEvent h when h.PreparedBasePower is null && h.PreparedFuryCutterCounter is null && h.PreparedAnimationPower is null && h.PreparedAnimationWeather is null =>
                new(h.Sequence, h.Turn, h.Actor, h.Target, h.Move, h.HpBefore, h.HpAfter, h.Damage, h.Critical, h.RandomPercent, false),
            SourceSubstituteDamagedEvent h when h.PreparedBasePower is null && h.PreparedFuryCutterCounter is null && h.PreparedAnimationPower is null && h.PreparedAnimationWeather is null =>
                new(h.Sequence, h.Turn, h.Actor, h.Target, h.Move, h.HpBefore, h.HpAfter, h.Damage, h.Critical, h.RandomPercent, true),
            _ => null
        };
        if (parsed is not { } hit || hit.Sequence != (adjusted?.Sequence ?? delayed.Sequence) + 1 || hit.Turn != delayed.Turn ||
            hit.Actor.Value >= count || hit.Target != delayed.Target || hit.Move != delayed.Move || hit.Before <= 0 ||
            hit.After < 0 || hit.After > hit.Before || hit.Damage < 0 || hit.After != Math.Max(0, hit.Before - hit.Damage) ||
            hit.Critical || hit.Percent is < 85 or > 100) return null;
        if (adjusted is null) return hit.Damage > 0 ? hit : null;
        // Successful Focus Band/Endure can commit zero HP loss at 1 HP. The
        // adjustment, not a fabricated positive loss, proves that successful hit.
        if (hit.Substitute || adjusted.DamageBefore < hit.Before || adjusted.DamageAfter != hit.Damage ||
            hit.Damage != hit.Before - 1 || hit.After != 1 || adjusted.Endured == adjusted.HungOn ||
            adjusted.HungOn && adjusted.Item != "ITEM_FOCUS_BAND") return null;
        return hit;
    }
}
