using Game.Domain.Battle;
using Game.Domain.WildBattle;
namespace Game.Application;

public static partial class CampaignStatAnimationPresentation
{
    private sealed record StatGroup(string Battle,long Attack,long Pp,int Move,int Affected,long First,long? Animation,int? Argument,long[] Silent);
    private sealed record GroupRow(long Sequence,int Turn,string Kind,int Actor=-1,int Target=-1,string Move="",string Chosen="",
        int Owner=-1,int Slot=-1,int Before=0,int After=0,int Cost=0,WildBattleStat Stat=default,string Detail="",int Damage=0,bool Critical=false,bool Endured=false,bool HungOn=false,int Percent=100);
    private static CampaignStatAnimationDispatch? GroupAnimation(StatGroup group,long current,bool substitute)=>
        !substitute&&group.Animation==current&&group.Argument is {} arg
            ?new(group.Battle,current,group.Attack,group.Pp,group.Move,new(group.Affected),arg):null;

    // Dedicated roots, generic stat waves and cap text use one validated source
    // result prefix. A later typed held-item activation remains a separate group.
    public static long? GroupedMoveResultSequence(CampaignDoubleBattle battle)=>Grouped(battle)?.First;
    public static long? GroupedMoveResultSequence(CampaignWildBattle battle,bool sourceRules)=>Grouped(battle,sourceRules)?.First;
    public static bool IsSilentResultReceipt(CampaignDoubleBattle battle)=>
        Grouped(battle) is {} group&&group.Silent.Contains(battle.Events[battle.EventIndex].Sequence);
    public static bool IsSilentResultReceipt(CampaignWildBattle battle,bool sourceRules)=>
        Grouped(battle,sourceRules) is {} group&&group.Silent.Contains(battle.Events[battle.EventIndex].Sequence);

    private static StatGroup? Grouped(CampaignDoubleBattle battle)
    {
        if(battle.Stage!=CampaignDoubleBattleStage.Combat||battle.EventIndex<0||battle.EventIndex>=battle.Events.Length)return null;
        int invocation=battle.EventIndex;
        while(invocation>=0&&battle.Events[invocation] is not (SourceMoveUsedEvent or SourceAttackStringEvent))invocation--;
        if(invocation<0||battle.Events[invocation] is not SourceMoveUsedEvent {Move.Value:"pret:MOVE_COSMIC_POWER" or "pret:MOVE_BULK_UP" or "pret:MOVE_TICKLE" or "pret:MOVE_SWAGGER"})return null;
        var itemReceipts=ItemReceiptSequences(battle.Events);
        return ReadGroup(battle.Id,battle.Events.Select(GroupRead).ToArray(),battle.EventIndex,4,itemReceipts);
    }
    private static StatGroup? Grouped(CampaignWildBattle battle,bool sourceRules)
    {
        if(!sourceRules||battle.Phase!=CampaignBattlePhase.Events||battle.EventIndex<0||battle.EventIndex>=battle.Events.Length)return null;
        int invocation=battle.EventIndex;
        while(invocation>=0&&battle.Events[invocation] is not (WildBattleMoveUsedEvent or WildBattleAttackStringEvent))invocation--;
        if(invocation<0||battle.Events[invocation] is not WildBattleMoveUsedEvent {Move.Value:"pret:MOVE_COSMIC_POWER" or "pret:MOVE_BULK_UP" or "pret:MOVE_TICKLE" or "pret:MOVE_SWAGGER"})return null;
        var itemReceipts=ItemReceiptSequences(battle.Events);
        return ReadGroup(battle.Id,battle.Events.Select(GroupRead).ToArray(),battle.EventIndex,2,itemReceipts);
    }
    private static HashSet<long> ItemReceiptSequences(IReadOnlyList<SourceBattleEvent> events)
    {
        var result=new HashSet<long>();
        for(int end=0;end<events.Count;end++)if(events[end] is SourceHeldItemConsumedEvent)
        {int start=CampaignHeldItemAnimationPresentation.ConsumedGroupStart(events,end);if(start>=0)for(int i=start;i<=end;i++)result.Add(events[i].Sequence);}
        return result;
    }
    private static HashSet<long> ItemReceiptSequences(IReadOnlyList<WildBattleEvent> events)
    {
        var result=new HashSet<long>();
        for(int end=0;end<events.Count;end++)if(events[end] is WildBattleHeldItemConsumedEvent)
        {int start=CampaignHeldItemAnimationPresentation.ConsumedGroupStart(events,end);if(start>=0)for(int i=start;i<=end;i++)result.Add(events[i].Sequence);}
        return result;
    }
    private static bool GroupBoundary(GroupRow row)=>row.Kind is "attack" or "used" or "boundary";
    private static StatGroup? ReadGroup(string battle,GroupRow[] rows,int index,int slots,HashSet<long> items)
    {
        int usedIndex=index;
        while(usedIndex>=0&&!GroupBoundary(rows[usedIndex]))usedIndex--;
        if(usedIndex<1||rows[usedIndex].Kind!="used")return null;
        var used=rows[usedIndex];var attack=rows[usedIndex-1];
        int move=used.Move switch{"pret:MOVE_COSMIC_POWER"=>322,"pret:MOVE_BULK_UP"=>339,"pret:MOVE_TICKLE"=>321,"pret:MOVE_SWAGGER"=>207,_=>0};
        if(move==0||attack.Kind!="attack"||attack.Actor!=used.Actor||attack.Move!=used.Move||attack.Chosen!=used.Move||attack.Turn!=used.Turn||
            used.Actor<0||used.Actor>=slots||used.Target<0||used.Target>=slots||used.Slot is <0 or >3||
            (move is 322 or 339?used.Actor!=used.Target:used.Actor==used.Target))return null;
        int end=usedIndex+1;
        while(end<rows.Length&&rows[end].Turn==used.Turn&&!GroupBoundary(rows[end]))end++;
        if(index>=end||end-usedIndex<3)return null;
        for(int i=usedIndex;i<end;i++)if(rows[i].Sequence<=rows[i-1].Sequence)return null;
        var pp=rows[usedIndex+1];
        if(pp.Kind!="pp"||pp.Actor!=used.Actor||pp.Owner!=used.Actor||pp.Target!=used.Target||pp.Move!=used.Move||pp.Chosen!=used.Move||
            pp.Slot!=used.Slot||pp.Before!=used.Before||pp.After!=used.After||pp.Before<1||pp.Cost<1||pp.After!=Math.Max(0,pp.Before-pp.Cost))return null;
        int first=usedIndex+2;var a=rows[first];long? animation=null;int? argument=null;int count=2;
        bool Owner(GroupRow r)=>r.Actor==used.Actor&&r.Target==used.Target;
        bool Changed(GroupRow r,WildBattleStat stat,int delta)=>Owner(r)&&r.Kind=="stat"&&r.Stat==stat&&r.Before is >=0 and <=12&&r.After!=r.Before&&r.After==Math.Clamp(r.Before+delta,0,12);
        bool Cap(GroupRow r,WildBattleStat stat,string kind)=>Owner(r)&&r.Kind==kind&&r.Stat==stat;
        bool Protection(GroupRow r,string kind,params string[] reasons)=>Owner(r)&&r.Kind==kind&&r.Move==used.Move&&reasons.Contains(r.Detail);
        if(move==321&&(Protection(a,"failed","mist")||Protection(a,"ability","ABILITY_CLEAR_BODY","ABILITY_WHITE_SMOKE")))count=1;
        else
        {
            if(first+1>=end)return null;var b=rows[first+1];
            switch(move)
            {
                case 322:case 339:
                    var firstStat=move==339?WildBattleStat.Attack:WildBattleStat.Defense;
                    var secondStat=move==339?WildBattleStat.Defense:WildBattleStat.SpecialDefense;
                    bool upFirst=Changed(a,firstStat,1),upSecond=Changed(b,secondStat,1);
                    if(!(upFirst||Cap(a,firstStat,"maximum"))||!(upSecond||Cap(b,secondStat,"maximum"))||!(upFirst||upSecond))return null;
                    // Both source scripts choose the wave before visiting either
                    // stat command, including an individually capped first stat.
                    animation=a.Sequence;argument=upFirst&&upSecond?55:AnimationArgument(upFirst?firstStat:secondStat,1);break;
                case 321:
                    bool downA=Changed(a,WildBattleStat.Attack,-1),downD=Changed(b,WildBattleStat.Defense,-1);
                    bool hyper=Protection(a,"ability","ABILITY_HYPER_CUTTER");
                    if(!(downA||Cap(a,WildBattleStat.Attack,"minimum")||hyper)||!(downD||Cap(b,WildBattleStat.Defense,"minimum"))||
                        !downA&&!downD&&!hyper)return null;
                    if(downA){animation=a.Sequence;argument=downD?57:22;}
                    else if(downD){animation=b.Sequence;argument=23;}
                    break;
                case 207:
                    bool raised=Changed(a,WildBattleStat.Attack,2),capped=Cap(a,WildBattleStat.Attack,"maximum");
                    bool confused=b.Kind=="confusion"&&b.Target==used.Target&&b.Before==0&&b.After is >=2 and <=5&&b.Detail=="move";
                    bool blocked=Protection(b,"ability","ABILITY_OWN_TEMPO")||Protection(b,"failed","safeguard","already-confused");
                    if(!(raised||capped)||!(confused||blocked)||capped&&b.Kind=="failed"&&b.Detail=="already-confused")return null;
                    if(raised){animation=a.Sequence;argument=39;}
                    break;
            }
        }
        // An item cannot borrow the direct move's own result as its prelude.
        for(int i=first;i<first+count;i++)if(items.Contains(rows[i].Sequence))return null;
        for(int i=first+count;i<end;i++)
            if(rows[i].Kind is "pp" or "stat" or "minimum" or "maximum" or "confusion" or "ability" or "failed" or "missed" or "status" or "focus" or "infatuation" && !items.Contains(rows[i].Sequence))return null;
        var silent=rows.Skip(first).Take(count).Where(r=>r.Kind is "minimum" or "maximum").Select(r=>r.Sequence).ToArray();
        return new(battle,attack.Sequence,pp.Sequence,move,used.Target,a.Sequence,animation,argument,silent);
    }
    private static GroupRow GroupRead(SourceBattleEvent e)=>e switch
    {
        SourceAttackStringEvent x=>new(x.Sequence,x.Turn,"attack",x.Actor.Value,Move:x.CurrentMove.Value,Chosen:x.ChosenMove.Value),
        SourceMoveUsedEvent x=>new(x.Sequence,x.Turn,"used",x.Actor.Value,x.Target.Value,x.Move.Value,Slot:x.Slot,Before:x.PpBefore,After:x.PpAfter),
        SourcePpCommandEvent x=>new(x.Sequence,x.Turn,"pp",x.Actor.Value,x.Target.Value,x.CurrentMove.Value,x.ChosenMove.Value,x.PpOwner.Value,x.PpSlot,x.Before,x.After,x.CommandCost),
        SourceHpChangedEvent x=>new(x.Sequence,x.Turn,"hp",x.Actor.Value,x.Target.Value,x.Move.Value,Before:x.HpBefore,After:x.HpAfter,Damage:x.Damage,Critical:x.Critical,Percent:x.RandomPercent),
        SourceSubstituteDamagedEvent x=>new(x.Sequence,x.Turn,"substitute",x.Actor.Value,x.Target.Value,x.Move.Value,Before:x.HpBefore,After:x.HpAfter,Damage:x.Damage,Critical:x.Critical,Percent:x.RandomPercent),
        SourceCriticalHitEvent x=>new(x.Sequence,x.Turn,"critical",x.Actor.Value,x.Target.Value),
        SourceEffectivenessEvent x=>new(x.Sequence,x.Turn,"effectiveness",x.Actor.Value,x.Target.Value,x.Move.Value,Detail:x.Effectiveness.ToString()),
        SourceDamageAdjustedEvent x=>new(x.Sequence,x.Turn,"adjustment",Target:x.Target.Value,Move:x.Move.Value,Before:x.DamageBefore,After:x.DamageAfter,Detail:x.Item,Endured:x.Endured,HungOn:x.HungOn),
        SourceHeldItemSurvivalEvent x=>new(x.Sequence,x.Turn,"survival",Target:x.Target.Value,Move:x.Move.Value,Detail:x.Item),
        SourceMtMoonVolatileEvent {Volatile:"endure",Before:1,After:1,Reason:"endured"} x=>new(x.Sequence,x.Turn,"endured",Target:x.Side.Value),
        SourceStatChangedEvent x=>new(x.Sequence,x.Turn,"stat",x.Actor.Value,x.Target.Value,Before:x.Before,After:x.After,Stat:x.Stat),
        SourceStatAtMinimumEvent x=>new(x.Sequence,x.Turn,"minimum",x.Actor.Value,x.Target.Value,Stat:x.Stat),
        SourceStatAtMaximumEvent x=>new(x.Sequence,x.Turn,"maximum",x.Actor.Value,x.Target.Value,Stat:x.Stat),
        SourceAbilityPreventedEvent x=>new(x.Sequence,x.Turn,"ability",x.Actor.Value,x.Target.Value,x.Move.Value,Detail:x.Ability),
        SourceMoveFailedEvent x=>new(x.Sequence,x.Turn,"failed",x.Actor.Value,x.Target.Value,x.Move.Value,Detail:x.Reason),
        SourceMoveMissedEvent x=>new(x.Sequence,x.Turn,"missed",x.Actor.Value,x.Target.Value,x.Move.Value),
        SourceConfusionChangedEvent x=>new(x.Sequence,x.Turn,"confusion",Target:x.Side.Value,Before:x.Before,After:x.After,Detail:x.Reason),
        SourceStatusChangedEvent=>new(e.Sequence,e.Turn,"status"),
        SourceFocusEnergyEvent=>new(e.Sequence,e.Turn,"focus"),
        SourceInfatuationChangedEvent=>new(e.Sequence,e.Turn,"infatuation"),
        SourceOrderEvent or SourceTurnReadyEvent or SourceStartedEvent or SourceActionBlockedEvent or SourceCalledPartySwitchedEvent or SourcePlayerSwitchedEvent or SourcePlayerReplacedEvent=>new(e.Sequence,e.Turn,"boundary"),
        _=>new(e.Sequence,e.Turn,"other")
    };
    private static GroupRow GroupRead(WildBattleEvent e)=>e switch
    {
        WildBattleAttackStringEvent x=>new(x.Sequence,x.Turn,"attack",(int)x.Actor,Move:x.CurrentMove.Value,Chosen:x.ChosenMove.Value),
        WildBattleMoveUsedEvent x=>new(x.Sequence,x.Turn,"used",(int)x.Actor,(int)x.Target,x.Move.Value,Slot:x.Slot,Before:x.PpBefore,After:x.PpAfter),
        WildBattlePpCommandEvent x=>new(x.Sequence,x.Turn,"pp",(int)x.Actor,(int)x.Target,x.CurrentMove.Value,x.ChosenMove.Value,(int)x.PpOwner,x.PpSlot,x.Before,x.After,x.CommandCost),
        WildBattleHpChangedEvent x=>new(x.Sequence,x.Turn,"hp",(int)x.Actor,(int)x.Target,x.Move.Value,Before:x.HpBefore,After:x.HpAfter,Damage:x.Damage,Critical:x.Critical,Percent:x.RandomPercent),
        WildBattleSubstituteDamagedEvent x=>new(x.Sequence,x.Turn,"substitute",(int)x.Actor,(int)x.Target,x.Move.Value,Before:x.HpBefore,After:x.HpAfter,Damage:x.Damage,Critical:x.Critical,Percent:x.RandomPercent),
        WildBattleCriticalHitEvent x=>new(x.Sequence,x.Turn,"critical",(int)x.Actor,(int)x.Target),
        WildBattleEffectivenessEvent x=>new(x.Sequence,x.Turn,"effectiveness",(int)x.Actor,(int)x.Target,x.Move.Value,Detail:x.Effectiveness.ToString()),
        WildBattleDamageAdjustedEvent x=>new(x.Sequence,x.Turn,"adjustment",Target:(int)x.Target,Move:x.Move.Value,Before:x.DamageBefore,After:x.DamageAfter,Detail:x.Item,Endured:x.Endured,HungOn:x.HungOn),
        WildBattleHeldItemSurvivalEvent x=>new(x.Sequence,x.Turn,"survival",Target:(int)x.Target,Move:x.Move.Value,Detail:x.Item),
        WildBattleMtMoonVolatileEvent {Volatile:"endure",Before:1,After:1,Reason:"endured"} x=>new(x.Sequence,x.Turn,"endured",Target:(int)x.Side),
        WildBattleStatChangedEvent x=>new(x.Sequence,x.Turn,"stat",(int)x.Actor,(int)x.Target,Before:x.Before,After:x.After,Stat:x.Stat),
        WildBattleStatAtMinimumEvent x=>new(x.Sequence,x.Turn,"minimum",(int)x.Actor,(int)x.Target,Stat:x.Stat),
        WildBattleStatAtMaximumEvent x=>new(x.Sequence,x.Turn,"maximum",(int)x.Actor,(int)x.Target,Stat:x.Stat),
        WildBattleAbilityPreventedEvent x=>new(x.Sequence,x.Turn,"ability",(int)x.Actor,(int)x.Target,x.Move.Value,Detail:x.Ability),
        WildBattleMoveFailedEvent x=>new(x.Sequence,x.Turn,"failed",(int)x.Actor,(int)x.Target,x.Move.Value,Detail:x.Reason),
        WildBattleMoveMissedEvent x=>new(x.Sequence,x.Turn,"missed",(int)x.Actor,(int)x.Target,x.Move.Value),
        WildBattleConfusionChangedEvent x=>new(x.Sequence,x.Turn,"confusion",Target:(int)x.Side,Before:x.Before,After:x.After,Detail:x.Reason),
        WildBattleStatusChangedEvent=>new(e.Sequence,e.Turn,"status"),
        WildBattleFocusEnergyEvent=>new(e.Sequence,e.Turn,"focus"),
        WildBattleInfatuationChangedEvent=>new(e.Sequence,e.Turn,"infatuation"),
        WildBattleOrderEvent or WildBattleTurnReadyEvent or WildBattleStartedEvent or WildBattleActionBlockedEvent or WildBattleCalledPartySwitchedEvent or WildBattlePlayerSwitchedEvent or WildBattlePlayerReplacedEvent=>new(e.Sequence,e.Turn,"boundary"),
        _=>new(e.Sequence,e.Turn,"other")
    };
}
