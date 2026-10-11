using Game.Domain.WildBattle;
namespace Game.Domain.Battle;

internal abstract partial class SourceEndTurnContext
{
    internal void ElectricChargeResidual(BattlerSlot slot)
    {
        var body = Get(slot); int before = body.ChargeTurnsRemaining;
        if (before == 0) return;
        Set(slot, body with { ChargeTurnsRemaining = before - 1 });
        Add(n => new SourceMtMoonVolatileEvent(n, Turn, slot, "charge", before, before - 1, "countdown"));
    }
}
