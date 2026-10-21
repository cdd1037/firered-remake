using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal sealed partial class FrameRecipeContext
{
    internal bool TargetMarkSandstorm => SourceWeatherRules.HasSandstorm(Frame.Field);
    internal void SetSureHit(BattlerSlot target, BattlerSlot actor)
    {
        var cell = Frame.Get(target);
        var turn = cell.Registers.Turn ?? new(WildBattleRules.CalledMoveKernelRulesVersion, null, 0, null);
        Frame = Frame with { Slots = Frame.Slots.SetItem(target.Value, cell with { Registers = cell.Registers with {
            Turn = turn with { SureHitTurnsRemaining = 2, SureHitSource = actor } } }) };
    }
}
