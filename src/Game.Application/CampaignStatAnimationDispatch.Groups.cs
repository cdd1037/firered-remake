using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
namespace Game.Application;

public static partial class CampaignStatAnimationPresentation
{
    private static (WildBattleStat First,WildBattleStat Second)? PairedSelfStats(string effect)=>effect switch {
        "EFFECT_COSMIC_POWER"=>(WildBattleStat.Defense,WildBattleStat.SpecialDefense),
        "EFFECT_BULK_UP"=>(WildBattleStat.Attack,WildBattleStat.Defense),
        "EFFECT_CALM_MIND"=>(WildBattleStat.SpecialAttack,WildBattleStat.SpecialDefense),
        "EFFECT_DRAGON_DANCE"=>(WildBattleStat.Attack,WildBattleStat.Speed),_=>null };
    private static (WildBattleStat Stat,int Delta)? ConfusionBoost(string effect)=>effect switch {
        "EFFECT_SWAGGER"=>(WildBattleStat.Attack,2),"EFFECT_FLATTER"=>(WildBattleStat.SpecialAttack,1),_=>null };
    internal static bool HasGroupedEffect(string effect)=>PairedSelfStats(effect) is not null||ConfusionBoost(effect) is not null||effect is "EFFECT_TICKLE" or "EFFECT_MEMENTO";
    private static bool HasGroupedDefinition(PokemonMoveDefinition definition)=>HasGroupedEffect(definition.Effect)&&definition.Power==0&&
        definition.Target==(PairedSelfStats(definition.Effect) is not null?"MOVE_TARGET_USER":"MOVE_TARGET_SELECTED");
    private sealed record StatGroup(string Battle,long Attack,long Pp,string Move,int Affected,long First,long? Animation,int? Argument,long[] Silent);
    private sealed record GroupRow(long Sequence,int Turn,string Kind,int Actor=-1,int Target=-1,string Move="",string Chosen="",
        int Owner=-1,int Slot=-1,int Before=0,int After=0,int Cost=0,WildBattleStat Stat=default,string Detail="",int Damage=0,bool Critical=false,bool Endured=false,bool HungOn=false,int Percent=100);
    private static CampaignStatAnimationDispatch? GroupAnimation(StatGroup group,long current,bool substitute,IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves)=>
        !substitute&&group.Animation==current&&group.Argument is {} arg&&CampaignMoveAnimationPresentation.Definition(new(group.Move),moves) is {} definition
            ?new(group.Battle,current,group.Attack,group.Pp,definition.SourceMoveId,new(group.Affected),arg):null;

    // Dedicated roots, generic stat waves and cap text use one validated source
    // result prefix. A later typed held-item activation remains a separate group.
    public static long? GroupedMoveResultSequence(CampaignDoubleBattle battle,IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves)=>Grouped(battle,moves)?.First;
    public static long? GroupedMoveResultSequence(CampaignWildBattle battle,bool sourceRules,IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves)=>Grouped(battle,sourceRules,moves)?.First;
    public static bool IsSilentResultReceipt(CampaignDoubleBattle battle,IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves)=>
        Grouped(battle,moves) is {} group&&group.Silent.Contains(battle.Events[battle.EventIndex].Sequence);
    public static bool IsSilentResultReceipt(CampaignWildBattle battle,bool sourceRules,IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves)=>
        Grouped(battle,sourceRules,moves) is {} group&&group.Silent.Contains(battle.Events[battle.EventIndex].Sequence);

    private static StatGroup? Grouped(CampaignDoubleBattle battle,IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves)
    {
        if(battle.Stage!=CampaignDoubleBattleStage.Combat||battle.EventIndex<0||battle.EventIndex>=battle.Events.Length)return null;
        int invocation=battle.EventIndex;
        while(invocation>=0&&battle.Events[invocation] is not (SourceMoveUsedEvent or SourceAttackStringEvent))invocation--;
        if(invocation<0||battle.Events[invocation] is not SourceMoveUsedEvent used||
            CampaignMoveAnimationPresentation.Definition(used.Move,moves) is not {} definition||!HasGroupedDefinition(definition))return null;
        var itemReceipts=ItemReceiptSequences(battle.Events);
        return ReadGroup(battle.Id,battle.Events.ToArray(),battle.Events.Select(GroupRead).ToArray(),battle.EventIndex,4,itemReceipts,definition.Effect,moves);
    }
    private static StatGroup? Grouped(CampaignWildBattle battle,bool sourceRules,IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves)
    {
        if(!sourceRules||battle.Phase!=CampaignBattlePhase.Events||battle.EventIndex<0||battle.EventIndex>=battle.Events.Length)return null;
        int invocation=battle.EventIndex;
        while(invocation>=0&&battle.Events[invocation] is not (SourceMoveUsedEvent or SourceAttackStringEvent))invocation--;
        if(invocation<0||battle.Events[invocation] is not SourceMoveUsedEvent used||
            CampaignMoveAnimationPresentation.Definition(used.Move,moves) is not {} definition||!HasGroupedDefinition(definition))return null;
        var itemReceipts=ItemReceiptSequences(battle.Events);
        return ReadGroup(battle.Id,battle.Events.ToArray(),battle.Events.Select(GroupRead).ToArray(),battle.EventIndex,2,itemReceipts,definition.Effect,moves);
    }
    internal static HashSet<long> ItemReceiptSequences(IReadOnlyList<SourceBattleEvent> events)
    {
        var result=new HashSet<long>();
        for(int end=0;end<events.Count;end++)if(events[end] is SourceHeldItemConsumedEvent)
        {int start=CampaignHeldItemAnimationPresentation.ConsumedGroupStart(events,end);if(start>=0)for(int i=start;i<=end;i++)result.Add(events[i].Sequence);}
        return result;
    }
    private static bool GroupBoundary(GroupRow row)=>row.Kind is "attack" or "used" or "boundary";
    private static StatGroup? ReadGroup(string battle,SourceBattleEvent[] events,GroupRow[] rows,int index,int slots,HashSet<long> items,string effect,
        IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves)
    {
        int usedIndex=index;
        while(usedIndex>=0&&!GroupBoundary(rows[usedIndex]))usedIndex--;
        if(usedIndex<1||rows[usedIndex].Kind!="used")return null;
        var used=rows[usedIndex];var attack=rows[usedIndex-1];
        var identity=CampaignAnimationInvocation.TryRead(events,usedIndex,slots,moves);
        if(identity is null||identity.AttackIndex!=usedIndex-1)return null;
        string move=used.Move;
        if(!HasGroupedEffect(effect)||attack.Kind!="attack"||attack.Actor!=used.Actor||attack.Move!=used.Move||attack.Chosen!=identity.ChosenMove.Value||attack.Turn!=used.Turn||
            used.Actor<0||used.Actor>=slots||used.Target<0||used.Target>=slots||used.Slot is <0 or >3||
            (PairedSelfStats(effect) is not null?used.Actor!=used.Target:used.Actor==used.Target))return null;
        int end=usedIndex+1;
        while(end<rows.Length&&rows[end].Turn==used.Turn&&!GroupBoundary(rows[end]))end++;
        if(index>=end||end-usedIndex<3)return null;
        for(int i=usedIndex;i<end;i++)if(rows[i].Sequence<=rows[i-1].Sequence)return null;
        var pp=rows[usedIndex+1];
        if(pp.Kind!="pp"||pp.Actor!=used.Actor||pp.Owner!=identity.PpOwner.Value||pp.Target!=identity.Target.Value||pp.Move!=identity.CurrentMove.Value||pp.Chosen!=identity.ChosenMove.Value||
            pp.Slot!=identity.PpSlot||pp.Slot!=used.Slot||pp.Before!=used.Before||pp.After!=used.After||pp.Before is <0 or >255||pp.After is <0 or >255||pp.Cost is <1 or >2||
            (identity.NoPpDeduct?pp.After!=pp.Before:pp.Before<1||pp.After!=Math.Max(0,pp.Before-pp.Cost)))return null;
        bool memento=effect=="EFFECT_MEMENTO";
        if(memento&&!CampaignMoveAnimationPresentation.AuthenticatedMementoGroup(battle,events,usedIndex,slots,moves))return null;
        int first=usedIndex+(memento?3:2);if(first>=end)return null;var a=rows[first];long? animation=null;int? argument=null;int count=2;
        bool Owner(GroupRow r)=>r.Actor==used.Actor&&r.Target==used.Target;
        bool Changed(GroupRow r,WildBattleStat stat,int delta)=>Owner(r)&&r.Kind=="stat"&&r.Stat==stat&&r.Before is >=0 and <=12&&r.After!=r.Before&&r.After==Math.Clamp(r.Before+delta,0,12);
        bool Cap(GroupRow r,WildBattleStat stat,string kind)=>Owner(r)&&r.Kind==kind&&r.Stat==stat;
        bool Protection(GroupRow r,string kind,params string[] reasons)=>Owner(r)&&r.Kind==kind&&r.Move==used.Move&&reasons.Contains(r.Detail);
        if(effect is "EFFECT_TICKLE" or "EFFECT_MEMENTO"&&(Protection(a,"failed","mist")||Protection(a,"ability","ABILITY_CLEAR_BODY","ABILITY_WHITE_SMOKE")))count=1;
        else
        {
            if(first+1>=end)return null;var b=rows[first+1];
            switch(effect)
            {
                case "EFFECT_COSMIC_POWER":case "EFFECT_BULK_UP":case "EFFECT_CALM_MIND":case "EFFECT_DRAGON_DANCE":
                    var (firstStat,secondStat)=PairedSelfStats(effect)!.Value;
                    bool upFirst=Changed(a,firstStat,1),upSecond=Changed(b,secondStat,1);
                    if(!(upFirst||Cap(a,firstStat,"maximum"))||!(upSecond||Cap(b,secondStat,"maximum"))||!(upFirst||upSecond))return null;
                    // Both source scripts choose the wave before visiting either
                    // stat command, including an individually capped first stat.
                    animation=a.Sequence;argument=upFirst&&upSecond?55:AnimationArgument(upFirst?firstStat:secondStat,1);break;
                case "EFFECT_TICKLE":case "EFFECT_MEMENTO":
                    var secondDown=memento?WildBattleStat.SpecialAttack:WildBattleStat.Defense;int delta=memento?-2:-1;
                    bool downA=Changed(a,WildBattleStat.Attack,delta),downD=Changed(b,secondDown,delta);
                    bool hyper=Protection(a,"ability","ABILITY_HYPER_CUTTER");
                    if(!(downA||Cap(a,WildBattleStat.Attack,"minimum")||hyper)||!(downD||Cap(b,secondDown,"minimum"))||
                        !downA&&!downD&&!hyper)return null;
                    if(downA){animation=a.Sequence;argument=downD?(memento?58:57):AnimationArgument(WildBattleStat.Attack,delta);}
                    else if(downD){animation=b.Sequence;argument=AnimationArgument(secondDown,delta);}
                    break;
                case "EFFECT_SWAGGER":case "EFFECT_FLATTER":
                    var (boostStat,boostDelta)=ConfusionBoost(effect)!.Value;
                    bool raised=Changed(a,boostStat,boostDelta),capped=Cap(a,boostStat,"maximum");
                    bool confused=b.Kind=="confusion"&&b.Target==used.Target&&b.Before==0&&b.After is >=2 and <=5&&b.Detail=="move";
                    bool blocked=Protection(b,"ability","ABILITY_OWN_TEMPO")||Protection(b,"failed","safeguard","already-confused");
                    if(!(raised||capped)||!(confused||blocked)||capped&&b.Kind=="failed"&&b.Detail=="already-confused")return null;
                    if(raised){animation=a.Sequence;argument=AnimationArgument(boostStat,boostDelta);}
                    break;
            }
        }
        // An item cannot borrow the authenticated move's own result as its prelude.
        for(int i=first;i<first+count;i++)if(items.Contains(rows[i].Sequence))return null;
        for(int i=first+count;!memento&&i<end;i++)
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
}
