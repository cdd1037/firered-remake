using Game.Domain.World;

namespace Game.Application;

// Presentation-only cue derived from an accepted movement. Never serialized into saves.
public sealed record CampaignFieldJumpCue(string ActorId,string Map,int FromX,int FromY,int ToX,int ToY,string Action,int Duration,double ElapsedFrames);
public enum CampaignFieldJumpLanding { None, Dust, TallGrass, ShallowWater, DeepWater }
public readonly record struct CampaignFieldShadowGeometry(int Width,int Height,int CenterX,int CenterY);

/// <summary>Ordinary overworld jump field effects, shared by live and recorded views.</summary>
public static class CampaignFieldJumpEffects
{
    // AnimateSprites invokes UpdateJumpImpactEffect before advancing the animation.
    // The end command marks age 24 ended; its callback removes the sprite at age 25.
    public const int DustLifetimeFrames=25;

    public static bool IsOrdinaryJump(string? action)=>Duration(action) is not null;
    public static int? Duration(string? action)=>action switch
    {
        "jump_2_down" or "jump_2_up" or "jump_2_left" or "jump_2_right"=>32,
        "jump_down" or "jump_up" or "jump_left" or "jump_right" or
        "jump_in_place_down" or "jump_in_place_up" or "jump_in_place_left" or "jump_in_place_right" or
        "jump_in_place_down_up" or "jump_in_place_up_down" or "jump_in_place_left_right" or "jump_in_place_right_left"=>16,
        // Surf mount/dismount special jumps have separate effect ownership.
        _=>null
    };

    public static CampaignFieldShadowGeometry ShadowGeometry(int sourceSize)=>sourceSize switch
    {
        0=>new(8,8,8,12),1=>new(16,8,8,12),2=>new(32,8,8,12),3=>new(64,32,8,0),
        _=>throw new ArgumentOutOfRangeException(nameof(sourceSize))
    };

    public static bool ShadowSurfaceAllowed(int? previousBehavior,int? currentBehavior)=>
        previousBehavior is {} previous&&currentBehavior is {} current&&
        !TallGrass(current)&&!FieldWaterRules.IsSurfableBehavior(previous)&&!FieldWaterRules.IsSurfableBehavior(current)&&
        !Reflective(previous)&&!Reflective(current);

    // Source logical coordinates lead the rendered pixels. InitJump shifts current
    // by one tile; callback 16 shifts the second tile of a far jump. Terrain can
    // destroy the shadow, but cannot restart it later in that same movement.
    public static bool ShadowVisible(CampaignFieldJumpCue cue,Func<int,int,int?> behaviorAt)
    {
        if(Duration(cue.Action) is not {} duration)return false;
        int completed=(int)Math.Floor(cue.ElapsedFrames+1e-7);
        if(completed<1||completed>=duration)return false;
        int firstX=duration==32?(cue.FromX+cue.ToX)/2:cue.ToX;
        int firstY=duration==32?(cue.FromY+cue.ToY)/2:cue.ToY;
        int? previous=behaviorAt(cue.FromX,cue.FromY),first=behaviorAt(firstX,firstY);
        if(!ShadowSurfaceAllowed(previous,first))return false;
        return duration!=32||completed<16||ShadowSurfaceAllowed(first,behaviorAt(cue.ToX,cue.ToY));
    }

    // This is the source selector order. FireRed's IsLongGrass always returns false.
    // Ice is reflective for shadows but still reaches the ordinary landing fallback.
    public static CampaignFieldJumpLanding LandingKind(int? destinationBehavior)=>destinationBehavior switch
    {
        null=>CampaignFieldJumpLanding.None,
        0x02 or 0xd1=>CampaignFieldJumpLanding.TallGrass,
        0x16=>CampaignFieldJumpLanding.ShallowWater,
        {} behavior when FieldWaterRules.IsSurfableBehavior(behavior)=>CampaignFieldJumpLanding.DeepWater,
        0x17=>CampaignFieldJumpLanding.ShallowWater,
        _=>CampaignFieldJumpLanding.Dust
    };

    public static int DustFrame(double elapsedSinceLanding)
    {
        int age=(int)Math.Floor(elapsedSinceLanding+1e-7);
        return age<0||age>=DustLifetimeFrames?-1:Math.Min(2,age/8);
    }

    public static int DustFrame(CampaignFieldJumpCue cue,int? destinationBehavior,bool landingEffectsDisabled=false)=>
        !landingEffectsDisabled&&Duration(cue.Action) is {} duration&&LandingKind(destinationBehavior)==CampaignFieldJumpLanding.Dust
            ?DustFrame(cue.ElapsedFrames-duration):-1;

    private static bool TallGrass(int behavior)=>behavior is 0x02 or 0xd1;
    private static bool Reflective(int behavior)=>behavior is 0x10 or 0x16 or 0x1a or 0x1b or 0x23;
}
