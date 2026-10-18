using System.Collections.Immutable;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Options;
using Game.Domain.WildBattle;
namespace Game.Application;

public sealed record CampaignHeldItemAnimationDispatch(string BattleId,long Sequence,long ReceiptSequence,BattlerSlot Affected);

/// <summary>General_HeldItemEffect precedes the first effect receipt and its text.
/// Entry charge moves are captured before the command that produced this event batch.
/// The committed final Frame is deliberately not read by this projection.</summary>
public static class CampaignHeldItemAnimationPresentation
{
    private sealed record Row(long Sequence,int Turn,string Kind,int Slot=-1,int Other=-1,int Before=0,int After=0,
        string Reason="",MoveId? Move=null,string Item="",int Restored=0,WildBattleStat Stat=default,
        ImmutableArray<SourceItemStatChange>? Stats=null,bool Focus=false,string? Flavor=null,SourceItemPpRecovery? Pp=null);
    private static bool Prelude(Row r)=>r.Kind is "confusion" or "status" or "infatuation" or "stat" or "focus";
    private static bool ValidItem(Row r)=>r.Item.StartsWith("ITEM_",StringComparison.Ordinal)&&r.Item!="ITEM_NONE";
    private static bool ValidHp(Row r)=>r.Before>=0&&r.After>=r.Before&&r.Restored==r.After-r.Before;
    private static int GroupStart(Row[] rows,int end)
    {
        var item=rows[end];if(item.Kind!="consumed"||!ValidItem(item)||!ValidHp(item))return -1;
        if(item.Pp is {} pp&&(pp.Slot is <0 or >3||pp.Before<0||pp.After<=pp.Before))return -1;
        int start=end;
        bool Previous(Func<Row,bool> matches)=>start>0&&rows[start-1].Turn==item.Turn&&rows[start-1].Slot==item.Slot&&matches(rows[start-1]);
        if(item.Focus){if(!Previous(r=>r.Kind=="focus"))return -1;start--;}
        if(item.Stats is {} stats)
        {
            if(stats.IsDefaultOrEmpty||stats.Select(s=>s.Stat).Distinct().Count()!=stats.Length)return -1;
            for(int i=stats.Length-1;i>=0;i--)
            {
                var s=stats[i];
                if(s.Before is <0 or >12||s.After is <0 or >12||s.Before==s.After||!Previous(r=>r.Kind=="stat"&&r.Other==item.Slot&&r.Stat==s.Stat&&r.Before==s.Before&&r.After==s.After))return -1;
                start--;
            }
        }
        if(Previous(r=>r.Kind=="infatuation"&&r.Reason=="mental-herb"&&r.Before>=0&&r.After==-1))start--;
        if(Previous(r=>r.Kind=="status"&&r.Reason=="held-berry"&&r.Before!=0&&r.After==0))start--;
        if(Previous(r=>r.Kind=="confusion"&&(r.Reason is "held-berry" or "persim-berry"&&r.Before>0&&r.After==0||r.Reason=="flavor-berry"&&item.Flavor is not null&&r.Before==0&&r.After>0)))start--;
        return start==end&&item.Restored==0&&item.Pp is null?-1:start;
    }
    // Pure receipt boundaries are shared by direct-effect presentation. Visibility
    // and animation admission remain owned by AtResult below.
    internal static int ConsumedGroupStart(IReadOnlyList<SourceBattleEvent> events,int end)=>
        end>=0&&end<events.Count?GroupStart(events.Select(Read).ToArray(),end):-1;
    internal static int ConsumedGroupStart(IReadOnlyList<WildBattleEvent> events,int end)=>
        end>=0&&end<events.Count?GroupStart(events.Select(Read).ToArray(),end):-1;
    // An invalid same-owner consumption span cannot authenticate its stat
    // prelude as a move result. A foreign recipient's later item is independent.
    internal static bool HasMalformedConsumedPrelude(IReadOnlyList<SourceBattleEvent> events,int index)=>
        HasMalformedConsumedPrelude(events.Select(Read).ToArray(),index);
    internal static bool HasMalformedConsumedPrelude(IReadOnlyList<WildBattleEvent> events,int index)=>
        HasMalformedConsumedPrelude(events.Select(Read).ToArray(),index);
    private static bool HasMalformedConsumedPrelude(Row[] rows,int index)
    {
        if(index<0||index>=rows.Length)return false;
        var stat=rows[index];if(stat.Kind!="stat"||stat.Slot!=stat.Other)return false;
        int end=index+1;
        while(end<rows.Length&&rows[end].Turn==stat.Turn&&Prelude(rows[end]))end++;
        return end<rows.Length&&rows[end].Kind=="consumed"&&rows[end].Turn==stat.Turn&&rows[end].Slot==stat.Slot&&GroupStart(rows,end)<0;
    }
    // Shared Cmd_playanimation visibility policy for ordinary general effects.
    // Callers validate the entry snapshot's battle-layout width.
    internal static bool VisibleAtResult(IReadOnlyList<SourceBattleEvent> events,int index,BattlerSlot slot,IReadOnlyList<MoveId?> entry)=>
        index>=0&&index<events.Count&&slot.Value<entry.Count&&VisibleAt(events.Select(Read).ToArray(),index,slot.Value,entry);
    internal static bool VisibleAtResult(IReadOnlyList<WildBattleEvent> events,int index,BattlerSlot slot,IReadOnlyList<MoveId?> entry)=>
        index>=0&&index<events.Count&&slot.Value<entry.Count&&VisibleAt(events.Select(Read).ToArray(),index,slot.Value,entry);
    internal static bool? SpriteVisibleAtResult(IReadOnlyList<SourceBattleEvent> events,int index,BattlerSlot slot,IReadOnlyList<MoveId?> entry)=>
        index>=0&&index<events.Count&&slot.Value<entry.Count?SpriteVisibleAt(events.Select(Read).ToArray(),index,slot.Value,entry):null;
    internal static bool? SpriteVisibleAtResult(IReadOnlyList<WildBattleEvent> events,int index,BattlerSlot slot,IReadOnlyList<MoveId?> entry)=>
        index>=0&&index<events.Count&&slot.Value<entry.Count?SpriteVisibleAt(events.Select(Read).ToArray(),index,slot.Value,entry):null;
    private static bool VisibleAt(Row[] rows,int index,int slot,IReadOnlyList<MoveId?> entry)=>SpriteVisibleAt(rows,index,slot,entry)==true;
    private static bool? SpriteVisibleAt(Row[] rows,int index,int slot,IReadOnlyList<MoveId?> entry)
    {
        var charge=entry.ToArray();var known=Enumerable.Repeat(true,entry.Count).ToArray();
        var lastPp=new MoveId?[entry.Count];var ppTurn=Enumerable.Repeat(-1,entry.Count).ToArray();
        for(int i=0;i<index;i++)
        {
            var r=rows[i];if(r.Kind=="start"){Array.Clear(charge);Array.Fill(known,true);Array.Clear(lastPp);Array.Fill(ppTurn,-1);continue;}
            if(r.Kind is not("pp" or "charge" or "switch"))continue;
            if(r.Slot<0||r.Slot>=entry.Count)return null;
            if(r.Kind=="pp"){lastPp[r.Slot]=r.Move;ppTurn[r.Slot]=r.Turn;continue;}
            if(r.Kind=="switch"){charge[r.Slot]=lastPp[r.Slot]=null;known[r.Slot]=true;ppTurn[r.Slot]=-1;continue;}
            // Sleep/paralysis clear visibility at their explicit charge-clear receipt,
            // not at the earlier status change. Release also clears before the next PP.
            if(r.After==0){charge[r.Slot]=null;known[r.Slot]=true;}
            else if(r.After==1&&r.Reason=="move"&&lastPp[r.Slot] is {} move&&ppTurn[r.Slot]==r.Turn)
            {charge[r.Slot]=move;known[r.Slot]=true;}
            else known[r.Slot]=false;
        }
        return known[slot]?SourceChargeRules.SemiInvulnerable(charge[slot])==WildBattleSemiInvulnerable.None:null;
    }
    private static CampaignHeldItemAnimationDispatch? Match(string id,Row[] rows,int index,IReadOnlyList<MoveId?> entry)
    {
        if(index<0||index>=rows.Length)return null;int end=index;
        if(rows[index].Kind!="heal")
        {
            while(end<rows.Length&&Prelude(rows[end]))end++;
            if(end>=rows.Length||GroupStart(rows,end)!=index)return null;
        }
        var receipt=rows[end];int slot=receipt.Slot;
        if(slot<0||slot>=entry.Count||!ValidItem(receipt)||!ValidHp(receipt)||receipt.Kind=="heal"&&receipt.Restored<=0)return null;
        for(int i=1;i<=end;i++)if(rows[i].Sequence<=rows[i-1].Sequence)return null;
        if(!VisibleAt(rows,index,slot,entry))return null;
        return new(id,rows[index].Sequence,receipt.Sequence,new(slot));
    }
    private static void ValidateEntry(IReadOnlyList<MoveId?> entry,int count)
    {ArgumentNullException.ThrowIfNull(entry);if(entry.Count!=count)throw new ArgumentException("One pre-command charge move is required for each battle position.",nameof(entry));}
    public static CampaignHeldItemAnimationDispatch? AtResult(CampaignWildBattle battle,bool sourceRules,SourceBattleScene scene,IReadOnlyList<MoveId?> entryChargeMoves,bool affectedBehindSubstitute=false)
    {
        ValidateEntry(entryChargeMoves,2);
        if(!sourceRules||!CampaignResultAnimationPolicy.Allows(CampaignResultAnimationKind.HeldItem,scene)||affectedBehindSubstitute||battle.Phase!=CampaignBattlePhase.Events)return null;
        return Match(battle.Id,battle.Events.Select(Read).ToArray(),battle.EventIndex,entryChargeMoves);
    }
    public static CampaignHeldItemAnimationDispatch? AtResult(CampaignDoubleBattle battle,SourceBattleScene scene,IReadOnlyList<MoveId?> entryChargeMoves,bool affectedBehindSubstitute=false)
    {
        ValidateEntry(entryChargeMoves,4);
        if(!CampaignResultAnimationPolicy.Allows(CampaignResultAnimationKind.HeldItem,scene)||affectedBehindSubstitute||battle.Stage!=CampaignDoubleBattleStage.Combat)return null;
        return Match(battle.Id,battle.Events.Select(Read).ToArray(),battle.EventIndex,entryChargeMoves);
    }
    private static Row Read(SourceBattleEvent e)=>e switch
    {
        SourceHeldItemConsumedEvent x=>new(x.Sequence,x.Turn,"consumed",x.Side.Value,Before:x.HpBefore,After:x.HpAfter,Item:x.Item,Restored:x.HpRestored,Stats:x.StatChanges,Focus:x.FocusEnergyRaised==true,Flavor:x.Flavor,Pp:x.PpRecovery),
        SourceHeldItemHealEvent x=>new(x.Sequence,x.Turn,"heal",x.Actor.Value,Before:x.HpBefore,After:x.HpAfter,Item:x.Item,Restored:x.HpRestored),
        SourceStatChangedEvent x=>new(x.Sequence,x.Turn,"stat",x.Target.Value,x.Actor.Value,x.Before,x.After,Stat:x.Stat),
        SourceFocusEnergyEvent x=>new(x.Sequence,x.Turn,"focus",x.Side.Value),
        SourceStatusChangedEvent x=>new(x.Sequence,x.Turn,"status",x.Side.Value,Before:(int)x.Before,After:(int)x.After,Reason:x.Reason),
        SourceConfusionChangedEvent x=>new(x.Sequence,x.Turn,"confusion",x.Side.Value,Before:x.Before,After:x.After,Reason:x.Reason),
        SourceInfatuationChangedEvent x=>new(x.Sequence,x.Turn,"infatuation",x.Side.Value,Before:x.Before?.Value??-1,After:x.After?.Value??-1,Reason:x.Reason),
        SourcePpCommandEvent x=>new(x.Sequence,x.Turn,"pp",x.Actor.Value,Move:x.CurrentMove),
        SourceMtMoonVolatileEvent {Volatile:"called-charge"} x=>new(x.Sequence,x.Turn,"charge",x.Side.Value,Before:x.Before,After:x.After,Reason:x.Reason),
        SourceCalledPartySwitchedEvent x=>new(x.Sequence,x.Turn,"switch",x.Side.Value),
        SourcePlayerSwitchedEvent x=>new(x.Sequence,x.Turn,"switch",x.Slot.Value),
        SourcePlayerReplacedEvent x=>new(x.Sequence,x.Turn,"switch",x.Slot.Value),
        SourceStartedEvent=>new(e.Sequence,e.Turn,"start"),
        _=>new(e.Sequence,e.Turn,"boundary")
    };
    private static Row Read(WildBattleEvent e)=>e switch
    {
        WildBattleHeldItemConsumedEvent x=>new(x.Sequence,x.Turn,"consumed",(int)x.Side,Before:x.HpBefore,After:x.HpAfter,Item:x.Item,Restored:x.HpRestored,Stats:x.StatChanges,Focus:x.FocusEnergyRaised==true,Flavor:x.Flavor,Pp:x.PpRecovery),
        WildBattleHeldItemHealEvent x=>new(x.Sequence,x.Turn,"heal",(int)x.Actor,Before:x.HpBefore,After:x.HpAfter,Item:x.Item,Restored:x.HpRestored),
        WildBattleStatChangedEvent x=>new(x.Sequence,x.Turn,"stat",(int)x.Target,(int)x.Actor,x.Before,x.After,Stat:x.Stat),
        WildBattleFocusEnergyEvent x=>new(x.Sequence,x.Turn,"focus",(int)x.Side),
        WildBattleStatusChangedEvent x=>new(x.Sequence,x.Turn,"status",(int)x.Side,Before:(int)x.Before,After:(int)x.After,Reason:x.Reason),
        WildBattleConfusionChangedEvent x=>new(x.Sequence,x.Turn,"confusion",(int)x.Side,Before:x.Before,After:x.After,Reason:x.Reason),
        WildBattleInfatuationChangedEvent x=>new(x.Sequence,x.Turn,"infatuation",(int)x.Side,Before:x.Before is {} before?(int)before:-1,After:x.After is {} after?(int)after:-1,Reason:x.Reason),
        WildBattlePpCommandEvent x=>new(x.Sequence,x.Turn,"pp",(int)x.Actor,Move:x.CurrentMove),
        WildBattleMtMoonVolatileEvent {Volatile:"called-charge"} x=>new(x.Sequence,x.Turn,"charge",(int)x.Side,Before:x.Before,After:x.After,Reason:x.Reason),
        WildBattleCalledPartySwitchedEvent x=>new(x.Sequence,x.Turn,"switch",(int)x.Side),
        WildBattlePlayerSwitchedEvent or WildBattlePlayerReplacedEvent=>new(e.Sequence,e.Turn,"switch",0),
        WildBattleStartedEvent=>new(e.Sequence,e.Turn,"start"),
        _=>new(e.Sequence,e.Turn,"boundary")
    };
}
