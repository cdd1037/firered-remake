using Game.Domain.Battle;
using Game.Domain.WildBattle;

namespace Game.Application;

public sealed partial class CampaignSession
{
    // Force-out has no extra wild text; the paired trainer replacement owns its
    // dragged-out message. Cleanup and applied-flinch markers are diagnostics.
    private static bool HiddenEarlyPartyEvent(SourceBattleEvent e) => e is
        SourceRewardAppliedEvent or SourceRewardLearningEvent or SourceOpponentRewardCompletedEvent or
        Game.Domain.Trainers.SourceTrainerStartedEvent or SourceTrainerOpponentSelectedEvent or SourceTrainerShiftOfferedEvent or
        SourceForcedOutEvent or SourceFlinchEvent { Applied: true } or
        SourceConfusionChangedEvent { Reason: not ("move" or "countdown" or "snapped-out") } or
        SourceMultiHitEvent { ActualHits: 0 };
}
