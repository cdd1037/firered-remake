using Game.Domain.Battle;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Application;

public static partial class CampaignMoveAnimationPresentation
{
    private static CampaignMoveAnimationDecision CureAnimation(Invocation battle,SourceMoveUsedEvent used,
        SourceAttackStringEvent attack,PokemonMoveDefinition definition,SourceBattleEvent[] action,SourcePpCommandEvent[] commands)
    {
        CampaignMoveAnimationDecision Reject(string reason)=>new(null,reason);
        if(definition.Power!=0||definition.Target!="MOVE_TARGET_USER"||used.Actor.Value<0||used.Actor.Value>=battle.Slots||
            used.Target!=used.Actor||!ValidSingleInvocationPp(commands,used,1)||action.Length==0||action[0]!=commands[0]||
            commands[0].Sequence!=used.Sequence+1||
            action.Zip(action.Skip(1)).Any(pair=>pair.Second.Sequence!=pair.First.Sequence+1)||
            action.Any(e=>e is SourceMoveFailedEvent or SourceMoveMissedEvent or SourceActionBlockedEvent or
                SourceCalledMoveTransitionEvent or SourceCallerSelectionEvent or SourceHpChangedEvent or
                SourceSubstituteDamagedEvent or SourceRecoveredEvent or SourceForcedOutEvent or SourceMoveCopiedEvent or
                SourceTransformedEvent or SourceCalledDelayedEvent or SourceCalledOverlayChangedEvent||
                e is SourceEffectivenessEvent {Effectiveness:WildBattleEffectiveness.NoEffect}))
            return Reject("unmatched-source-cure-invocation");
        int following=battle.EventIndex+1+action.Length;
        if(following<battle.Events.Length&&battle.Events[following] is not
            (SourceAttackStringEvent or SourceMoveUsedEvent or SourceOrderEvent or SourceTurnReadyEvent or SourcePpCommandEvent)&&
            battle.Events[following].Turn!=used.Turn)
            return Reject("unmatched-source-cure-turn-boundary");
        var pp=commands[0];
        if(definition.Effect=="EFFECT_REFRESH")
        {
            // cureifburnedparalysedorpoisoned branches to failure before
            // attackanimation. Only its actual successful status write owns Refresh.
            if(action.Length<2||action[1] is not SourceStatusChangedEvent {After:0,Reason:"refresh"} result||
                result.Side!=used.Actor||!Refreshable(result.Before)||
                action.OfType<SourceStatusChangedEvent>().Count(e=>e.Reason=="refresh")!=1||
                action.OfType<SourceAbilityPreventedEvent>().Any()||!CureMoveEndTail(action,2,used,battle.Slots))
                return Reject("unmatched-source-refresh-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp.Sequence,result.Sequence,
                definition.SourceMoveId,used.Actor,used.Actor,false),"committed-source-animation");
        }
        // BattleScript_EffectHealBell reaches attackanimation unconditionally
        // after ppreduce/healpartystatus, even with zero status changes or when
        // Soundproof blocks every present party member. This command-owned root
        // uses the genuine PP receipt; it must never invent a successful cure.
        bool bell=used.Move.Value=="pret:MOVE_HEAL_BELL";
        string reason=bell?"heal-bell":"aromatherapy";
        int lastParty=-1;
        var cured=new HashSet<BattlerSlot>();var blocked=new HashSet<BattlerSlot>();
        for(int i=1;i<action.Length;i++)
        {
            switch(action[i])
            {
                case SourceCalledPartyChangedEvent party when party.Reason is "heal-bell" or "aromatherapy":
                    if(party.Reason!=reason||party.Side!=used.Actor.Side||party.PartyIndex<=lastParty||party.PartyIndex>=6||
                        party.Before is null||party.After is null||!CureStatus(party.Before.Status)||
                        party.After.Status!=0||bell&&party.Before.IsEgg||!OnlyPartyStatusCleared(party.Before,party.After))
                        return Reject("unmatched-source-party-cure");
                    lastParty=party.PartyIndex;break;
                case SourceTowerVolatileEvent nightmare when nightmare.Reason is "heal-bell" or "aromatherapy":
                    if(nightmare.Reason!=reason||nightmare.Effect!="nightmare"||nightmare.Applied||
                        !OwnActive(nightmare.Side)||i+1>=action.Length||
                        action[i+1] is not SourceStatusChangedEvent clear||clear.Side!=nightmare.Side||
                        clear.Reason!=reason||!WildBattleStatus.IsSleeping(clear.Before)||clear.After!=0)
                        return Reject("unmatched-source-party-nightmare");
                    break;
                case SourceStatusChangedEvent status when status.Reason is "heal-bell" or "aromatherapy":
                    if(status.Reason!=reason||!OwnActive(status.Side)||!CureStatus(status.Before)||status.After!=0||
                        !cured.Add(status.Side)||blocked.Contains(status.Side))return Reject("unmatched-source-party-status");
                    break;
                case SourceAbilityPreventedEvent prevention:
                    if(!bell||prevention.Actor!=used.Actor||prevention.Move!=used.Move||
                        prevention.Ability!="ABILITY_SOUNDPROOF"||!OwnActive(prevention.Target)||
                        !blocked.Add(prevention.Target)||cured.Contains(prevention.Target))
                        return Reject("unmatched-source-party-soundproof");
                    break;
                default:
                    if(!CureMoveEndTail(action,i,used,battle.Slots))return Reject("unmatched-source-cure-move-end");
                    i=action.Length;break;
            }
        }
        return new(new(battle.Id,used.Sequence,attack.Sequence,pp.Sequence,pp.Sequence,
            definition.SourceMoveId,used.Actor,used.Actor,false),"committed-source-party-cure-animation");

        bool OwnActive(BattlerSlot target)=>target.Value>=0&&target.Value<battle.Slots&&
            (target==used.Actor||battle.Slots==4&&target==used.Actor.Partner);
    }

    private static bool Refreshable(uint status)=>status is WildBattleStatus.Poison or WildBattleStatus.Burn or WildBattleStatus.Paralysis||
        WildBattleSourceFieldEffects.IsValidToxic(status);
    private static bool CureStatus(uint status)=>Refreshable(status)||status==WildBattleStatus.Freeze||WildBattleStatus.IsSleeping(status);
    private static bool OnlyPartyStatusCleared(WildBattleCalledPartyMember before,WildBattleCalledPartyMember after)=>
        before.Pokemon is not null&&after.Pokemon is not null&&!before.Pokemon.Moves.IsDefault&&!after.Pokemon.Moves.IsDefault&&
        before with{Status=0,Pokemon=after.Pokemon}==after&&
        before.Pokemon with{Moves=[]}==after.Pokemon with{Moves=[]}&&before.Pokemon.Moves.SequenceEqual(after.Pokemon.Moves);
}
