using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Application;

public static partial class CampaignMoveAnimationPresentation
{
    // Beat Up's script loops over eligible party members, not MULTIHIT_RESULT.
    // A skipped party member does not advance the animation's per-hit ordinal.
    private static CampaignMoveAnimationDecision? BeatUpResult(string id,SourceBattleEvent[] rows,int index,int slots,
        IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves,Func<int,string,bool> supports)
    {
        if(index<0||index>=rows.Length||CommittedHit(rows[index],slots) is not {} current||
            Definition(current.Move,moves) is not {Effect:"EFFECT_BEAT_UP"} definition)return null;
        CampaignMoveAnimationDecision Deny()=>new(null,"unmatched-source-contributor-hit");
        if(!supports(definition.SourceMoveId,definition.Id.Value))return new(null,"script-outside-slice");
        if(definition.Target!="MOVE_TARGET_SELECTED"||definition.Power<=0)return Deny();
        int start=index-1;
        while(start>=0&&rows[start].Turn==rows[index].Turn&&!InvocationBoundary(rows[start]))start--;
        var context=CampaignAnimationInvocation.TryRead(rows,start,slots,moves);
        if(context is null)return Deny();
        if(start<1||rows[start] is not SourceMoveUsedEvent used||rows[start-1] is not SourceAttackStringEvent attack||
            used.Actor!=current.Actor||used.Target!=current.Target||used.Move!=current.Move||
            used.Actor.Value>=slots||used.Target.Value>=slots||used.Actor==used.Target||
            attack.Actor!=used.Actor||attack.CurrentMove!=used.Move||attack.ChosenMove!=context.ChosenMove||
            attack.Turn!=used.Turn||used.Turn!=rows[index].Turn||attack.Sequence>=used.Sequence)return Deny();
        int end=start+1;
        while(end<rows.Length&&rows[end].Turn==used.Turn&&!InvocationBoundary(rows[end])&&
            !(rows[end] is SourcePpCommandEvent foreign&&foreign.Actor!=used.Actor))end++;
        if(index>=end)return Deny();
        for(int i=start+1;i<end;i++)if(rows[i].Sequence<=rows[i-1].Sequence)return Deny();
        var action=rows[(start+1)..end];var pp=action.OfType<SourcePpCommandEvent>().ToArray();
        if(action.Length<3||!ValidSingleInvocationPp(pp,used,2,invocation:context)||action[0]!=pp[0]||
            action[1] is not SourceCalledBeatUpMemberEvent||FailedHit(action,used.Actor,used.Target,used.Move)||
            action.Any(e=>e is SourceCalledMoveTransitionEvent or SourceCallerSelectionEvent or SourceMultiHitEvent))return Deny();
        bool pending=false,knockedOut=false;int lastParty=-1,ordinal=0,selected=-1;
        foreach(var row in action.Skip(1))
        {
            if(row is SourceCalledBeatUpMemberEvent member)
            {
                if(pending||knockedOut||member.Side!=used.Actor.Side||member.PartyIndex is <0 or >5||
                    member.PartyIndex<=lastParty||string.IsNullOrWhiteSpace(member.Species.Value))return Deny();
                lastParty=member.PartyIndex;pending=true;
            }
            else if(row is SourceHpChangedEvent or SourceSubstituteDamagedEvent)
            {
                if(!pending||CommittedHit(row,slots) is not {} hit||hit.Actor!=used.Actor||hit.Target!=used.Target||hit.Move!=used.Move)return Deny();
                if(row.Sequence==rows[index].Sequence)selected=ordinal;
                ordinal++;pending=false;
                if(row is SourceHpChangedEvent {HpAfter:0})knockedOut=true;
            }
        }
        if(pending||ordinal is <1 or >6||selected<0)return Deny();
        return new(new(id,rows[index].Sequence,attack.Sequence,pp[0].Sequence,rows[index].Sequence,definition.SourceMoveId,
            used.Actor,used.Target,current.Substitute){MoveTurn=checked((byte)selected),Boundary=CampaignMoveAnimationBoundary.BeforeHitResult},
            "committed-source-contributor-animation");
    }
}
