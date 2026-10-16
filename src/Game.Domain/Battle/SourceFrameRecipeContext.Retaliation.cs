using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal sealed partial class FrameRecipeContext
{
    internal override SourceDamageRegisters? RetaliationReceipt(BattlerSlot actor) => Frame.Get(actor).Registers.Damage;
    internal override void RetaliationTarget(BattlerSlot target) => invocation = invocation with { Target = target };
    internal override void RetaliationSuppressPressure() => invocation = invocation with {
        Flags = invocation.Flags | WildBattleInvocationFlags.PpNotAffectedByPressure };
    internal override BattlerSlot? RetaliationFollowMe(BattleSide side) =>
        Frame.Sides[(int)side] is { FollowMeTimer: > 0 } timer ? timer.FollowMeTarget : null;
}
