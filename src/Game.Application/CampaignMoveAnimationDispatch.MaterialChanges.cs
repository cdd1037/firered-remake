using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
namespace Game.Application;

public sealed record CampaignBattlerMaterialChangeRequest(BattlerSlot Affected,long ResultSequence,
    WildBattleCombatant BeforeBody,WildBattleCombatant AfterBody);

public static partial class CampaignMoveAnimationPresentation
{
    private static CampaignMoveAnimationDecision MaterialChangeAnimation(Invocation battle,
        SourceMoveUsedEvent used,PokemonMoveDefinition definition)
    {
        CampaignMoveAnimationDecision No(string reason)=>new(null,reason);
        int at=battle.EventIndex;var rows=battle.Events;
        bool substitute=definition.Effect=="EFFECT_SUBSTITUTE";
        if(definition.Effect is not("EFFECT_TRANSFORM" or "EFFECT_SUBSTITUTE")||definition.Power!=0||
            definition.Target!=(substitute?"MOVE_TARGET_USER":"MOVE_TARGET_SELECTED")||
            used.Actor.Value>=battle.Slots||used.Target.Value>=battle.Slots||used.Slot is <0 or >3||
            (substitute?used.Target!=used.Actor:used.Target==used.Actor)||at<1||
            rows[at-1] is not SourceAttackStringEvent attack||attack.Actor!=used.Actor||attack.Turn!=used.Turn||
            attack.ChosenMove!=used.Move||attack.CurrentMove!=used.Move||attack.Sequence+1!=used.Sequence)
            return No("unmatched-source-material-invocation");
        int end=at+1;
        while(end<rows.Length&&rows[end].Turn==used.Turn&&
            rows[end] is not(SourceAttackStringEvent or SourceMoveUsedEvent or SourceOrderEvent or SourceTurnReadyEvent)&&
            !(rows[end] is SourcePpCommandEvent next&&next.Actor!=used.Actor))end++;
        int first=substitute?at-2:at-1;
        if(first<0)return No("unmatched-source-material-pp");
        for(int i=first+1;i<end;i++)if(rows[i].Sequence<=rows[i-1].Sequence)return No("unmatched-source-material-order");
        var action=rows[(at+1)..end];
        if(action.Any(e=>e is SourceMoveFailedEvent or SourceMoveMissedEvent or SourceAbilityPreventedEvent))
            return No("source-no-animation-result");
        if(!substitute)
        {
            var pp=action.OfType<SourcePpCommandEvent>().ToArray();
            if(!ValidSingleInvocationPp(pp,used,2)||action.Length<2||action[0]!=pp[0]||
                action[1] is not SourceTransformedEvent changed||changed.Actor!=used.Actor||changed.Target!=used.Target||
                changed.Turn!=used.Turn||changed.Sequence!=pp[0].Sequence+1||
                action.OfType<SourceTransformedEvent>().Count()!=1||changed.BattleView is null||
                changed.BattleView.Species!=changed.AppearanceSpecies||changed.BattleView.SourceSpeciesId<=0)
                return No("unmatched-source-transform-result");
            return new(new(battle.Id,used.Sequence,attack.Sequence,pp[0].Sequence,changed.Sequence,
                definition.SourceMoveId,used.Actor,used.Target,false),"committed-source-animation");
        }
        // Substitute's source script is PP -> attack string -> animation -> HP.
        // Its later volatile receipt is the material handoff, not a second hit.
        if(rows[at-2] is not SourcePpCommandEvent prefix||prefix.Turn!=used.Turn||
            prefix.Actor!=used.Actor||prefix.PpOwner!=used.Actor||prefix.Target!=used.Actor||
            prefix.PpSlot!=used.Slot||prefix.ChosenMove!=used.Move||prefix.CurrentMove!=used.Move||
            prefix.CommandCost!=1||prefix.Before is <1 or >255||prefix.After!=Math.Max(0,prefix.Before-1)||
            prefix.Before!=used.PpBefore||prefix.After!=used.PpAfter||prefix.Sequence+1!=attack.Sequence||
            action.OfType<SourcePpCommandEvent>().Any()||action.Length<2||
            action[0] is not SourceHpChangedEvent {Critical:false,RandomPercent:100} cost||
            cost.Actor!=used.Actor||cost.Target!=used.Actor||cost.Move!=used.Move||cost.Turn!=used.Turn||
            cost.Sequence!=used.Sequence+1||cost.Damage is <1 or >255||cost.HpBefore<=cost.Damage||cost.HpAfter!=cost.HpBefore-cost.Damage||
            action[1] is not SourceMtMoonVolatileEvent {Volatile:"substitute",Before:0,Reason:"move"} marker||
            marker.Side!=used.Actor||marker.After!=cost.Damage||marker.Turn!=used.Turn||marker.Sequence!=cost.Sequence+1||
            action.OfType<SourceMtMoonVolatileEvent>().Count(e=>e.Volatile=="substitute"&&e.Reason=="move")!=1||
            action.OfType<SourceHpChangedEvent>().Count(e=>e.Actor==used.Actor&&e.Move==used.Move)!=1)
            return No("unmatched-source-substitute-result");
        return new(new(battle.Id,used.Sequence,attack.Sequence,prefix.Sequence,marker.Sequence,
            definition.SourceMoveId,used.Actor,used.Actor,false),"committed-source-animation");
    }

    /// <summary>Detach pre/post material bodies from the existing forward-only display
    /// reducer. nextEventIndex is its real cursor: the current MoveUsed is already
    /// applied in singles and remains pending in doubles. No final frame is accepted.</summary>
    public static CampaignBattlerMaterialChangeRequest? MaterialChangeAtInvocation(
        CampaignMoveAnimationDispatch dispatch,CampaignBattleDisplayBodies displayed,
        IReadOnlyList<SourceBattleEvent> events,int nextEventIndex,
        IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves)
    {
        int count=displayed.Bodies.Length,actor=dispatch.Actor.Value,target=dispatch.Target.Value;
        if(count is not(2 or 4)||displayed.Members.Length!=count||actor>=count||target>=count||
            displayed.Members.Where((m,i)=>m.Side!=new BattlerSlot(i).Side).Any()||
            dispatch.Boundary!=CampaignMoveAnimationBoundary.AfterAttackText)return null;
        int at=-1,last=-1;
        for(int i=0;i<events.Count;i++)
        {
            if(i>0&&events[i].Sequence<=events[i-1].Sequence)return null;
            if(events[i].Sequence==dispatch.Sequence)at=i;
            if(events[i].Sequence==dispatch.ResultSequence)last=i;
        }
        if(at<0||last<=at||nextEventIndex!=at&&nextEventIndex!=at+1||
            events[at] is not SourceMoveUsedEvent used||Definition(used.Move,moves) is not {} definition||
            definition.Effect is not("EFFECT_TRANSFORM" or "EFFECT_SUBSTITUTE"))return null;
        var owned=MaterialChangeAnimation(new(dispatch.BattleId,events.ToArray(),at,count,()=>null,_=>null),used,definition).Dispatch;
        if(owned!=dispatch)return null;
        var before=displayed.Bodies[actor];
        if(before.Pokemon.CurrentHp<=0||before.CalledVolatile?.SubstituteHp>0)return null;
        if(events[last] is SourceTransformedEvent transformed)
        {
            var donor=displayed.Bodies[target];var original=WildBattleTransformationRules.PersistentPokemon(before);
            if(donor.Transform is not null||transformed.OriginalSpecies!=original.Species||
                transformed.AppearanceSpecies!=donor.Pokemon.Species||transformed.AppearancePersonality!=donor.Pokemon.Personality||
                transformed.AppearanceOriginalTrainerId!=donor.Pokemon.OriginalTrainerId||
                transformed.BattleView.SourceSpeciesId!=donor.Pokemon.SourceSpeciesId||
                transformed.BattleView.Personality!=before.Pokemon.Personality||transformed.BattleView.OriginalTrainerId!=before.Pokemon.OriginalTrainerId||
                transformed.BattleView.CurrentHp!=before.Pokemon.CurrentHp)return null;
        }
        else if(events[last-1] is not SourceHpChangedEvent cost||cost.HpBefore!=before.Pokemon.CurrentHp)return null;
        var after=displayed;
        for(int i=nextEventIndex;i<=last;i++)after=after.Apply(events[i]);
        return new(dispatch.Actor,dispatch.ResultSequence,before,after.Bodies[actor]);
    }
}
