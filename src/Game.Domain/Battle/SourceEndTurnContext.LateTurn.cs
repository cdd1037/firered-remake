using Game.Domain.WildBattle;
namespace Game.Domain.Battle;
internal sealed partial class FrameEndTurnContext
{
    internal bool PerishResidual(BattlerSlot slot)
    {
        if (Get(slot).CalledVolatile is not { PerishSongTimer: { } timer } value) return false;
        var next = value with { PerishSongTimer = timer == 0 ? null : timer - 1 };
        Set(slot, Get(slot) with { CalledVolatile = next == new WildBattleCalledVolatileState() ? null : next });
        Add(n => new SourceMtMoonVolatileEvent(n, Turn, slot, "perish-song", timer, timer == 0 ? -1 : timer - 1, timer == 0 ? "perished" : "countdown"));
        if (timer == 0 && Get(slot).Pokemon.CurrentHp != 0)
        {
            int hp = Get(slot).Pokemon.CurrentHp;
            Set(slot, Get(slot) with { Pokemon = Get(slot).Pokemon with { CurrentHp = 0 } });
            Add(n => new SourceHpChangedEvent(n, Turn, slot, slot, new("pret:MOVE_PERISH_SONG"), hp, 0, hp, false, 100));
            Faint(slot);
        }
        return true;
    }
}
