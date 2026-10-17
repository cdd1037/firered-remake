using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.WildBattle;

namespace Game.Application;

// Expired is the silent shape with no adjacent outcome, not independent proof of zero HP.
public enum CampaignWishResolutionKind { Expired, Recovery, FullHp, Invalid }
public sealed record CampaignWishResolution(CampaignWishResolutionKind Kind, BattlerSlot Affected,
    int? CasterPartyIndex, long ReceiptSequence);

/// <summary>Committed Wish expiry is emitted before the native HP check. Only its
/// adjacent recovery/full-HP result enters WishComesTrue; a bare expiry is silent.</summary>
public static class CampaignWishResolutionPresentation
{
    // Party roster identity is stable across a positional replacement. This does
    // not inspect the committed frame's future HP, charge, form or substitute.
    public static bool CasterAvailable(SourceBattleFrame frame, BattlerSlot slot, int? member) =>
        member is >= 0 and < 6 && frame.Parties.Any(p => p.Side == slot.Side && member.Value < p.Members.Length);

    private sealed record Row(long Sequence, int Turn, string Kind, int Slot = -1, MoveId? Move = null,
        int Before = 0, int After = 0, string Reason = "", int? Caster = null, int Amount = 0, int Actor = -1);

    public static CampaignWishResolution? At(IReadOnlyList<SourceBattleEvent> events, int index) =>
        Match(events.Select(Read).ToArray(), index, 4);
    public static CampaignWishResolution? At(IReadOnlyList<WildBattleEvent> events, int index) =>
        Match(events.Select(Read).ToArray(), index, 2);

    private static CampaignWishResolution? Match(Row[] rows, int index, int count)
    {
        if (index < 0 || index >= rows.Length) return null;
        var r = rows[index];
        if (r.Kind != "delayed" || r.Move?.Value != "pret:MOVE_WISH" || r.Reason != "resolved" || r.Slot < 0 || r.Slot >= count) return null;
        CampaignWishResolution Result(CampaignWishResolutionKind kind, long? receipt = null) =>
            new(kind, new(r.Slot), r.Caster, receipt ?? r.Sequence);
        if (r.Before != 1 || r.After != 0 || r.Caster is not (>= 0 and < 6) || r.Sequence < 0 || r.Turn < 0)
            return Result(CampaignWishResolutionKind.Invalid);
        for (int i = 0; i < index; i++)
        {
            if (rows[i].Sequence >= rows[i + 1].Sequence) return Result(CampaignWishResolutionKind.Invalid);
            if (rows[i].Kind == "delayed" && rows[i].Move == r.Move && rows[i].Reason == "resolved" &&
                rows[i].Turn == r.Turn && rows[i].Slot == r.Slot) return Result(CampaignWishResolutionKind.Invalid);
        }
        if (index + 1 >= rows.Length) return Result(CampaignWishResolutionKind.Expired);
        var next = rows[index + 1];
        if (next.Sequence <= r.Sequence) return Result(CampaignWishResolutionKind.Invalid);
        if (next.Kind is not ("recovery" or "failed"))
        {
            if (next.Kind == "delayed" && next.Move == r.Move && next.Reason == "resolved" && next.Turn == r.Turn && next.Slot == r.Slot)
                return Result(CampaignWishResolutionKind.Invalid);
            return Result(CampaignWishResolutionKind.Expired);
        }
        if (next.Sequence != r.Sequence + 1 || next.Turn != r.Turn || next.Slot != r.Slot || next.Move != r.Move)
            return Result(CampaignWishResolutionKind.Invalid);
        if (next.Kind == "recovery")
            return Result(next.Before > 0 && next.After > next.Before && next.Amount == next.After - next.Before
                ? CampaignWishResolutionKind.Recovery : CampaignWishResolutionKind.Invalid, next.Sequence);
        return Result(next.Actor == r.Slot && next.Reason == "already-full-hp"
            ? CampaignWishResolutionKind.FullHp : CampaignWishResolutionKind.Invalid, next.Sequence);
    }

    private static Row Read(SourceBattleEvent e) => e switch {
        SourceCalledDelayedEvent x => new(x.Sequence, x.Turn, "delayed", x.Target.Value, x.Move, x.Before, x.After, x.Reason, x.SourcePartyIndex),
        SourceRecoveredEvent x => new(x.Sequence, x.Turn, "recovery", x.Side.Value, x.Move, x.HpBefore, x.HpAfter, Amount: x.Recovered),
        SourceMoveFailedEvent x => new(x.Sequence, x.Turn, "failed", x.Target.Value, x.Move, Reason: x.Reason, Actor: x.Actor.Value),
        _ => new(e.Sequence, e.Turn, "boundary")
    };
    private static Row Read(WildBattleEvent e) => e switch {
        WildBattleCalledDelayedEvent x => new(x.Sequence, x.Turn, "delayed", (int)x.Target, x.Move, x.Before, x.After, x.Reason, x.SourcePartyIndex),
        WildBattleRecoveredEvent x => new(x.Sequence, x.Turn, "recovery", (int)x.Side, x.Move, x.HpBefore, x.HpAfter, Amount: x.Recovered),
        WildBattleMoveFailedEvent x => new(x.Sequence, x.Turn, "failed", (int)x.Target, x.Move, Reason: x.Reason, Actor: (int)x.Actor),
        _ => new(e.Sequence, e.Turn, "boundary")
    };
}
