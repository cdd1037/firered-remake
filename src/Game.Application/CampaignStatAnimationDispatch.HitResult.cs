using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Application;

public static partial class CampaignStatAnimationPresentation
{
    internal static bool IsFailedToxicSecondary(IReadOnlyList<SourceBattleEvent> action,
        SourceMoveUsedEvent used,long sequence,IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves)=>
        IsFailedToxicSecondary(action.Select(GroupRead).ToArray(),GroupRead(used),sequence,moves);

    internal static bool IsFailedToxicSecondary(IReadOnlyList<WildBattleEvent> action,
        WildBattleMoveUsedEvent used,long sequence,IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves)=>
        IsFailedToxicSecondary(action.Select(GroupRead).ToArray(),GroupRead(used),sequence,moves);

    private static bool IsFailedToxicSecondary(GroupRow[] rows,GroupRow used,long sequence,
        IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? moves)
    {
        if(moves is null||!moves.TryGetValue(new(used.Move),out var definition)||
            !SourceMoveDefinitionRules.IsValid(definition)||definition.Id.Value!=used.Move||
            definition.SourceMoveId!=CampaignMoveAnimationPresentation.SupportedSourceMoveId(used.Move)||
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
