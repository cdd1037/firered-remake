using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;

namespace Game.Application;

public sealed record CampaignWishHealingAnimationDispatch(string BattleId, long Sequence, long ReceiptSequence, BattlerSlot Affected);

/// <summary>The named general healing animation precedes the resolved text and
/// committed HP change. Its recipient is the current position, not the old caster.</summary>
public static class CampaignWishHealingAnimationPresentation
{
    private static void ValidateEntry(IReadOnlyList<MoveId?> entry, int count)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.Count != count) throw new ArgumentException("One pre-command charge move is required for each battle position.", nameof(entry));
    }
    public static CampaignWishHealingAnimationDispatch? AtResult(CampaignWildBattle battle, bool sourceRules,
        SourceBattleScene scene, IReadOnlyList<MoveId?> entryChargeMoves, bool affectedBehindSubstitute = false)
    {
        ValidateEntry(entryChargeMoves, 2);
        if (!sourceRules || battle.Phase != CampaignBattlePhase.Events || affectedBehindSubstitute ||
            !CampaignResultAnimationPolicy.Allows(CampaignResultAnimationKind.DelayedHealing, scene)) return null;
        var result = CampaignWishResolutionPresentation.At(battle.Events, battle.EventIndex);
        if (result is not { Kind: CampaignWishResolutionKind.Recovery } ||
            !CampaignHeldItemAnimationPresentation.VisibleAtResult(battle.Events, battle.EventIndex, result.Affected, entryChargeMoves)) return null;
        return new(battle.Id, battle.Events[battle.EventIndex].Sequence, result.ReceiptSequence, result.Affected);
    }
    public static CampaignWishHealingAnimationDispatch? AtResult(CampaignDoubleBattle battle, SourceBattleScene scene,
        IReadOnlyList<MoveId?> entryChargeMoves, bool affectedBehindSubstitute = false)
    {
        ValidateEntry(entryChargeMoves, 4);
        if (battle.Stage != CampaignDoubleBattleStage.Combat || affectedBehindSubstitute ||
            !CampaignResultAnimationPolicy.Allows(CampaignResultAnimationKind.DelayedHealing, scene)) return null;
        var result = CampaignWishResolutionPresentation.At(battle.Events, battle.EventIndex);
        if (result is not { Kind: CampaignWishResolutionKind.Recovery } ||
            !CampaignHeldItemAnimationPresentation.VisibleAtResult(battle.Events, battle.EventIndex, result.Affected, entryChargeMoves)) return null;
        return new(battle.Id, battle.Events[battle.EventIndex].Sequence, result.ReceiptSequence, result.Affected);
    }
}
