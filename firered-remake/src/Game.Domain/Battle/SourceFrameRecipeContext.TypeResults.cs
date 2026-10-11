using Game.Domain.Pokemon;

namespace Game.Domain.Battle;

internal sealed partial class FrameRecipeContext
{
    internal override PokemonCatalog TypeResultCatalog => catalog;
    internal override void SourceTypeNoEffect(BattlerSlot target, bool clearHitType)
    {
        commandNoEffect = true;
        var cell = Frame.Get(target);
        Frame = Frame with { Slots = Frame.Slots.SetItem(target.Value, cell with {
            Body = cell.Body! with { LastLandedMove = new("pret:MOVE_NONE") },
            Registers = clearHitType ? cell.Registers with { History = (cell.Registers.History ?? new()) with {
                LastHitByType = "TYPE_NORMAL" } } : cell.Registers }) };
    }
}
