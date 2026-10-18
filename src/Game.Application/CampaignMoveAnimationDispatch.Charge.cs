using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Application;

public static partial class CampaignMoveAnimationPresentation
{
    private static bool HasChargeAnimation(PokemonMoveDefinition definition)=>
        definition.Effect is "EFFECT_RAZOR_WIND" or "EFFECT_SKULL_BASH" or "EFFECT_SEMI_INVULNERABLE" or "EFFECT_SKY_ATTACK" or "EFFECT_SOLAR_BEAM";

    // The first attackanimation precedes the charging text and has no attack
    // string. Its PP command is the launch snapshot, not a fabricated MoveUsed.
    private static CampaignMoveAnimationDecision? ChargeSetup(string id,SourceBattleEvent[] rows,int index,int slots,
        IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves,Func<int,string,bool> supports)
    {
        if(index<0||index>=rows.Length||rows[index] is not SourceMtMoonVolatileEvent {Volatile:"called-charge",Reason:"move"} charge)return null;
        CampaignMoveAnimationDecision Deny()=>new(null,"unmatched-source-charge-setup");
        if(index==0||rows[index-1] is not SourcePpCommandEvent pp||Definition(pp.CurrentMove,moves) is not {} definition||
            !HasChargeAnimation(definition))return Deny();
        if(definition.Effect=="EFFECT_SOLAR_BEAM"&&(definition.Power<=0||definition.Target!="MOVE_TARGET_SELECTED"))return Deny();
        if(!supports(definition.SourceMoveId,definition.Id.Value))return new(null,"script-outside-slice");
        var context=CampaignAnimationInvocation.TryReadChargeSetup(rows,index,slots,moves);
        if(context is null)return Deny();
        int maxCost=definition.Target=="MOVE_TARGET_BOTH"?3:2;
        if(charge.Before!=0||charge.After!=1||charge.Side!=pp.Actor||charge.Turn!=pp.Turn||charge.Sequence<=pp.Sequence||
            pp.Actor.Value<0||pp.Actor.Value>=slots||pp.Target.Value<0||pp.Target.Value>=slots||pp.Actor==pp.Target||
            definition.Target=="MOVE_TARGET_BOTH"&&pp.Actor.Side==pp.Target.Side||
            pp.PpOwner!=context.PpOwner||pp.ChosenMove!=context.ChosenMove||pp.PpSlot!=context.PpSlot||pp.PpSlot is <0 or >3||pp.CommandCost<1||pp.CommandCost>maxCost||
            pp.Before is <0 or >255||pp.After is <0 or >255||
            (context.NoPpDeduct?pp.After!=pp.Before:pp.Before<1||pp.After!=Math.Max(0,pp.Before-pp.CommandCost)))return Deny();
        int first=context.IsCalled?context.ChainStartIndex-1:index-2;
        while(first>=0&&rows[first].Turn==charge.Turn&&!InvocationBoundary(rows[first]))
        {
            if(rows[first] is SourcePpCommandEvent foreign&&foreign.Actor!=pp.Actor)break;
            if(rows[first] is SourceCalledMoveTransitionEvent or SourceCallerSelectionEvent or SourcePpCommandEvent||
                rows[first] is SourceMtMoonVolatileEvent {Volatile:"called-charge"} previous&&previous.Side==charge.Side)return Deny();
            first--;
        }
        if(first>=0&&rows[first].Turn==charge.Turn&&(rows[first] is SourceAttackStringEvent a&&a.Actor==pp.Actor||rows[first] is SourceMoveUsedEvent u&&u.Actor==pp.Actor))return Deny();
        int end=index+1;
        while(end<rows.Length&&rows[end].Turn==charge.Turn&&!InvocationBoundary(rows[end]))
        {
            if(rows[end] is SourcePpCommandEvent next&&next.Actor!=pp.Actor)break;
            if(rows[end].Sequence<=rows[end-1].Sequence||rows[end] is SourceCalledMoveTransitionEvent or SourceCallerSelectionEvent or SourcePpCommandEvent||
                rows[end] is SourceMtMoonVolatileEvent {Volatile:"called-charge"} duplicate&&duplicate.Side==charge.Side)return Deny();
            end++;
        }
        return new(new(id,charge.Sequence,null,pp.Sequence,charge.Sequence,definition.SourceMoveId,pp.Actor,pp.Target,false)
            {MoveTurn=0,Boundary=CampaignMoveAnimationBoundary.BeforeChargeResult},"committed-source-charge-setup");
    }

    private static bool AuthenticatedChargeRelease(Invocation battle,SourceMoveUsedEvent used,PokemonMoveDefinition definition,SourceBattleEvent[] action)
    {
        int marker=battle.EventIndex-2;
        if(marker<0||battle.Events[marker] is not SourceMtMoonVolatileEvent {Volatile:"called-charge",Before:1,After:0,Reason:"released"} released||
            released.Side!=used.Actor||released.Turn!=used.Turn||released.Sequence>=battle.Events[marker+1].Sequence)return false;
        if(definition.Effect=="EFFECT_SOLAR_BEAM"&&!SolarChargePrefix(battle,used,definition,action,ref marker))return false;
        if(battle.Context?.IsCalled==true)
            return definition.Effect=="EFFECT_SOLAR_BEAM"&&battle.Context.EarlyLeafPpIndex is not null&&
                battle.Context.PrefixStartIndex==marker;
        for(int i=marker-1;i>=0&&battle.Events[i].Turn==used.Turn&&!InvocationBoundary(battle.Events[i]);i--)
        {
            if(battle.Events[i] is SourcePpCommandEvent foreign&&foreign.Actor!=used.Actor)break;
            if(battle.Events[i] is SourceCalledMoveTransitionEvent or SourceCallerSelectionEvent or SourcePpCommandEvent||
                battle.Events[i] is SourceMtMoonVolatileEvent {Volatile:"called-charge"} prior&&prior.Side==used.Actor)return false;
        }
        return true;
    }
    internal static CampaignStatAnimationDispatch? ChargeStatResult(string id,SourceBattleEvent[] rows,int index,int slots,
        IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves,bool substitute)
    {
        if(substitute||index<2||index>=rows.Length||rows[index] is not SourceStatChangedEvent {Stat:Game.Domain.WildBattle.WildBattleStat.Defense} stat||
            stat.Actor!=stat.Target||stat.Before is <0 or >=12||stat.After!=stat.Before+1||rows[index-2] is not SourcePpCommandEvent pp||
            Definition(pp.CurrentMove,moves)?.Effect!="EFFECT_SKULL_BASH")return null;
        var setup=ChargeSetup(id,rows,index-1,slots,moves,(_,_)=>true)?.Dispatch;
        if(setup is null||setup.Actor!=stat.Actor||stat.Turn!=rows[index-1].Turn||stat.Sequence<=rows[index-1].Sequence)return null;
        return new(id,stat.Sequence,null,pp.Sequence,setup.SourceMoveId,stat.Actor,16);
    }

}
