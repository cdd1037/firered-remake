using Game.Domain.WildBattle;

namespace Game.Application;

public sealed partial class CampaignSession
{
    // Force-out has no extra wild text; the paired trainer replacement owns its
    // dragged-out message. Cleanup and applied-flinch markers are diagnostics.
    private static bool HiddenEarlyPartyEvent(WildBattleEvent e) => e is
        WildBattleForcedOutEvent or WildBattleFlinchEvent { Applied: true } or
        WildBattleConfusionChangedEvent { Reason: not ("move" or "countdown" or "snapped-out") } or
        WildBattleMultiHitEvent { ActualHits: 0 };
}
