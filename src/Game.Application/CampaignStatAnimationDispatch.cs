using Game.Domain.Battle;
using Game.Domain.WildBattle;
namespace Game.Application;

/// <summary>Original General_StatsChange, before its result text. This is separate from
/// the dedicated move root and completes without acknowledging the result event.</summary>
public sealed record CampaignStatAnimationDispatch(string BattleId,long Sequence,long AttackSequence,long PpSequence,
    int SourceMoveId,BattlerSlot Affected,int AnimationArgument);
public static class CampaignStatAnimationPresentation
{
    private sealed record Row(long Sequence,int Turn,string Kind,int Actor=-1,int Target=-1,string Move="",string Chosen="",
        int Owner=-1,WildBattleStat Stat=default,int Before=0,int After=0);
    private static CampaignStatAnimationDispatch? Match(string battleId,Row[] rows,int index,bool substitute)
    {
        if(substitute||index<3||index>=rows.Length)return null;
        var stat=rows[index];var pp=rows[index-1];
        if(stat.Kind!="stat"||stat.Before is <0 or >12||stat.After is <0 or >12||stat.After==stat.Before||
            pp.Kind!="pp"||pp.Actor!=stat.Actor||pp.Target!=stat.Target||pp.Owner!=stat.Actor||pp.Move!=pp.Chosen||pp.Turn!=stat.Turn||pp.Sequence>=stat.Sequence)return null;
        var spec=pp.Move switch
        {
            "pret:MOVE_AGILITY"=>(Move:97,Stat:WildBattleStat.Speed,Delta:2,Arg:41),
            "pret:MOVE_GROWTH"=>(Move:74,Stat:WildBattleStat.SpecialAttack,Delta:1,Arg:18),
            "pret:MOVE_MINIMIZE"=>(Move:107,Stat:WildBattleStat.Evasion,Delta:1,Arg:21),
            "pret:MOVE_WITHDRAW"=>(Move:110,Stat:WildBattleStat.Defense,Delta:1,Arg:16),
            "pret:MOVE_HARDEN"=>(Move:106,Stat:WildBattleStat.Defense,Delta:1,Arg:16),
            "pret:MOVE_IRON_DEFENSE"=>(Move:334,Stat:WildBattleStat.Defense,Delta:2,Arg:40),
            "pret:MOVE_TAIL_WHIP"=>(Move:39,Stat:WildBattleStat.Defense,Delta:-1,Arg:23),
            "pret:MOVE_FLASH"=>(Move:148,Stat:WildBattleStat.Accuracy,Delta:-1,Arg:27),
            _=>default
        };
        if(spec.Move==0||spec.Stat!=stat.Stat||stat.After!=Math.Clamp(stat.Before+spec.Delta,0,12)||
            (spec.Delta>0?stat.Target!=stat.Actor:(stat.Target&1)==(stat.Actor&1)))return null;
        int used=index-2;
        // Tail Whip can have a second target pass after the first target's result/cap.
        while(used>=0&&rows[used].Kind!="used")
        {
            if(rows[used].Turn!=stat.Turn||rows[used].Kind is not("pp" or "stat" or "cap" or "failed"))return null;
            used--;
        }
        if(used<1)return null;var move=rows[used];var attack=rows[used-1];
        if(move.Actor!=stat.Actor||move.Move!=pp.Move||move.Turn!=stat.Turn||attack.Kind!="attack"||attack.Actor!=stat.Actor||
            attack.Move!=pp.Move||attack.Chosen!=pp.Move||attack.Turn!=stat.Turn||attack.Sequence>=move.Sequence||move.Sequence>=pp.Sequence)return null;
        var passes=rows.Skip(used+1).Take(index-used-1).Where(r=>r.Kind=="pp").ToArray();
        if((spec.Move!=39&&passes.Length!=1)||passes.Length is <1 or >2||passes.Select(r=>r.Target).Distinct().Count()!=passes.Length||passes[0].Target!=move.Target||
            passes.Any(r=>r.Actor!=stat.Actor||r.Owner!=stat.Actor||r.Move!=pp.Move||r.Chosen!=pp.Move||r.Turn!=stat.Turn||r.Sequence<=move.Sequence)||
            passes[0].Before!=move.Before||passes[0].After!=move.After||passes[0].After>=passes[0].Before||passes.Skip(1).Any(r=>r.Before!=passes[0].After||r.After!=r.Before)||
            passes.Skip(1).Any(r=>r.Target!=(move.Target^2)))return null;
        if(rows.Skip(used+1).Take(index-used-1).Any(r=>r.Kind is "stat" or "cap" or "failed"&&
            (r.Actor!=stat.Actor||!passes.Any(p=>p.Target==r.Target)||r.Kind=="failed"&&r.Move!=pp.Move)))return null;
        // No successful result may be borrowed twice from a repeated/malformed pass.
        if(rows.Skip(used+1).Take(index-used-1).Any(r=>r.Kind=="stat"&&r.Target==stat.Target))return null;
        return new(battleId,stat.Sequence,attack.Sequence,pp.Sequence,spec.Move,new(stat.Target),spec.Arg);
    }
    public static CampaignStatAnimationDispatch? AtResult(CampaignWildBattle battle,bool sourceRules,bool affectedBehindSubstitute=false)
    {
        if(!sourceRules||battle.Phase!=CampaignBattlePhase.Events)return null;
        return Match(battle.Id,battle.Events.Select(e=>e switch
        {
            WildBattleStatChangedEvent s=>new Row(s.Sequence,s.Turn,"stat",(int)s.Actor,(int)s.Target,Stat:s.Stat,Before:s.Before,After:s.After),
            WildBattlePpCommandEvent p=>new(p.Sequence,p.Turn,"pp",(int)p.Actor,(int)p.Target,p.CurrentMove.Value,p.ChosenMove.Value,(int)p.PpOwner,Before:p.Before,After:p.After),
            WildBattleMoveUsedEvent m=>new(m.Sequence,m.Turn,"used",(int)m.Actor,(int)m.Target,m.Move.Value,Before:m.PpBefore,After:m.PpAfter),
            WildBattleAttackStringEvent a=>new(a.Sequence,a.Turn,"attack",(int)a.Actor,Move:a.CurrentMove.Value,Chosen:a.ChosenMove.Value),
            WildBattleStatAtMinimumEvent c=>new(c.Sequence,c.Turn,"cap",(int)c.Actor,(int)c.Target),
            WildBattleStatAtMaximumEvent c=>new(c.Sequence,c.Turn,"cap",(int)c.Actor,(int)c.Target),
            _=>new Row(e.Sequence,e.Turn,"boundary")
        }).ToArray(),battle.EventIndex,affectedBehindSubstitute);
    }
    public static CampaignStatAnimationDispatch? AtResult(CampaignDoubleBattle battle,bool affectedBehindSubstitute=false)
    {
        if(battle.Stage!=CampaignDoubleBattleStage.Combat)return null;
        return Match(battle.Id,battle.Events.Select(e=>e switch
        {
            SourceStatChangedEvent s=>new Row(s.Sequence,s.Turn,"stat",s.Actor.Value,s.Target.Value,Stat:s.Stat,Before:s.Before,After:s.After),
            SourcePpCommandEvent p=>new(p.Sequence,p.Turn,"pp",p.Actor.Value,p.Target.Value,p.CurrentMove.Value,p.ChosenMove.Value,p.PpOwner.Value,Before:p.Before,After:p.After),
            SourceMoveUsedEvent m=>new(m.Sequence,m.Turn,"used",m.Actor.Value,m.Target.Value,m.Move.Value,Before:m.PpBefore,After:m.PpAfter),
            SourceAttackStringEvent a=>new(a.Sequence,a.Turn,"attack",a.Actor.Value,Move:a.CurrentMove.Value,Chosen:a.ChosenMove.Value),
            SourceStatAtMinimumEvent c=>new(c.Sequence,c.Turn,"cap",c.Actor.Value,c.Target.Value),
            SourceStatAtMaximumEvent c=>new(c.Sequence,c.Turn,"cap",c.Actor.Value,c.Target.Value),
            SourceMoveFailedEvent f=>new(f.Sequence,f.Turn,"failed",f.Actor.Value,f.Target.Value,f.Move.Value),
            SourceAbilityPreventedEvent f=>new(f.Sequence,f.Turn,"failed",f.Actor.Value,f.Target.Value,f.Move.Value),
            _=>new Row(e.Sequence,e.Turn,"boundary")
        }).ToArray(),battle.EventIndex,affectedBehindSubstitute);
    }
}
