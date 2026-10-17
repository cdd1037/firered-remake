using Game.Domain.Options;
namespace Game.Application;
public enum CampaignResultAnimationKind { StatChange, HeldItem, DelayedHealing, FormChange }
public static class CampaignResultAnimationPolicy
{
    // Cmd_playanimation exempts B_ANIM_STATS_CHANGE from NO_ANIMATIONS;
    // Castform uses its direct controller launch regardless of the scene option.
    // General_HeldItemEffect follows the ordinary option gate.
    public static bool Allows(CampaignResultAnimationKind kind,SourceBattleScene scene)=>kind switch
    {
        CampaignResultAnimationKind.StatChange or CampaignResultAnimationKind.FormChange=>scene is SourceBattleScene.On or SourceBattleScene.Off,
        CampaignResultAnimationKind.HeldItem or CampaignResultAnimationKind.DelayedHealing=>scene==SourceBattleScene.On,
        _=>false
    };
}
