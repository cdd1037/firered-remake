using Game.Domain.Battle;
using Game.Domain.WildBattle;
namespace Game.Application;

public static partial class CampaignMoveAnimationPresentation
{
    private sealed record EffectReceipt(long Sequence,int Actor,int Target,string Kind,WildBattleStat Stat=default,int Before=0,int After=0,string Detail="",string Move="");
    private static EffectReceipt? EffectRow(SourceBattleEvent e)=>e switch
    {
        SourceStatChangedEvent s=>new(s.Sequence,s.Actor.Value,s.Target.Value,"stat",s.Stat,s.Before,s.After),
        SourceStatAtMaximumEvent s=>new(s.Sequence,s.Actor.Value,s.Target.Value,"maximum",s.Stat),
        SourceStatAtMinimumEvent s=>new(s.Sequence,s.Actor.Value,s.Target.Value,"minimum",s.Stat),
        SourceConfusionChangedEvent s=>new(s.Sequence,-1,s.Side.Value,"confusion",Before:s.Before,After:s.After,Detail:s.Reason),
        SourceAbilityPreventedEvent s=>new(s.Sequence,s.Actor.Value,s.Target.Value,"ability",Detail:s.Ability,Move:s.Move.Value),
        SourceMoveFailedEvent s=>new(s.Sequence,s.Actor.Value,s.Target.Value,"failed",Detail:s.Reason,Move:s.Move.Value),
        SourceMoveMissedEvent s=>new(s.Sequence,s.Actor.Value,s.Target.Value,"missed",Move:s.Move.Value),
        _=>null
    };
    private static EffectReceipt? EffectRow(WildBattleEvent e)=>e switch
    {
        WildBattleStatChangedEvent s=>new(s.Sequence,(int)s.Actor,(int)s.Target,"stat",s.Stat,s.Before,s.After),
        WildBattleStatAtMaximumEvent s=>new(s.Sequence,(int)s.Actor,(int)s.Target,"maximum",s.Stat),
        WildBattleStatAtMinimumEvent s=>new(s.Sequence,(int)s.Actor,(int)s.Target,"minimum",s.Stat),
        WildBattleConfusionChangedEvent s=>new(s.Sequence,-1,(int)s.Side,"confusion",Before:s.Before,After:s.After,Detail:s.Reason),
        WildBattleAbilityPreventedEvent s=>new(s.Sequence,(int)s.Actor,(int)s.Target,"ability",Detail:s.Ability,Move:s.Move.Value),
        WildBattleMoveFailedEvent s=>new(s.Sequence,(int)s.Actor,(int)s.Target,"failed",Detail:s.Reason,Move:s.Move.Value),
        WildBattleMoveMissedEvent s=>new(s.Sequence,(int)s.Actor,(int)s.Target,"missed",Move:s.Move.Value),
        _=>null
    };
    // These source roots animate before their individual stat/confusion commands.
    // A later protection receipt is evidence that execution reached that point,
    // rather than a reason to suppress an animation that already occurred.
    private static long? MixedEffectResult(int move,int actor,int target,string symbol,long pp,IEnumerable<EffectReceipt?> input)
    {
        var rows=input.OfType<EffectReceipt>().ToArray();
        if(rows.Length==0||rows.Any(r=>r.Sequence<=pp||r.Target!=target||r.Actor!=-1&&r.Actor!=actor||r.Move.Length!=0&&r.Move!=symbol))return null;
        if(move==207)
        {
            if(rows.Length!=2)return null;
            var stat=rows[0];var confusion=rows[1];
            bool changed=stat.Kind=="stat"&&stat.Stat==WildBattleStat.Attack&&stat.Before is >=0 and <12&&stat.After==Math.Min(12,stat.Before+2);
            bool capped=stat.Kind=="maximum"&&stat.Stat==WildBattleStat.Attack;
            bool applied=confusion.Kind=="confusion"&&confusion.Before==0&&confusion.After is >=2 and <=5&&confusion.Detail=="move";
            bool prevented=confusion.Kind=="ability"&&confusion.Detail=="ABILITY_OWN_TEMPO"||confusion.Kind=="failed"&&confusion.Detail is "safeguard" or "already-confused";
            return !(capped&&confusion.Kind=="failed"&&confusion.Detail=="already-confused")&&(changed||capped)&&(applied||prevented)&&confusion.Sequence>stat.Sequence?stat.Sequence:null;
        }
        if(move==321)
        {
            if(rows.Length==1&&((rows[0].Kind=="failed"&&rows[0].Detail=="mist")||(rows[0].Kind=="ability"&&rows[0].Detail is "ABILITY_CLEAR_BODY" or "ABILITY_WHITE_SMOKE")))return rows[0].Sequence;
            if(rows.Length!=2||rows[1].Sequence<=rows[0].Sequence)return null;
            bool Stat(EffectReceipt r,WildBattleStat expected)=>r.Stat==expected&&(r.Kind=="stat"&&r.Before is >0 and <=12&&r.After==r.Before-1||r.Kind=="minimum");
            bool attack=Stat(rows[0],WildBattleStat.Attack)||rows[0].Kind=="ability"&&rows[0].Detail=="ABILITY_HYPER_CUTTER";
            return attack&&Stat(rows[1],WildBattleStat.Defense)&&!(rows[0].Kind=="minimum"&&rows[1].Kind=="minimum")?rows[0].Sequence:null;
        }
        return null;
    }
}
