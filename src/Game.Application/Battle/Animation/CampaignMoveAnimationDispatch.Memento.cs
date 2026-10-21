using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
namespace Game.Application;

public static partial class CampaignMoveAnimationPresentation
{
    private static CampaignMoveAnimationDecision MementoAnimation(Invocation battle,SourceMoveUsedEvent used,
        SourceAttackStringEvent attack,PokemonMoveDefinition definition,SourceBattleEvent[] action,SourcePpCommandEvent[] commands)
    {
        CampaignMoveAnimationDecision Reject(string reason)=>new(null,reason);
        if(definition.Power!=0||definition.Target!="MOVE_TARGET_SELECTED"||used.Actor==used.Target||
            used.Actor.Value<0||used.Actor.Value>=battle.Slots||used.Target.Value<0||used.Target.Value>=battle.Slots||
            !ValidSingleInvocationPp(commands,used,2,invocation:battle.Context)||attack.Sequence+1!=used.Sequence||
            action.Length<4||action[0]!=commands[0]||commands[0].Sequence!=used.Sequence+1||
            action.Any(e=>e.Turn!=used.Turn)||action.Zip(action.Skip(1)).Any(p=>p.Second.Sequence!=p.First.Sequence+1)||
            action[1] is not SourceHpChangedEvent cost||cost.Actor!=used.Actor||cost.Target!=used.Actor||cost.Move!=used.Move||
            cost.HpBefore<1||cost.HpAfter!=0||cost.Damage!=cost.HpBefore||cost.Critical||cost.RandomPercent!=100||
            cost.PreparedBasePower is not null||cost.PreparedAnimationPower is not null||cost.PreparedAnimationWeather is not null||cost.PreparedFuryCutterCounter is not null)
            return Reject("unmatched-source-memento-cost");
        int tail=2;
        bool Failure(SourceBattleEvent e,string why)=>e is SourceMoveFailedEvent f&&f.Actor==used.Actor&&f.Target==used.Target&&f.Move==used.Move&&f.Reason==why;
        bool Prevention(SourceBattleEvent e,params string[] abilities)=>e is SourceAbilityPreventedEvent a&&a.Actor==used.Actor&&a.Target==used.Target&&a.Move==used.Move&&abilities.Contains(a.Ability);
        bool Stat(SourceBattleEvent e,WildBattleStat stat)=>e switch{
            SourceStatChangedEvent change=>change.Actor==used.Actor&&change.Target==used.Target&&change.Stat==stat&&change.Before is >0 and <=12&&change.After==Math.Max(0,change.Before-2),
            SourceStatAtMinimumEvent cap=>cap.Actor==used.Actor&&cap.Target==used.Target&&cap.Stat==stat,_=>false};
        // Protected target takes the separate source branch before attackanimation.
        // Substitute is tested only afterward, so its failure still owns the root.
        if(Failure(action[tail],"protected"))return Reject("source-no-animation-result");
        if(Failure(action[tail],"substitute")||Failure(action[tail],"mist")||Prevention(action[tail],"ABILITY_CLEAR_BODY","ABILITY_WHITE_SMOKE"))tail++;
        else
        {
            if(tail+1>=action.Length||!(Stat(action[tail],WildBattleStat.Attack)||Prevention(action[tail],"ABILITY_HYPER_CUTTER"))||
                !Stat(action[tail+1],WildBattleStat.SpecialAttack)||action[tail] is SourceStatAtMinimumEvent&&action[tail+1] is SourceStatAtMinimumEvent)
                return Reject("unmatched-source-memento-stat-order");
            tail+=2;
        }
        if(!SelfCostTail(action,tail,used,battle.Slots,commands,new(used.Actor,cost.HpBefore,0,cost.Damage),
            maximum=>cost.HpBefore<=maximum,battle.Context))return Reject("unmatched-source-memento-faint-tail");
        return new(new(battle.Id,used.Sequence,attack.Sequence,commands[0].Sequence,cost.Sequence,
            definition.SourceMoveId,used.Actor,used.Target,false),"committed-source-animation");
    }
    // Stat waves reuse the entire authenticated root, including cost/party/faint
    // order. Group matching never admits a bare HP or stat row on its own.
    internal static bool AuthenticatedMementoGroup(string id,SourceBattleEvent[] events,int usedIndex,int slots,
        IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves)
    {
        if(usedIndex<0||usedIndex>=events.Length||events[usedIndex] is not SourceMoveUsedEvent used||Definition(used.Move,moves)?.Effect!="EFFECT_MEMENTO")return false;
        return AtAttackText(new(id,events,usedIndex,slots,()=>null,_=>null),moves,(_,_)=>true).Dispatch is not null;
    }
}
