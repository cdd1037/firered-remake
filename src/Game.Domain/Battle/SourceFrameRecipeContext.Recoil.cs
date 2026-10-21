using Game.Domain.Pokemon;

namespace Game.Domain.Battle;

internal sealed partial class FrameRecipeContext
{
    internal void RecordCrashDamage(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, int lost)
    {
        RecordCrashDamageCore(actor, target, move, lost);
        var cell = Frame.Get(actor);
        Frame = Frame with { Slots = Frame.Slots.SetItem(actor.Value, cell with { Registers = cell.Registers with {
            Damage = SourceMoveHistoryReducer.RecordDamage(cell.Registers.Damage, Turn, target, lost, move.Type) } }) };
    }
}
