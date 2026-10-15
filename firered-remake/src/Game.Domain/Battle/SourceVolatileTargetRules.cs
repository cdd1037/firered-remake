using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal static class SourceVolatileTargetRules
{
    internal static string? Validate(PokemonCatalog catalog, SourceBattleFrame frame, SourceBattleSlot cell)
    {
        var body = cell.Body!;
        if (body.BindTurnsRemaining is < 0 or > 6 ||
            (body.BindTurnsRemaining == 0) != (body.BindingMove is null) ||
            body.BindingMove is { } move && (!catalog.Moves.TryGetValue(move, out var definition) ||
                definition.Effect != "EFFECT_TRAP" || !SourceMoveDefinitionRules.IsValid(definition)))
            return "source_volatile_binding_state";
        foreach (var source in new[] { cell.Registers.WrappedBy, cell.Registers.MeanLookSource })
            if (source is { } slot && (slot == cell.Slot || body.Pokemon.CurrentHp == 0 || frame.Get(slot).Body?.Pokemon.CurrentHp is not > 0))
                return "source_volatile_source_state";
        return null;
    }
}
