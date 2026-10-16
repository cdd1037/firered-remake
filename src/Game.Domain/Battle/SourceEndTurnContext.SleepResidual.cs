using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal abstract partial class SourceEndTurnContext
{
    internal virtual bool YawnEnabled => true;
    internal abstract bool YawnUproarPreventsSleep(BattlerSlot slot);
    internal abstract void YawnCancelMultiTurn(BattlerSlot slot);
    internal void NightmareResidual(BattlerSlot slot)
    {
        var body = Get(slot);
        if (!body.Nightmare || body.Pokemon.CurrentHp == 0) return;
        if (!WildBattleStatus.IsSleeping(body.Status))
        {
            Set(slot, body with { Nightmare = false });
            Add(n => new SourceTowerVolatileEvent(n, Turn, slot, "nightmare", false, "awake"));
        }
        else SleepResidualDamage(slot, "nightmare");
    }
    internal void CurseResidual(BattlerSlot slot)
    {
        if (Get(slot).Cursed && Get(slot).Pokemon.CurrentHp > 0) SleepResidualDamage(slot, "curse");
    }
    private void SleepResidualDamage(BattlerSlot slot, string effect)
    {
        var body = Get(slot); int before = body.Pokemon.CurrentHp, damage = Math.Max(1, body.Pokemon.Stats.MaxHp / 4), after = Math.Max(0, before - damage);
        Set(slot, body with { Pokemon = body.Pokemon with { CurrentHp = after } });
        Add(n => new SourceTowerHpEvent(n, Turn, slot, effect, before, after, damage));
        if (after == 0) Faint(slot);
    }
    internal void YawnResidual(BattlerSlot slot)
    {
        var body = Get(slot);
        if (!YawnEnabled || body.Pokemon.CurrentHp == 0 || body.YawnTurnsRemaining == 0) return;
        int after = body.YawnTurnsRemaining - 1;
        Set(slot, body with { YawnTurnsRemaining = after });
        Add(n => new SourceYawnEvent(n, Turn, slot, body.YawnTurnsRemaining, after, "countdown"));
        if (after != 0 || body.Status != 0 || WildBattleEffective.Ability(body) is "ABILITY_VITAL_SPIRIT" or "ABILITY_INSOMNIA" || YawnUproarPreventsSleep(slot)) return;
        YawnCancelMultiTurn(slot);
        ChangeStatus(slot, (uint)((Draw(Purpose(slot, "yawn-sleep-duration"), 4) & 3) + 2), "yawn");
    }
}
