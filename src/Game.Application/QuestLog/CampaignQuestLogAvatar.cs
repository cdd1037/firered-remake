namespace Game.Application;

// Source field_player_avatar.c transitions and quest_log_player.c NORMAL/BIKE/SURF.
// The existing actor graphics identity carries appearance; no campaign snapshot is retained.
public static class CampaignQuestLogAvatar
{
    public static string Graphics(CampaignState state)
    {
        string mode=state.SurfEffect?.Phase==FieldSurfSourcePresentation.ShowMon?"NORMAL":
            state.IsSurfing||state.SurfEffect?.Phase==FieldSurfSourcePresentation.DismountJump?"SURF":
            state.IsCycling?"BIKE":"NORMAL";
        return "OBJ_EVENT_GFX_"+(state.PlayerGender==0?"RED":"GREEN")+"_"+mode;
    }
    public static bool IsSurf(string graphics)=>graphics is "OBJ_EVENT_GFX_RED_SURF" or "OBJ_EVENT_GFX_GREEN_SURF";
    public static bool IsBike(string graphics)=>graphics is "OBJ_EVENT_GFX_RED_BIKE" or "OBJ_EVENT_GFX_GREEN_BIKE";
    public static int Gender(string graphics,int defaultGender)=>graphics.StartsWith("OBJ_EVENT_GFX_GREEN_",StringComparison.Ordinal)?1:
        graphics.StartsWith("OBJ_EVENT_GFX_RED_",StringComparison.Ordinal)?0:defaultGender;
}
