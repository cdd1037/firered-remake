namespace Game.Domain.Battle;

internal sealed partial class FrameRecipeContext
{
    internal bool LeechSeedSandstorm => SourceWeatherRules.HasSandstorm(Frame.Field);
    internal BattlerSlot? LeechSeedSource(BattlerSlot target) => Frame.Get(target).Registers.LeechSeedSource;
    internal void SetLeechSeedSource(BattlerSlot target, BattlerSlot actor)
    {
        var cell = Frame.Get(target);
        Frame = Frame with { Slots = Frame.Slots.SetItem(target.Value, cell with {
            Registers = cell.Registers with { LeechSeedSource = actor } }) };
    }
}

internal static class SourceResidualStatusRules
{
    internal static string? Validate(SourceBattleFrame frame, SourceBattleSlot cell)
    {
        var body = cell.Body!;
        // Source death clears status3; outgoing seed links deliberately survive a source KO.
        if (cell.Registers.LeechSeedSource == cell.Slot ||
            !frame.IsSourceAbsent(cell.Slot) && body.Pokemon.CurrentHp == 0 && (body.Rooted || cell.Registers.LeechSeedSource is not null))
            return "source_residual_status_state";
        return null;
    }
}
