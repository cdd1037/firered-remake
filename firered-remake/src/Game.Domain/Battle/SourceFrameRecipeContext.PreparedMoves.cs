using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
namespace Game.Domain.Battle;
internal sealed partial class FrameRecipeContext
{
    internal override BattlerSlot PreparedReflectTarget(BattlerSlot original, BattlerSlot reflector) =>
        Frame.Sides[(int)reflector.Side ^ 1] is { FollowMeTimer: > 0, FollowMeTarget: { } follow } && Frame.Get(follow).Body?.Pokemon.CurrentHp > 0 ? follow : original;
    internal override int PreparedActionIndex => action.ActionOrderIndex;
    internal override int PreparedBattlerCount => Frame.BattlerCount;
    internal override void SetPreparedInvocation(SourceMoveInvocation value) => invocation = value;
    internal override void PreparedPp(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, bool attackString = true) => SpendFramePp(actor, target, move, attackString);
    internal override void PreparedNoEffect() => commandNoEffect = true;
    internal override MoveId? PreparedPreviousMove(BattlerSlot owner) => initial.Get(owner).Body!.LastUsedMove;
    internal override (int Physical, int Special) PreparedDamage(BattlerSlot actor) => Frame.Get(actor).Registers.Damage is { } damage && damage.Turn == Turn
        ? (damage.PhysicalDamage, damage.SpecialDamage) : (0, 0);
}
