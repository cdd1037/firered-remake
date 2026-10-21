using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Battle;
using Game.Domain.WildBattle;
namespace Game.Application;

/// <summary>Original General_StatsChange, before its result text. This is separate from
/// the dedicated move root and completes without acknowledging the result event.</summary>
public sealed record CampaignStatAnimationDispatch(string BattleId,long Sequence,long? AttackSequence,long PpSequence,
    int SourceMoveId,BattlerSlot Affected,int AnimationArgument);
public static partial class CampaignStatAnimationPresentation
{
    private sealed record Row(long Sequence,int Turn,string Kind,int Actor=-1,int Target=-1,string Move="",string Chosen="",
        int Owner=-1,WildBattleStat Stat=default,int Before=0,int After=0,int Slot=-1,int Cost=0);
    private static int AnimationArgument(WildBattleStat stat,int delta)
    {
        int statIndex=stat switch{WildBattleStat.Attack=>0,WildBattleStat.Defense=>1,WildBattleStat.Speed=>2,WildBattleStat.SpecialAttack=>3,WildBattleStat.SpecialDefense=>4,WildBattleStat.Accuracy=>5,WildBattleStat.Evasion=>6,_=>throw new ArgumentOutOfRangeException(nameof(stat))};
        // Cmd_setgraphicalstatchangevalues uses the requested change, even when
        // the resulting stage was clamped at zero.
        return (delta switch{1=>15,2=>39,-1=>22,-2=>46,_=>throw new ArgumentOutOfRangeException(nameof(delta))})+statIndex;
    }
    private static CampaignStatAnimationDispatch? Match(string battleId,SourceBattleEvent[] events,Row[] rows,int index,bool substitute,IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves,bool doubles,Func<int,int> consumedGroupStart)
    {
        if(substitute||index<3||index>=rows.Length)return null;
        // A prior target can consume an item before the next target pass. Reuse
        // the existing typed item-group authority; do not mistake its restoration
        // rows for another move stat result or accept an unauthenticated suffix.
        for(int end=0;end<index-1;end++)
            if(rows[end].Kind=="consumed"&&consumedGroupStart(end) is var start&&start>=0)
                for(int i=start;i<=end;i++)rows[i]=rows[i] with{Kind="item-suffix"};
        // Typed self-state receipts can occur before or after PP in the committed
        // kernel stream. Remove only the matching invocation's optional prelude;
        // stat/PP ordering and all other boundaries remain checked below.
        int invocation=Array.FindLastIndex(rows,index-1,r=>r.Kind=="used");
        if(invocation>=0)
        {
            var moveRow=rows[invocation];
            var prelude=rows.Skip(invocation+1).Take(index-invocation-1).Where(r=>r.Kind=="self-prelude").ToArray();
            if(prelude.Length>1||prelude.Any(r=>r.Move!=moveRow.Move||r.Actor!=moveRow.Actor||r.Target!=moveRow.Actor||r.Turn!=moveRow.Turn||r.Sequence<=moveRow.Sequence||r.Sequence>=rows[index].Sequence))return null;
            if(prelude.Length>0){rows=rows.Where((r,i)=>i<=invocation||i>=index||r.Kind!="self-prelude").ToArray();index-=prelude.Length;}
        }
        var stat=rows[index];var pp=rows[index-1];
        if(stat.Kind!="stat"||stat.Actor<0||stat.Actor>=(doubles?4:2)||stat.Target<0||stat.Target>=(doubles?4:2)||stat.Before is <0 or >12||stat.After is <0 or >12||stat.After==stat.Before||
            pp.Kind!="pp"||pp.Actor!=stat.Actor||pp.Target!=stat.Target||pp.Owner!=stat.Actor||pp.Turn!=stat.Turn||pp.Sequence>=stat.Sequence)return null;
        var definition=CampaignMoveAnimationPresentation.Definition(new(pp.Move),moves);
        if(definition is null)return null;
        int sourceMove=definition.SourceMoveId;
        var change=CampaignMoveAnimationPresentation.ReductionAnimation(definition)
            ?? CampaignMoveAnimationPresentation.SelfStatAnimation(definition);
        var spec=change is {} delta
            ? (Move:sourceMove,Stat:delta.Stat,Delta:delta.Delta,Arg:AnimationArgument(delta.Stat,delta.Delta))
            : default;
        if(spec.Move==0||spec.Stat!=stat.Stat||stat.After!=Math.Clamp(stat.Before+spec.Delta,0,12)||
            (spec.Delta>0?stat.Target!=stat.Actor:stat.Target==stat.Actor||(stat.Target&1)==(stat.Actor&1)&&!AllowsSelectedStatAlly(pp.Move,moves)))return null;
        int used=index-2;
        // BOTH-target primary reductions may revisit after the first result or prevention.
        while(used>=0&&rows[used].Kind!="used")
        {
            if(rows[used].Turn!=stat.Turn||rows[used].Kind is not("pp" or "stat" or "cap" or "failed" or "item-suffix"))return null;
            used--;
        }
        if(used<1)return null;var move=rows[used];var attack=rows[used-1];
        // Read the untouched typed receipts at the original index. Optional
        // self-state rows above affect result matching, never caller authority.
        var identity=CampaignAnimationInvocation.TryRead(events,invocation,doubles?4:2,moves);
        if(identity is null||events[identity.UsedIndex].Sequence!=move.Sequence||
            events[identity.AttackIndex].Sequence!=attack.Sequence)return null;
        if(move.Actor!=stat.Actor||move.Move!=pp.Move||move.Turn!=stat.Turn||attack.Kind!="attack"||attack.Actor!=stat.Actor||
            attack.Move!=identity.CurrentMove.Value||attack.Chosen!=identity.ChosenMove.Value||attack.Turn!=stat.Turn||attack.Sequence>=move.Sequence||move.Sequence>=pp.Sequence)return null;
        for(int i=used;i<index;i++)if(rows[i].Sequence>=rows[i+1].Sequence)return null;
        var passes=rows.Skip(used+1).Take(index-used-1).Where(r=>r.Kind=="pp").ToArray();
        if(((!doubles||definition.Target!="MOVE_TARGET_BOTH")&&passes.Length!=1)||passes.Length is <1 or >2||passes.Select(r=>r.Target).Distinct().Count()!=passes.Length||passes[0].Target!=move.Target||
            passes.Any(r=>r.Actor!=stat.Actor||r.Owner!=identity.PpOwner.Value||r.Move!=identity.CurrentMove.Value||r.Chosen!=identity.ChosenMove.Value||r.Turn!=stat.Turn||r.Sequence<=move.Sequence||r.Slot!=identity.PpSlot||r.Slot!=move.Slot||r.Slot is <0 or >3||r.Before is <0 or >255||r.After is <0 or >255||r.Cost<1||r.Cost>(definition.Target=="MOVE_TARGET_BOTH"?3:2))||
            passes[0].Before!=move.Before||passes[0].After!=move.After||(identity.NoPpDeduct?passes[0].After!=passes[0].Before:passes[0].Before<1||passes[0].After!=Math.Max(0,passes[0].Before-passes[0].Cost))||passes.Skip(1).Any(r=>r.Before!=passes[0].After||r.After!=r.Before)||
            passes.Skip(1).Any(r=>r.Target!=(move.Target^2)))return null;
        if(rows.Skip(used+1).Take(index-used-1).Any(r=>r.Kind is "stat" or "cap" or "failed"&&
            (r.Actor!=stat.Actor||!passes.Any(p=>p.Target==r.Target)||r.Kind=="failed"&&r.Move!=pp.Move)))return null;
        if(rows.Skip(used+1).Take(index-used-1).Any(r=>r.Kind=="item-suffix"&&
            !passes.Any(p=>p.Target==r.Target&&p.Sequence<r.Sequence)))return null;
        // No successful result may be borrowed twice from a repeated/malformed pass.
        if(rows.Skip(used+1).Take(index-used-1).Any(r=>r.Kind=="stat"&&r.Target==stat.Target))return null;
        return new(battleId,stat.Sequence,attack.Sequence,pp.Sequence,spec.Move,new(stat.Target),spec.Arg);
    }
    public static CampaignStatAnimationDispatch? AtResult(CampaignWildBattle battle,bool sourceRules,bool affectedBehindSubstitute=false,IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves=null)
    {
        if(!sourceRules||battle.Phase!=CampaignBattlePhase.Events)return null;
        if(CampaignMoveAnimationPresentation.ChargeStatResult(battle.Id,battle.Events.ToArray(),battle.EventIndex,2,moves,affectedBehindSubstitute) is {} charge)return charge;
        if(Grouped(battle,true,moves) is {} grouped)return GroupAnimation(grouped,battle.Events[battle.EventIndex].Sequence,affectedBehindSubstitute,moves);
        return Match(battle.Id,battle.Events.ToArray(),battle.Events.Select(Read).ToArray(),battle.EventIndex,affectedBehindSubstitute,moves,false,i=>CampaignHeldItemAnimationPresentation.ConsumedGroupStart(battle.Events,i))??Secondary(battle,moves,affectedBehindSubstitute);
    }
    public static CampaignStatAnimationDispatch? AtResult(CampaignDoubleBattle battle,bool affectedBehindSubstitute=false,IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves=null)
    {
        if(battle.Stage!=CampaignDoubleBattleStage.Combat)return null;
        if(CampaignMoveAnimationPresentation.ChargeStatResult(battle.Id,battle.Events.ToArray(),battle.EventIndex,4,moves,affectedBehindSubstitute) is {} charge)return charge;
        if(Grouped(battle,moves) is {} grouped)return GroupAnimation(grouped,battle.Events[battle.EventIndex].Sequence,affectedBehindSubstitute,moves);
        return Match(battle.Id,battle.Events.ToArray(),battle.Events.Select(Read).ToArray(),battle.EventIndex,affectedBehindSubstitute,moves,true,i=>CampaignHeldItemAnimationPresentation.ConsumedGroupStart(battle.Events,i))??Secondary(battle,moves,affectedBehindSubstitute);
    }
    private static Row Read(SourceBattleEvent e)=>e switch
    {
        SourceDefenseCurledEvent s=>new Row(s.Sequence,s.Turn,"self-prelude",s.Side.Value,s.Side.Value,"pret:MOVE_DEFENSE_CURL"),
        SourceMtMoonVolatileEvent s when s.Volatile=="minimized"&&s.Before==0&&s.After==1&&s.Reason=="move"=>new Row(s.Sequence,s.Turn,"self-prelude",s.Side.Value,s.Side.Value,"pret:MOVE_MINIMIZE"),
        SourceStatChangedEvent s=>new Row(s.Sequence,s.Turn,"stat",s.Actor.Value,s.Target.Value,Stat:s.Stat,Before:s.Before,After:s.After),
        SourcePpCommandEvent p=>new(p.Sequence,p.Turn,"pp",p.Actor.Value,p.Target.Value,p.CurrentMove.Value,p.ChosenMove.Value,p.PpOwner.Value,Before:p.Before,After:p.After,Slot:p.PpSlot,Cost:p.CommandCost),
        SourceMoveUsedEvent m=>new(m.Sequence,m.Turn,"used",m.Actor.Value,m.Target.Value,m.Move.Value,Before:m.PpBefore,After:m.PpAfter,Slot:m.Slot),
        SourceAttackStringEvent a=>new(a.Sequence,a.Turn,"attack",a.Actor.Value,Move:a.CurrentMove.Value,Chosen:a.ChosenMove.Value),
        SourceStatAtMinimumEvent c=>new(c.Sequence,c.Turn,"cap",c.Actor.Value,c.Target.Value),
        SourceStatAtMaximumEvent c=>new(c.Sequence,c.Turn,"cap",c.Actor.Value,c.Target.Value),
        SourceHeldItemConsumedEvent h=>new(h.Sequence,h.Turn,"consumed",h.Side.Value,h.Side.Value),
        SourceMoveMissedEvent f=>new(f.Sequence,f.Turn,"failed",f.Actor.Value,f.Target.Value,f.Move.Value),
        SourceMoveFailedEvent f=>new(f.Sequence,f.Turn,"failed",f.Actor.Value,f.Target.Value,f.Move.Value),
        SourceAbilityPreventedEvent f=>new(f.Sequence,f.Turn,"failed",f.Actor.Value,f.Target.Value,f.Move.Value),
        _=>new Row(e.Sequence,e.Turn,"boundary")
    };
}
