using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal sealed partial class FrameRecipeContext
{
    internal SourceDamageRegisters? RetaliationReceipt(BattlerSlot actor) => Frame.Get(actor).Registers.Damage;
    internal void RetaliationTarget(BattlerSlot target) => invocation = invocation with { Target = target };
    internal void RetaliationSuppressPressure() => invocation = invocation with {
        Flags = invocation.Flags | WildBattleInvocationFlags.PpNotAffectedByPressure };
    internal BattlerSlot? RetaliationFollowMe(BattleSide side) =>
        Frame.Sides[(int)side] is { FollowMeTimer: > 0 } timer ? timer.FollowMeTarget : null;
}
