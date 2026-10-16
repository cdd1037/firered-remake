using Game.Domain.WildBattle;
namespace Game.Domain.Battle;
/// <summary>Read-only singles presentation projection. Never accepts commands or returns authoritative battle state.</summary>
public static class SourceBattleReadProjection
{
    public static WildBattleCombatant Combatant(SourceBattleFrame frame, BattlerSlot slot)
    {
        if (frame.Layout != SourceBattleLayout.Singles || slot.Value >= frame.BattlerCount || frame.Get(slot).Body is null)
            throw new ArgumentException("source_read_projection_slot");
        var cell = frame.Get(slot); return Combatant(cell.Body!, cell.Registers);
    }
    internal static WildBattleCombatant Combatant(WildBattleCombatant body, SourceSlotRegisters r)
    {
        return body with { LeechSeedSource = Side(r.LeechSeedSource), MeanLookSource = Side(r.MeanLookSource),
            LastHitBy = Side(r.LastHitBy), InfatuationSource = Side(r.InfatuationSource),
            SourceMoveHistory = r.History is { } history ? new() { RulesVersion = history.RulesVersion,
                LastPrintedMove = history.LastPrintedMove, LastResultingMove = history.LastResultingMove,
                LastHitByType = history.LastHitByType, LastTakenMove = history.LastTakenMove,
                LastTakenFromPlayer = history.LastTakenFrom[0], LastTakenFromOpponent = history.LastTakenFrom[1] } : null,
            CalledDamage = r.Damage is { } damage ? new() { RulesVersion = damage.RulesVersion, Turn = damage.Turn,
                PhysicalDamage = damage.PhysicalDamage, PhysicalSource = Side(damage.PhysicalSource),
                SpecialDamage = damage.SpecialDamage, SpecialSource = Side(damage.SpecialSource) } : null,
            CalledTurnState = r.Turn is { } turn ? new() { RulesVersion = turn.RulesVersion,
                SureHitTurnsRemaining = turn.SureHitTurnsRemaining, SureHitSource = Side(turn.SureHitSource),
                Lock = turn.Lock is { } locked ? new(locked.Kind, locked.Move, locked.PpSlot, locked.PpSlotMove,
                    Side(locked.Target), locked.TurnsRemaining) { StoredDamage = locked.StoredDamage,
                    StoredDamageSource = Side(locked.StoredDamageSource), UnableToUseMove = locked.UnableToUseMove } : null } : null };
    }
    private static WildBattleSide Side(BattlerSlot slot) => slot.Value < 2 ? (WildBattleSide)slot.Value : throw new ArgumentException("source_read_projection_single_slot");
    private static WildBattleSide? Side(BattlerSlot? slot) => slot is { } value ? Side(value) : null;
}
