using Game.Domain.Options;
namespace Game.Application;
public enum CampaignResultAnimationKind { StatChange, HeldItem, DelayedHealing }
public static class CampaignResultAnimationPolicy
{
    // Cmd_playanimation exempts B_ANIM_STATS_CHANGE from NO_ANIMATIONS;
    // General_HeldItemEffect follows the ordinary option gate.
    public static bool Allows(CampaignResultAnimationKind kind,SourceBattleScene scene)=>kind switch
    {
        CampaignResultAnimationKind.StatChange=>scene is SourceBattleScene.On or SourceBattleScene.Off,
        CampaignResultAnimationKind.HeldItem or CampaignResultAnimationKind.DelayedHealing=>scene==SourceBattleScene.On,
        _=>false
    };
}
