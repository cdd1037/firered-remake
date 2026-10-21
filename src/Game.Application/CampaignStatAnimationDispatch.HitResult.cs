using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Application;

public static partial class CampaignStatAnimationPresentation
{
    internal static bool IsPreventedItemSecondary(IReadOnlyList<SourceBattleEvent> action,
        SourceMoveUsedEvent used,long sequence,IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves)
    {
        if(moves is null||!moves.TryGetValue(used.Move,out var definition)||
            !SourceMoveDefinitionRules.IsValid(definition)||definition.Id!=used.Move||
            definition.Effect is not("EFFECT_KNOCK_OFF" or "EFFECT_THIEF")||
            definition.Target!="MOVE_TARGET_SELECTED"||definition.Power<=0)return false;
        var rows=action.Select(GroupRead).ToArray();int index=Array.FindIndex(rows,r=>r.Sequence==sequence);
        if(index<2||rows[0].Kind!="pp"||rows[index] is not {Kind:"ability",Detail:"ABILITY_STICKY_HOLD"} result||
            result.Actor!=used.Actor.Value||result.Target!=used.Target.Value||result.Move!=used.Move.Value)return false;
        // The source item effect runs after the successful damage/result-message
        // prefix. Sticky Hold blocks the item mutation, not that attack animation.
        // Require the actual HP hit: a substitute hit cannot authorize this suffix.
        int hitIndex=rows[1].Kind=="adjustment"?2:1;
        if(hitIndex>=index||rows[hitIndex].Kind!="hp")return false;
        int cursor=1;
        return ReadSuccessfulDamagePrefix(rows,ref cursor,index,used.Actor.Value,used.Target.Value,used.Move.Value,
            requireLivingTarget:definition.Effect=="EFFECT_KNOCK_OFF")&&cursor==index;
    }

    internal static bool IsFailedToxicSecondary(IReadOnlyList<SourceBattleEvent> action,
        SourceMoveUsedEvent used,long sequence,IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves)=>
        IsFailedToxicSecondary(action.Select(GroupRead).ToArray(),GroupRead(used),sequence,moves);

    private static bool IsFailedToxicSecondary(GroupRow[] rows,GroupRow used,long sequence,
        IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves)
    {
        if(moves is null||!moves.TryGetValue(new(used.Move),out var definition)||
            !SourceMoveDefinitionRules.IsValid(definition)||definition.Id.Value!=used.Move||
            definition.Effect!="EFFECT_POISON_FANG"||definition.Target!="MOVE_TARGET_SELECTED"||
            definition.Power<=0||definition.SecondaryEffectChance is <1 or >100)return false;
        int index=Array.FindIndex(rows,r=>r.Sequence==sequence);
        if(index<2||rows[0].Kind!="pp"||rows[index] is not {Kind:"effectiveness",Detail:"NoEffect"} result||
            result.Actor!=used.Actor||result.Target!=used.Target||result.Move!=used.Move)return false;
        // Header, PP ownership and invocation sequence are authenticated by the
        // dedicated dispatcher. Only a complete successful hit prefix can make
        // this source effect's later NoEffect belong to toxic-type prevention.
        // ExecuteStatusHit preserves its critical result after a successful
        // zero-loss survival adjustment; the generic stat-secondary path does not.
        int cursor=1;
        return ReadSuccessfulDamagePrefix(rows,ref cursor,index,used.Actor,used.Target,used.Move,true,criticalAtZero:true)&&cursor==index;
    }
}
