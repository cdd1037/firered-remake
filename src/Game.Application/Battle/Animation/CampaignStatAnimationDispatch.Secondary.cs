using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
namespace Game.Application;

public static partial class CampaignStatAnimationPresentation
{
    // BattleScript_Effect*Hit sets these source effect bytes and then shares
    // EffectHit. The successful ChangeStatBuffs result owns StatUp/StatDown;
    // chance, abilities, caps and source RNG have already been committed.
    private static (WildBattleStat Stat,int Delta,bool User)? SecondaryStat(string effect)=>effect switch
    {
        "EFFECT_ATTACK_DOWN_HIT"=>(WildBattleStat.Attack,-1,false),
        "EFFECT_DEFENSE_DOWN_HIT"=>(WildBattleStat.Defense,-1,false),
        "EFFECT_SPEED_DOWN_HIT"=>(WildBattleStat.Speed,-1,false),
        "EFFECT_SPECIAL_ATTACK_DOWN_HIT"=>(WildBattleStat.SpecialAttack,-1,false),
        "EFFECT_SPECIAL_DEFENSE_DOWN_HIT"=>(WildBattleStat.SpecialDefense,-1,false),
        "EFFECT_ACCURACY_DOWN_HIT"=>(WildBattleStat.Accuracy,-1,false),
        "EFFECT_ATTACK_UP_HIT"=>(WildBattleStat.Attack,1,true),
        "EFFECT_DEFENSE_UP_HIT"=>(WildBattleStat.Defense,1,true),
        "EFFECT_OVERHEAT"=>(WildBattleStat.SpecialAttack,-2,true),
        _=>null
    };
    private static bool AllowsSelectedStatAlly(string move,IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves)=>
        moves is not null&&moves.TryGetValue(new(move),out var definition)&&SourceMoveDefinitionRules.IsValid(definition)&&definition.Id.Value==move&&
        definition.Target=="MOVE_TARGET_SELECTED";

    private static CampaignStatAnimationDispatch? Secondary(CampaignDoubleBattle battle,
        IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves,bool substitute)
    {
        if(moves is null||substitute||battle.Stage!=CampaignDoubleBattleStage.Combat||battle.EventIndex<0||battle.EventIndex>=battle.Events.Length||battle.Events[battle.EventIndex] is not (SourceStatChangedEvent or SourceStatAtMaximumEvent))return null;
        if(CampaignHeldItemAnimationPresentation.HasMalformedConsumedPrelude(battle.Events,battle.EventIndex))return null;
        return ReadSecondary(battle.Id,battle.Events.ToArray(),battle.Events.Select(GroupRead).ToArray(),battle.EventIndex,4,moves,ItemReceiptSequences(battle.Events));
    }
    private static CampaignStatAnimationDispatch? Secondary(CampaignWildBattle battle,
        IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves,bool substitute)
    {
        if(moves is null||substitute||battle.Phase!=CampaignBattlePhase.Events||battle.EventIndex<0||battle.EventIndex>=battle.Events.Length||battle.Events[battle.EventIndex] is not (SourceStatChangedEvent or SourceStatAtMaximumEvent))return null;
        if(CampaignHeldItemAnimationPresentation.HasMalformedConsumedPrelude(battle.Events,battle.EventIndex))return null;
        return ReadSecondary(battle.Id,battle.Events.ToArray(),battle.Events.Select(GroupRead).ToArray(),battle.EventIndex,2,moves,ItemReceiptSequences(battle.Events));
    }
    private static CampaignStatAnimationDispatch? ReadSecondary(string battle,SourceBattleEvent[] events,GroupRow[] rows,int index,int slots,
        IReadOnlyDictionary<MoveId,PokemonMoveDefinition> moves,HashSet<long> items)
    {
        var stat=rows[index];if(stat.Kind is not("stat" or "maximum")||items.Contains(stat.Sequence))return null;
        int start=index-1;while(start>=0&&!GroupBoundary(rows[start]))start--;
        if(start<1||rows[start].Kind!="used")return null;
        var used=rows[start];var attack=rows[start-1];
        var identity=CampaignAnimationInvocation.TryRead(events,start,slots,moves);
        if(identity is null||identity.AttackIndex!=start-1)return null;
        if(!moves.TryGetValue(new(used.Move),out var definition)||!SourceMoveDefinitionRules.IsValid(definition)||definition.Id.Value!=used.Move||
            definition.Power<=0||definition.SecondaryEffectChance is <1 or >100||definition.Target is not("MOVE_TARGET_SELECTED" or "MOVE_TARGET_BOTH"))return null;
        bool allStats=definition.Effect=="EFFECT_ALL_STATS_UP_HIT";
        (WildBattleStat Stat,int Delta,bool User)? secondary=allStats?(WildBattleStat.Attack,1,true):SecondaryStat(definition.Effect);
        if(secondary is not {} spec||definition.Target=="MOVE_TARGET_BOTH"&&spec.User)return null;
        if(attack.Kind!="attack"||attack.Actor!=used.Actor||attack.Move!=used.Move||attack.Chosen!=identity.ChosenMove.Value||attack.Turn!=used.Turn||
            used.Actor<0||used.Actor>=slots||used.Target<0||used.Target>=slots||used.Actor==used.Target||used.Slot is <0 or >3)return null;
        // The authenticated result is a source-ordered prefix. Later Rage, contact and
        // item commands cannot revoke it or lend another stat to this prefix.
        for(int i=start;i<=index;i++)if(rows[i].Turn!=used.Turn||rows[i].Sequence<=rows[i-1].Sequence)return null;
        bool spread=definition.Target=="MOVE_TARGET_BOTH";
        if(spread&&(used.Target&1)==(used.Actor&1))return null;
        var passes=Enumerable.Range(start+1,index-start-1).Where(i=>rows[i].Kind=="pp").ToArray();
        if(passes.Length<1||passes.Length>(spread&&slots==4?2:1)||passes[0]!=start+1)return null;
        var first=rows[passes[0]];
        for(int pass=0;pass<passes.Length;pass++)
        {
            var p=rows[passes[pass]];
            if(p.Actor!=used.Actor||p.Owner!=identity.PpOwner.Value||p.Move!=identity.CurrentMove.Value||p.Chosen!=identity.ChosenMove.Value||p.Slot!=identity.PpSlot||p.Slot!=used.Slot||p.Cost<1||p.Cost>(spread?3:2)||p.Before is <0 or >255||p.After is <0 or >255||
                p.Target!=(pass==0?used.Target:used.Target^2))return null;
            if(pass==0)
            {if(p.Before!=used.Before||p.After!=used.After||(identity.NoPpDeduct?p.After!=p.Before:p.Before<1||p.After!=Math.Max(0,p.Before-p.Cost)))return null;}
            // MOVEEND_NEXT_TARGET repeats ppreduce with NO_ATTACKSTRING. Cost
            // remains positive; the committed unchanged PP is the spend proof.
            else if(p.Before!=first.After||p.After!=p.Before)return null;
        }
        int cursor=passes[^1]+1;var pp=rows[passes[^1]];int target=pp.Target;
        if(!ReadSuccessfulDamagePrefix(rows,ref cursor,index,used.Actor,target,used.Move,!spec.User))return null;
        if(allStats)
        {
            // AllStatsUp chooses one wave before visiting the five stat commands.
            // Capped ALLOW_PTR commands still print their cap text: ChangeStatBuffs
            // returns WORKED and Cmd_statbuffchange advances past the jump operand.
            WildBattleStat[] order=[WildBattleStat.Attack,WildBattleStat.Defense,WildBattleStat.Speed,WildBattleStat.SpecialAttack,WildBattleStat.SpecialDefense];
            if(cursor!=index||cursor+order.Length>rows.Length)return null;
            int changed=0;WildBattleStat only=default;
            for(int i=0;i<order.Length;i++)
            {
                var result=rows[cursor+i];
                if(result.Turn!=used.Turn||result.Sequence!=rows[cursor+i-1].Sequence+1||
                    result.Actor!=used.Actor||result.Target!=used.Actor||result.Stat!=order[i]||items.Contains(result.Sequence))return null;
                if(result.Kind=="maximum")continue;
                if(result.Kind!="stat"||result.Before is <0 or >=12||result.After!=result.Before+1)return null;
                changed++;only=result.Stat;
            }
            if(changed==0)return null;
            for(int i=cursor+order.Length;i<rows.Length&&rows[i].Turn==used.Turn&&!GroupBoundary(rows[i]);i++)
                if(rows[i].Kind is "stat" or "maximum"&&rows[i].Actor==used.Actor&&rows[i].Target==used.Actor&&!items.Contains(rows[i].Sequence))return null;
            return new(battle,stat.Sequence,attack.Sequence,pp.Sequence,definition.SourceMoveId,new(used.Actor),changed>1?55:AnimationArgument(only,1));
        }
        if(cursor!=index||stat.Kind!="stat"||stat.Actor!=used.Actor||stat.Target!=(spec.User?used.Actor:target)||
            stat.Stat!=spec.Stat||stat.Before is <0 or >12||stat.After is <0 or >12||stat.After==stat.Before||stat.After!=Math.Clamp(stat.Before+spec.Delta,0,12))return null;
        return new(battle,stat.Sequence,attack.Sequence,pp.Sequence,definition.SourceMoveId,new(stat.Target),AnimationArgument(stat.Stat,spec.Delta));
    }
    // Shared source-ordered hit result prefix. A later secondary result may not
    // borrow a zero-loss immunity receipt as evidence of successful damage.
    private static bool ReadSuccessfulDamagePrefix(GroupRow[] rows,ref int cursor,int index,
        int actor,int target,string move,bool requireLivingTarget,bool criticalAtZero=false)
    {
        GroupRow? adjustment=null;
        if(cursor<index&&rows[cursor].Kind=="adjustment")adjustment=rows[cursor++];
        if(cursor>=index)return false;var hit=rows[cursor++];
        if(hit.Kind is not("hp" or "substitute")||hit.Actor!=actor||hit.Target!=target||hit.Move!=move||
            hit.Before<1||hit.Damage<0||hit.Percent is <85 or >100||hit.After!=Math.Max(0,hit.Before-hit.Damage)||requireLivingTarget&&(hit.Kind=="substitute"||hit.After==0))return false;
        if(adjustment is {} adjusted&&(hit.Kind!="hp"||adjusted.Target!=target||adjusted.Move!=move||
            adjusted.Before<hit.Before||adjusted.Before<adjusted.After||adjusted.After!=hit.Damage||hit.After!=1||
            adjusted.Endured==adjusted.HungOn||adjusted.HungOn&&adjusted.Detail!="ITEM_FOCUS_BAND"))return false;
        if(hit.Critical&&(hit.Damage>0||criticalAtZero&&adjustment is not null))
        {
            if(cursor>=index||rows[cursor] is not {Kind:"critical"} critical||critical.Actor!=actor||critical.Target!=target)return false;
            cursor++;
        }
        if(adjustment is {} survival)
        {
            if(cursor>=index)return false;var result=rows[cursor++];
            if(result.Target!=target||(survival.Endured?result.Kind!="endured":
                result.Kind!="survival"||result.Move!=move||result.Detail!=survival.Detail))return false;
        }
        // Endure / Focus Band at one HP may preserve a successful hit with zero
        // loss; its positive pre-adjustment damage and result receipt are required.
        if(hit.Damage==0&&adjustment is null)return false;
        if(cursor<index&&rows[cursor].Kind=="effectiveness")
        {
            var result=rows[cursor++];
            if(result.Actor!=actor||result.Target!=target||result.Move!=move||
                result.Detail is not("NotVeryEffective" or "SuperEffective"))return false;
        }
        return true;
    }

}
