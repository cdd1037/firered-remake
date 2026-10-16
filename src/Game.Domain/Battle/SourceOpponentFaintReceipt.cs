namespace Game.Domain.Battle;

/// <summary>Single battle-local source result register, overwritten on each opponent faint.</summary>
public sealed record SourceOpponentFaintReceipt(int Species, BattlerSlot? ScriptAttacker);

public static class SourceOpponentFaintResults
{
    public static SourceBattleFrame Record(SourceBattleFrame frame, BattlerSlot fainted, BattlerSlot? scriptAttacker)
    {
        if (fainted.Side != BattleSide.Opponent) return frame;
        var member=frame.Get(fainted).Member!.Value;
        return frame with { LastOpponentFaint = new(frame.Party(member.Side).Members[member.Index].Pokemon.SourceSpeciesId,scriptAttacker) };
    }
}
