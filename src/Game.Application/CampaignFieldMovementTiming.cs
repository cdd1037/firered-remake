using Game.Domain.World;

namespace Game.Application;

public sealed record CampaignFieldStepTiming(int Frames,string SourceAction);

// Shared live-field / recap timing. It observes an accepted displacement; it never
// chooses legality, advances RNG, or writes campaign state.
public static class CampaignFieldMovementTiming
{
    public const int LedgeFrames=32;
    public static CampaignFieldStepTiming Resolve(CampaignState state,CampaignCommand command,ICampaignCatalog? catalog,CampaignState? after=null)
    {
        var p=state.World.Player;var direction=command.Direction;
        string suffix=direction switch{Direction.North=>"up",Direction.South=>"down",Direction.West=>"left",_=>"right"};
        CampaignFieldStepTiming Step(string action)
        {
            string opcode=action=="player_run_slow"?"player_run_"+suffix+"_slow":action+"_"+suffix;
            return new(SourceDuration(opcode)??throw new InvalidOperationException("field_timing_action_unknown"),opcode);
        }
        int? Behavior(int x,int y)=>state.MapEdits?.TryGetValue(x+":"+y,out var edit)==true
            ?catalog?.MetatileDefinition(p.MapId,edit.SourceMetatileId)?.Behavior
            :(state.MapLayout is {} layout?catalog?.MapLayout(p.MapId,layout):null)?.SourceBehaviorAt(x,y)
                ??catalog?.Maps.GetValueOrDefault(p.MapId)?.SourceBehaviorAt(x,y);
        if(after is not null&&p.MapId==after.World.Player.MapId&&after.Motions.IsEmpty)
        {
            int dx=after.World.Player.X-p.X,dy=after.World.Player.Y-p.Y;
            if((dx,dy,Behavior(p.X+dx/2,p.Y+dy/2)) is (2,0,0x38) or (-2,0,0x39) or (0,-2,0x3a) or (0,2,0x3b))return Step("jump_2");
        }
        if(state.IsSurfing)return Step("walk_fast");
        bool stairs=direction is Direction.North or Direction.South&&Behavior(p.X,p.Y+(direction==Direction.South?1:0))==0x2a;
        if(state.IsCycling)
        {
            if(CyclingRoadRules.IsSlope(Behavior(p.X,p.Y)))
            {
                var input=CyclingRoadRules.Resolve(state.BicycleSlope,p.Facing,command.Text=="idle"?null:direction,command.Kind=="cycle_input"?command.Choice==1:command.RunHeld);
                suffix=input.Direction switch{Direction.North=>"up",Direction.South=>"down",Direction.West=>"left",_=>"right"};
                return Step(input.Action!=CyclingRoadAction.Move?"face":input.Ticks==4?"walk_faster":"walk");
            }
            // bike.c GetBikeCollisionAt returns COLLISION_COUNT for destination cracked ice.
            var (dx,dy)=direction switch{Direction.North=>(0,-1),Direction.South=>(0,1),Direction.West=>(-1,0),_=>(1,0)};
            return stairs||Behavior(p.X+dx,p.Y+dy)==FourIslandFieldSource.CrackedIce?Step("walk_fast"):Step("ride_water_current");
        }
        bool run=RunningAllowed(command.RunHeld,state.Script.Flags.GetValueOrDefault("FLAG_SYS_B_DASH"),catalog?.AllowsRunning(p.MapId)==true,Behavior(p.X,p.Y));
        return run?(stairs?Step("player_run_slow"):Step("player_run")):(stairs?Step("walk_slow"):Step("walk"));
    }

    public static int? SourceDuration(string action)
    {
        if(action.StartsWith("jump_2_",StringComparison.Ordinal))return LedgeFrames;
        if(action.StartsWith("face_",StringComparison.Ordinal))return 1;
        if(action.StartsWith("ride_water_current_",StringComparison.Ordinal))return 6;
        if(action.StartsWith("player_run_",StringComparison.Ordinal))return action.EndsWith("_slow",StringComparison.Ordinal)?11:8;
        if(action.StartsWith("walk_slow_",StringComparison.Ordinal))return 23;
        if(action.StartsWith("walk_faster_",StringComparison.Ordinal))return 4;
        if(action.StartsWith("walk_fast_",StringComparison.Ordinal))return 8;
        if(action is "walk_up" or "walk_down" or "walk_left" or "walk_right")return 16;
        return null;
    }

    public static bool RunningAllowed(bool held,bool shoes,bool mapAllows,int? behavior)=>held&&shoes&&mapAllows&&behavior is {} b&&b!=0x0a;

    // Source event_object_movement.c pixel schedules, shared by both renderers.
    public static double Fraction(string? action,int duration,double elapsedFrames)
    {
        int frame=Math.Clamp((int)Math.Floor(elapsedFrames+1e-9),0,duration);
        if(action=="cycle"||action?.StartsWith("ride_water_current_",StringComparison.Ordinal)==true)
        {
            ReadOnlySpan<int> pixels=[0,2,5,8,10,13,16];return pixels[Math.Min(frame,6)]/16d;
        }
        if(action=="walk-slow"||action?.StartsWith("walk_slow_",StringComparison.Ordinal)==true)return Math.Min(16,frame-frame/3)/16d;
        if(action=="run-slow"||action?.StartsWith("player_run_",StringComparison.Ordinal)==true&&action.EndsWith("_slow",StringComparison.Ordinal))return Math.Min(16,frame+frame/2)/16d;
        return Math.Clamp(elapsedFrames/duration,0,1);
    }
}
