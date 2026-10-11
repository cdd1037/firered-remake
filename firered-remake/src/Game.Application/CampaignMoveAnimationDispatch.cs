using Game.Domain.Battle;
using Game.Domain.Options;
using Game.Domain.WildBattle;

namespace Game.Application;

/// <summary>Read-only projection of a committed source invocation, consumed after its attack text.
/// It adds no Domain event, command, RNG draw, saved field or authoritative acknowledgement.</summary>
public sealed record CampaignMoveAnimationDispatch(string BattleId, long Sequence, long AttackSequence,
    long PpSequence, long ResultSequence, int SourceMoveId, BattlerSlot Actor, BattlerSlot Target,
    bool HitsSubstitute);
public sealed record CampaignMoveAnimationDecision(CampaignMoveAnimationDispatch? Dispatch, string Reason);

public static class CampaignMoveAnimationPresentation
{
    public static CampaignMoveAnimationDecision AtAttackText(CampaignWildBattle battle, bool sourceRules,
        SourceBattleScene scene)
    {
        if (!sourceRules) return new(null, "historical-rules");
        if (scene != SourceBattleScene.On) return new(null, "animations-disabled");
        if (battle.Phase != CampaignBattlePhase.Events || battle.EventIndex < 0 || battle.EventIndex >= battle.Events.Length)
            return new(null, "not-attack-text");
        // Accepted basic hit/slice moves print through SpendInvocationPp. The paired AttackString is silent;
        // MoveUsed is the actual visible page. Callers such as Metronome have a different current move.
        if (battle.Events[battle.EventIndex] is not WildBattleMoveUsedEvent used) return new(null, "not-attack-text");
        int move = used.Move.Value switch { "pret:MOVE_POUND" => 1, "pret:MOVE_TACKLE" => 33, "pret:MOVE_SCRATCH" => 10, "pret:MOVE_CUT" => 15, "pret:MOVE_SLASH" => 163, "pret:MOVE_FALSE_SWIPE" => 206, "pret:MOVE_COUNTER" => 68, "pret:MOVE_VITAL_THROW" => 233, "pret:MOVE_TAIL_WHIP" => 39, "pret:MOVE_FLASH" => 148, "pret:MOVE_GROWTH" => 74, "pret:MOVE_HARDEN" => 106, "pret:MOVE_IRON_DEFENSE" => 334, _ => 0 };
        if (move == 0) return new(null, "script-outside-slice");
        if (battle.EventIndex == 0 || battle.Events[battle.EventIndex - 1] is not WildBattleAttackStringEvent attack ||
            attack.Actor != used.Actor || attack.CurrentMove != used.Move || attack.Turn != used.Turn)
            return new(null, "unmatched-source-attack-string");
        int end = battle.EventIndex + 1;
        while (end < battle.Events.Length && battle.Events[end].Turn == used.Turn &&
            battle.Events[end] is not (WildBattleAttackStringEvent or WildBattleMoveUsedEvent or WildBattleOrderEvent or WildBattleTurnReadyEvent)) end++;
        var action = battle.Events.Skip(battle.EventIndex + 1).Take(end - battle.EventIndex - 1).ToArray();
        var pp = action.OfType<WildBattlePpCommandEvent>().ToArray();
        if (pp.Length != 1 || pp[0].Actor != used.Actor || pp[0].Target != used.Target || pp[0].CurrentMove != used.Move)
            return new(null, "unmatched-source-pp-command");
        if (action.Any(e => e is WildBattleMoveMissedEvent m && m.Actor == used.Actor && m.Move == used.Move ||
            e is WildBattleMoveFailedEvent f && f.Actor == used.Actor && f.Move == used.Move ||
            e is WildBattleAbilityPreventedEvent a && a.Actor == used.Actor && a.Move == used.Move ||
            e is WildBattleEffectivenessEvent { Effectiveness: WildBattleEffectiveness.NoEffect } t && t.Actor == used.Actor && t.Move == used.Move))
            return new(null, "source-no-animation-result");
        if(move is 74 or 106 or 334)
        {
            var resultIndices=Enumerable.Range(battle.EventIndex+1,end-battle.EventIndex-1).Where(i=>battle.Events[i] is WildBattleStatChangedEvent).ToArray();
            if(resultIndices.Length!=1||CampaignStatAnimationPresentation.AtResult(battle with{EventIndex=resultIndices[0]},true) is not {} result||result.SourceMoveId!=move)
                return new(null,"unmatched-source-self-stat-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,result.Sequence,move,new((int)used.Actor),new((int)used.Target),false),"committed-source-animation");
        }
        if(move is 39 or 148)
        {
            var stat=move==39?WildBattleStat.Defense:WildBattleStat.Accuracy;
            var results=action.OfType<WildBattleStatChangedEvent>().Where(e=>e.Actor==used.Actor&&e.Target==used.Target&&e.Stat==stat&&e.After<e.Before).ToArray();
            if(results.Length!=1||results[0].Sequence<=pp[0].Sequence)return new(null,"unmatched-source-stat-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,results[0].Sequence,move,
                new((int)used.Actor),new((int)used.Target),false),"committed-source-animation");
        }
        var hits = action.Where(e => e is WildBattleHpChangedEvent h && h.Actor == used.Actor && h.Target == used.Target && h.Move == used.Move ||
            e is WildBattleSubstituteDamagedEvent s && s.Actor == used.Actor && s.Target == used.Target && s.Move == used.Move).ToArray();
        if (hits.Length != 1 || hits[0].Sequence <= pp[0].Sequence) return new(null, "unmatched-source-hit-result");
        // Damage and HP deltas are deliberately not eligibility inputs. Endure and substitute can
        // leave actual Pokémon HP unchanged while the original attack animation still executes.
        return new(new(battle.Id, used.Sequence, attack.Sequence, pp[0].Sequence, hits[0].Sequence, move,
            new((int)used.Actor), new((int)used.Target), hits[0] is WildBattleSubstituteDamagedEvent), "committed-source-animation");
    }
    public static CampaignMoveAnimationDecision AtAttackText(CampaignDoubleBattle battle,
        SourceBattleScene scene)
    {
        if (scene != SourceBattleScene.On) return new(null, "animations-disabled");
        if (battle.Stage != CampaignDoubleBattleStage.Combat || battle.EventIndex < 0 || battle.EventIndex >= battle.Events.Length)
            return new(null, "not-attack-text");
        // Accepted basic hit/slice moves print through SpendInvocationPp. The paired AttackString is silent;
        // MoveUsed is the actual visible page. Callers such as Metronome have a different current move.
        if (battle.Events[battle.EventIndex] is not SourceMoveUsedEvent used) return new(null, "not-attack-text");
        int move = used.Move.Value switch { "pret:MOVE_POUND" => 1, "pret:MOVE_TACKLE" => 33, "pret:MOVE_SCRATCH" => 10, "pret:MOVE_CUT" => 15, "pret:MOVE_SLASH" => 163, "pret:MOVE_FALSE_SWIPE" => 206, "pret:MOVE_COUNTER" => 68, "pret:MOVE_VITAL_THROW" => 233, "pret:MOVE_TAIL_WHIP" => 39, "pret:MOVE_FLASH" => 148, "pret:MOVE_GROWTH" => 74, "pret:MOVE_HARDEN" => 106, "pret:MOVE_IRON_DEFENSE" => 334, _ => 0 };
        if (move == 0) return new(null, "script-outside-slice");
        if (battle.EventIndex == 0 || battle.Events[battle.EventIndex - 1] is not SourceAttackStringEvent attack ||
            attack.Actor != used.Actor || attack.CurrentMove != used.Move || attack.Turn != used.Turn)
            return new(null, "unmatched-source-attack-string");
        int end = battle.EventIndex + 1;
        while (end < battle.Events.Length && battle.Events[end].Turn == used.Turn &&
            battle.Events[end] is not (SourceAttackStringEvent or SourceMoveUsedEvent or SourceOrderEvent or SourceTurnReadyEvent)) end++;
        var action = battle.Events.Skip(battle.EventIndex + 1).Take(end - battle.EventIndex - 1).ToArray();
        var pp = action.OfType<SourcePpCommandEvent>().ToArray();
        if(move==39)
        {
            // The original BOTH-target stat script revisits ppreduce without spending
            // again. One visible attack page can own two target passes. Select the
            // first successful committed result, including when the first target is capped.
            if(pp.Length is <1 or >2||pp[0].Target!=used.Target||pp.Select(p=>p.Target).Distinct().Count()!=pp.Length||
                pp.Any(p=>p.Actor!=used.Actor||p.CurrentMove!=used.Move||p.ChosenMove!=attack.ChosenMove||p.PpSlot!=used.Slot||p.Sequence<=used.Sequence||
                    (p.Target.Value&1)==(used.Actor.Value&1)||p.Target!=used.Target&&p.Target!=new BattlerSlot(used.Target.Value^2))||
                pp[0].Before!=used.PpBefore||pp[0].After!=used.PpAfter||pp.Skip(1).Any(p=>p.Before!=pp[0].After||p.After!=p.Before))
                return new(null,"unmatched-source-both-stat-pp");
            var results=action.OfType<SourceStatChangedEvent>().Where(e=>e.Actor==used.Actor&&e.Stat==WildBattleStat.Defense&&e.After<e.Before).ToArray();
            if(results.Length is <1 or >2||results.Select(r=>r.Target).Distinct().Count()!=results.Length||results.Any(r=>!pp.Any(p=>p.Target==r.Target&&p.Sequence<r.Sequence)))
                return new(null,"unmatched-source-both-stat-result");
            var result=results[0];var ownPp=pp.Single(p=>p.Target==result.Target);
            if(action.Any(e=>e is SourceMoveMissedEvent m&&m.Actor==used.Actor&&m.Target==result.Target&&m.Move==used.Move||
                e is SourceMoveFailedEvent f&&f.Actor==used.Actor&&f.Target==result.Target&&f.Move==used.Move||
                e is SourceAbilityPreventedEvent a&&a.Actor==used.Actor&&a.Target==result.Target&&a.Move==used.Move))
                return new(null,"source-no-animation-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,ownPp.Sequence,result.Sequence,move,used.Actor,result.Target,false),"committed-source-animation");
        }
        if (pp.Length != 1 || pp[0].Actor != used.Actor || pp[0].Target != used.Target || pp[0].CurrentMove != used.Move)
            return new(null, "unmatched-source-pp-command");
        if (action.Any(e => e is SourceMoveMissedEvent m && m.Actor == used.Actor && m.Move == used.Move ||
            e is SourceMoveFailedEvent f && f.Actor == used.Actor && f.Move == used.Move ||
            e is SourceAbilityPreventedEvent a && a.Actor == used.Actor && a.Move == used.Move ||
            e is SourceEffectivenessEvent { Effectiveness: WildBattleEffectiveness.NoEffect } t && t.Actor == used.Actor && t.Move == used.Move))
            return new(null, "source-no-animation-result");
        if(move is 74 or 106 or 334)
        {
            var resultIndices=Enumerable.Range(battle.EventIndex+1,end-battle.EventIndex-1).Where(i=>battle.Events[i] is SourceStatChangedEvent).ToArray();
            if(resultIndices.Length!=1||CampaignStatAnimationPresentation.AtResult(battle with{EventIndex=resultIndices[0]}) is not {} result||result.SourceMoveId!=move)
                return new(null,"unmatched-source-self-stat-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,result.Sequence,move,used.Actor,used.Target,false),"committed-source-animation");
        }
        if(move is 39 or 148)
        {
            var stat=move==39?WildBattleStat.Defense:WildBattleStat.Accuracy;
            var results=action.OfType<SourceStatChangedEvent>().Where(e=>e.Actor==used.Actor&&e.Target==used.Target&&e.Stat==stat&&e.After<e.Before).ToArray();
            if(results.Length!=1||results[0].Sequence<=pp[0].Sequence)return new(null,"unmatched-source-stat-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,results[0].Sequence,move,
                used.Actor,used.Target,false),"committed-source-animation");
        }
        var hits = action.Where(e => e is SourceHpChangedEvent h && h.Actor == used.Actor && h.Target == used.Target && h.Move == used.Move ||
            e is SourceSubstituteDamagedEvent s && s.Actor == used.Actor && s.Target == used.Target && s.Move == used.Move).ToArray();
        if (hits.Length != 1 || hits[0].Sequence <= pp[0].Sequence) return new(null, "unmatched-source-hit-result");
        // Damage and HP deltas are deliberately not eligibility inputs. Endure and substitute can
        // leave actual Pokémon HP unchanged while the original attack animation still executes.
        return new(new(battle.Id, used.Sequence, attack.Sequence, pp[0].Sequence, hits[0].Sequence, move,
            used.Actor, used.Target, hits[0] is SourceSubstituteDamagedEvent), "committed-source-animation");
    }
}
