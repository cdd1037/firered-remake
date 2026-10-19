using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Encounters;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Randomness;
using Game.Domain.World;

namespace Game.Application;

/// <summary>Transient source field effect. Coordinates, owner and callback are correlated; never saved.</summary>
public sealed record CampaignSurfEffect(int PartyIndex,long InstanceId,int SourceSpeciesId,string Nickname,
    TilePosition Origin,TilePosition Destination,long OriginSteps,string Phase,string WaitId);

public static class FieldSurfSourcePresentation
{
    public const string ShowMon="show-mon",MountJump="mount-jump",DismountJump="dismount-jump";
    public const int ShowMonCueFrames=FieldCutSourcePresentation.ShowMonCueFrames,JumpCueFrames=32;
    // Exact extracted C task-order fixture: both tasks replace the completed jump with a
    // stationary face action before unlocking. Neither jump increments tookStep. The
    // existing stable input model does not retain the source subframe TURN_DIRECTION edge.
    public const int MountCompletedSteps=0,DismountCompletedSteps=0;

    /// <summary>The jump is drawn in the origin map until the correlated completion enters the destination.</summary>
    public static TilePosition JumpDestination(CampaignSurfEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        if(effect.Origin.MapId==effect.Destination.MapId)return effect.Destination;
        var (dx,dy)=effect.Destination.Facing switch
        {
            Direction.North=>(0,-1),Direction.South=>(0,1),Direction.East=>(1,0),Direction.West=>(-1,0),
            _=>throw new ArgumentOutOfRangeException(nameof(effect))
        };
        return effect.Destination with{MapId=effect.Origin.MapId,X=effect.Origin.X+dx,Y=effect.Origin.Y+dy};
    }
}

public sealed partial class CampaignSession
{
    public bool CanUseFieldSurf(int partyIndex)=>FieldSurfCommandError(State,partyIndex) is null;
    private static bool SurfWaterBehavior(int behavior)=>behavior is 0x10 or 0x15;
    private bool SurfRegionAllows(TilePosition p)=>(_content.IsRoute22StablePosition(p.MapId,p.X,p.Y))&&
        (_content.IsRoute3StablePosition(p.MapId,p.X,p.Y))&&(_content.IsSnorlaxStablePosition(p.MapId,p.X,p.Y));
    private bool IsSurfWater(CampaignState state, TilePosition p)=>SurfRegionAllows(p)&&EditedFieldMaps(state).TryGetValue(p.MapId,out var map)&&
        map.CellAt(p.X,p.Y) is {Collision:0,Elevation:1} c&&
        (SurfWaterBehavior(c.SourceBehavior)&&_content.TerrainAt(p.MapId,p.X,p.Y)==2||SeafoamFieldRules.IsWaterFloor(p.MapId)&&
            (SeafoamFieldRules.Surfable(c.SourceBehavior)||c.SourceBehavior==0x17))&&p.Elevation==1;
    private bool ActorOccupies(CampaignState state,int x,int y,int elevation)=>state.Actors.Values.Any(a=>a.Active&&a.X==x&&a.Y==y&&(a.Elevation==0||elevation==0||a.Elevation==elevation));
    private bool ActorOccupies(CampaignState state,MapId map,int x,int y,int elevation)=>map==state.World.Player.MapId
        ?ActorOccupies(state,x,y,elevation)
        :_content.MapDefinition(map).Actors.Any(a=>a.X==x&&a.Y==y&&(a.HideFlag is "0" or "0x0"||!state.Script.Flags.GetValueOrDefault(a.HideFlag))&&
            (a.Elevation==0||elevation==0||a.Elevation==elevation));
    private FieldHmCollision SurfFacingCollision(CampaignState state,TilePosition origin,Direction direction,out TilePosition destination,out ConnectedFieldCell? target)
    {
        var maps=EditedFieldMaps(state);var map=maps[origin.MapId];var (dx,dy)=Delta(direction);
        var here=map.CellAt(origin.X,origin.Y)??throw new InvalidDataException("surf_origin_cell_missing");
        target=null;destination=origin with{X=origin.X+dx,Y=origin.Y+dy,Facing=direction};
        if(ConnectedFieldRules.ResolveDestination(origin,direction,maps,out var resolved) is not null)return FieldHmCollision.Impassable;
        target=resolved!.Cell;
        destination=new(resolved.MapId,resolved.X,resolved.Y,target.Elevation is 0 or 15?origin.Elevation:target.Elevation,direction);
        return FieldHmSourceRules.StrengthTargetCollision(here,target,origin.Elevation,direction,
            ActorOccupies(state,destination.MapId,destination.X,destination.Y,origin.Elevation));
    }
    private string? FieldSurfCommandError(CampaignState state,int index)
    {
        if(!IsStableFieldForParty(state)||state.SurfEffect is not null||state.StrengthEffect is not null||state.FlashEffect is not null||state.CutEffect is not null||
            state.Fishing is not null||state.Pc is not null||state.TownMap is not null||state.Repel is not null)return "field_surf_event_active";
        if(state.IsSurfing)return "field_surf_already_active";
        if(index<0||index>=state.Party.Length)return "field_surf_party_index_invalid";
        if(!FieldHmSourceRules.Knows(state.Party[index],FieldHmSourceRules.SurfMove))return "field_surf_move_missing";
        if(!state.Script.Flags.GetValueOrDefault(FieldHmSourceRules.SurfBadge))return "field_surf_badge_required";
        var collision=SurfFacingCollision(state,state.World.Player,state.World.Player.Facing,out var destination,out var cell);
        if(cell?.SourceBehavior==FieldHmSourceRules.FastWaterBehavior)return "field_surf_fast_current";
        if(cell is null||!FieldHmSourceRules.FacesSurfableWater(state.World.Player.Elevation,collision,_content.TerrainAt(destination.MapId,destination.X,destination.Y)==2))return "field_surf_not_facing_water";
        if(!SurfRegionAllows(destination))return "field_surf_region_boundary_not_open";
        return !IsSurfWater(state,destination)?"field_surf_water_mode_unsupported":null;
    }
    private Decision<CampaignState> StartFieldSurf(CampaignState state,CampaignCommand command)
    {
        if(FieldSurfCommandError(state,command.PartyIndex) is {} error)return new(CommandStatus.Rejected,state,error);
        return new(CommandStatus.Accepted,BeginFieldSurfEffect(state,command.PartyIndex));
    }
    private Decision<CampaignState>? TryStartFieldSurfInteraction(CampaignState state)
    {
        if(state.IsSurfing)return null;
        int owner=FieldHmSourceRules.FirstPartyMember(state.Party,FieldHmSourceRules.SurfMove);
        if(owner==FieldHmSourceRules.NoPartyMember)return null;
        var p=state.World.Player;
        SurfFacingCollision(state,p,p.Facing,out _,out var target);
        if(target?.SourceBehavior==FieldHmSourceRules.FastWaterBehavior)
            return new(CommandStatus.Accepted,Pump(StartStory(state,"EventScript_CurrentTooFast")));
        if(FieldSurfCommandError(state,owner) is {} error)
            return error is "field_surf_water_mode_unsupported" or "field_surf_region_boundary_not_open"?new(CommandStatus.Rejected,state,error):null;
        return new(CommandStatus.Accepted,Pump(StartStory(state,"EventScript_UseSurf")));
    }
    private string FieldTravelMusic(CampaignState state)=>state.IsSurfing?SurfMusic(state.World.Player.MapId):_content.MapDefinition(state.World.Player.MapId).Music;
    private string SurfMusic(MapId map)=>_content.MapDefinition(map).RegionMapSection is "MAPSEC_KANTO_VICTORY_ROAD" or "MAPSEC_ROUTE_23" or "MAPSEC_INDIGO_PLATEAU"
        ?_content.MapDefinition(map).Music:"MUS_SURF";
    private CampaignState BeginFieldSurfEffect(CampaignState state,int index)
    {
        var p=state.World.Player;var collision=SurfFacingCollision(state,p,p.Facing,out var destination,out var cell);
        if(index<0||index>=state.Party.Length||!state.Script.Flags.GetValueOrDefault(FieldHmSourceRules.SurfBadge)||state.IsSurfing||state.SurfEffect is not null||
            cell is null||!FieldHmSourceRules.CanStartSurf(state.Party[index],true,false,p.Elevation,cell.SourceBehavior,collision,_content.TerrainAt(destination.MapId,destination.X,destination.Y)==2)||!IsSurfWater(state,destination))
            throw new InvalidDataException("field_surf_effect_context");
        var mon=state.Party[index];long sequence=checked(state.Script.EffectSequence+1);
        return state with{IsSurfing=true,IsCycling=false,SavedMusic="",Music=SurfMusic(p.MapId),Dialogue=null,SignMessage=null,MessagePointer=null,
            SurfEffect=new(index,mon.InstanceId,mon.Pokemon.SourceSpeciesId,mon.Nickname,p,destination,state.World.StepCount,FieldSurfSourcePresentation.ShowMon,"field-surf:"+sequence+":show-mon"),
            Script=state.Script with{Locked=true,EffectSequence=sequence},PresentationTicks=FieldSurfSourcePresentation.ShowMonCueFrames,PresentationEffects=[],Motions=[]};
    }
    private bool TryApplyFieldSurfEffect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;
        if(effect.Source.Path!="data/scripts/surf.inc")return false;
        var a=effect.Args;
        int Value(string t)=>int.TryParse(t,out int v)?v:state.Script.Variables.TryGetValue(t,out v)?v:_content.TryConstant(t,out v)?v:throw new InvalidDataException("surf_operand:"+t);
        bool Is(string t,string symbol)=>t==symbol||_content.TryConstant(symbol,out int n)&&Value(t)==n;
        switch(effect.Opcode)
        {
            case "checkpartymove":
                if(a.Length!=1||!Is(a[0],"MOVE_SURF"))throw new InvalidDataException("surf_check_move");
                int index=FieldHmSourceRules.FirstPartyMember(state.Party,FieldHmSourceRules.SurfMove);
                var vars=state.Script.Variables.SetItem("VAR_RESULT",index);
                if(index!=6)vars=vars.SetItem("VAR_0x8004",state.Party[index].Pokemon.SourceSpeciesId);
                next=state with{Script=state.Script with{Variables=vars}};return true;
            case "bufferpartymonnick":
                if(a.Length!=2||a[0]!="STR_VAR_1"||Value(a[1]) is var n&&(n<0||n>=state.Party.Length))throw new InvalidDataException("surf_nickname");
                next=state with{TextBuffers=state.TextBuffers.SetItem(a[0],state.Party[Value(a[1])].Nickname)};return true;
            case "setfieldeffectargument":
                if(a.Length!=2||Value(a[0])!=0||Value(a[1]) is var selected&&(selected<0||selected>=state.Party.Length||!FieldHmSourceRules.Knows(state.Party[selected],FieldHmSourceRules.SurfMove)))throw new InvalidDataException("surf_argument");
                next=state with{FieldEffectArguments=ImmutableSortedDictionary<int,int>.Empty.Add(0,Value(a[1]))};return true;
            case "dofieldeffect":
                if(a.Length!=1||!Is(a[0],"FLDEFF_USE_SURF")||state.FieldEffectArguments is not {} args||!args.TryGetValue(0,out int owner))throw new InvalidDataException("surf_effect_argument");
                next=BeginFieldSurfEffect(state,owner);return true;
            case "releaseall":
                next=state with{FieldEffectArguments=null,Dialogue=null,SignMessage=null,MessagePointer=null};return true;
            default:return false;
        }
    }
    private Decision<CampaignState>? TryStartFieldSurfDismount(CampaignState state,CampaignCommand command)
    {
        if(!state.IsSurfing||command.Kind!="move")return null;
        var origin=state.World.Player with{Facing=command.Direction};
        var collision=SurfFacingCollision(state,origin,command.Direction,out var destination,out var target);
        if(target is null||!FieldHmSourceRules.CanStopSurfing(true,collision,target.Elevation,ActorOccupies(state,destination.MapId,destination.X,destination.Y,3)))return null;
        // Only already-admitted ordinary shore is exposed. Doors, currents and source
        // special handlers stay explicit boundaries, even if their height is three.
        var validation=ConnectedFieldRules.Complete(state.World with{Player=destination,RulesVersion=ConnectedFieldRules.RulesVersion},
            new(new("surf:shore-validation"),state.World.Revision,ConnectedFieldAction.Idle,command.Direction),OccupiedFieldMaps(state),true,true,enableSpinTiles:true);
        if(validation.Status!=TransitionStatus.Accepted)return new(CommandStatus.Rejected,state,"field_surf_shore_mode_unsupported");
        if(!SurfRegionAllows(destination))return new(CommandStatus.Rejected,state,"field_surf_region_boundary_not_open");
        long sequence=checked(state.Script.EffectSequence+1);
        var effect=new CampaignSurfEffect(-1,0,0,"",origin,destination,state.World.StepCount,FieldSurfSourcePresentation.DismountJump,"field-surf:"+sequence+":dismount-jump");
        return new(CommandStatus.Accepted,SurfJump(state with{IsSurfing=false,SavedMusic="",Music=_content.MapDefinition(origin.MapId).Music,
            World=state.World with{Player=origin},Script=state.Script with{Locked=true,EffectSequence=sequence}},effect));
    }
    private CampaignState SurfJump(CampaignState state,CampaignSurfEffect effect)
    {
        string suffix=effect.Destination.Facing switch{Direction.North=>"up",Direction.South=>"down",Direction.West=>"left",_=>"right"};
        // Keep source map edits and actors authoritative while the jump owns input. A connected
        // destination becomes the world position only after this source-chart jump completes.
        var jumpDestination=FieldSurfSourcePresentation.JumpDestination(effect);
        var position=effect.Origin.MapId==effect.Destination.MapId?effect.Destination:effect.Origin;
        return state with{SurfEffect=effect,World=state.World with{Player=position,Revision=checked(state.World.Revision+1)},
            Motions=[new("LOCALID_PLAYER",[effect.Origin,jumpDestination],[(effect.Phase==FieldSurfSourcePresentation.DismountJump?"jump_special_with_effect_":"jump_special_")+suffix,"step_end"])],PresentationTicks=FieldSurfSourcePresentation.JumpCueFrames,PresentationEffects=[]};
    }
    private Decision<CampaignState> CompleteFieldSurf(CampaignState state,CampaignCommand command)
    {
        if(command.Kind!="field_surf_complete")return new(CommandStatus.Rejected,state,"field_surf_event_active");
        var effect=state.SurfEffect!;
        if(command.Text!=effect.WaitId)return new(CommandStatus.Rejected,state,"field_surf_callback_mismatch");
        if(!state.Script.Completed||state.Script.Wait is not null||state.World.StepCount!=effect.OriginSteps||state.World.Player.MapId!=effect.Origin.MapId)
            throw new InvalidDataException("field_surf_callback_context");
        if(effect.Phase is FieldSurfSourcePresentation.ShowMon or FieldSurfSourcePresentation.MountJump)
        {
            if(!state.IsSurfing||effect.PartyIndex<0||effect.PartyIndex>=state.Party.Length||state.Party[effect.PartyIndex].InstanceId!=effect.InstanceId||
                !FieldHmSourceRules.Knows(state.Party[effect.PartyIndex],FieldHmSourceRules.SurfMove)||!state.Script.Flags.GetValueOrDefault(FieldHmSourceRules.SurfBadge)||!IsSurfWater(state,effect.Destination))
                throw new InvalidDataException("field_surf_owner_context_changed");
            var collision=SurfFacingCollision(state,effect.Origin,effect.Origin.Facing,out var destination,out var cell);
            if(destination!=effect.Destination||!FieldHmSourceRules.FacesSurfableWater(effect.Origin.Elevation,collision,_content.TerrainAt(destination.MapId,destination.X,destination.Y)==2))
                throw new InvalidDataException("field_surf_mount_context_changed");
            if(effect.Phase==FieldSurfSourcePresentation.ShowMon)
            {
                if(state.World.Player!=effect.Origin)throw new InvalidDataException("field_surf_origin_changed");
                return new(CommandStatus.Accepted,SurfJump(state,effect with{Phase=FieldSurfSourcePresentation.MountJump,WaitId=effect.WaitId.Replace(":show-mon",":mount-jump",StringComparison.Ordinal)}));
            }
        }
        else if(effect.Phase==FieldSurfSourcePresentation.DismountJump)
        {
            var collision=SurfFacingCollision(state,effect.Origin,effect.Destination.Facing,out var destination,out var cell);
            if(state.IsSurfing||destination!=effect.Destination||cell is null||!FieldHmSourceRules.CanStopSurfing(true,collision,cell.Elevation,ActorOccupies(state,destination.MapId,destination.X,destination.Y,3)))
                throw new InvalidDataException("field_surf_dismount_context_changed");
        }
        else throw new InvalidDataException("field_surf_phase");
        bool connection=effect.Origin.MapId!=effect.Destination.MapId;
        if(state.World.Player!=(connection?effect.Origin:effect.Destination))throw new InvalidDataException("field_surf_destination_changed");
        // Both original task callbacks install a stationary face action before unlocking.
        // At the admitted stable input boundary no walking counters or encounter are replayed.
        // The source subframe TURN_DIRECTION encounter-only edge is recorded by the C fixture.
        var next=state with{SurfEffect=null,FieldEffectArguments=null,Script=state.Script with{Locked=false},Motions=[],PresentationTicks=0,PresentationEffects=[]};
        if(connection)
        {
            next=next with{World=next.World with{Player=effect.Destination},
                Exploration=ResetEncounterModifiers(next.Exploration!),
                FieldTransition=new(FieldTransitionCause.ConnectedEdge,FieldTransitionDeparture.Immediate,FieldTransitionArrival.Plain,
                    effect.Origin,effect.Destination,effect.Destination)};
            // Enter only after releasing the effect: destination scripts, objects, map music and
            // roamer transfer use the ordinary shared entry path, without a walking-step callback.
            next=next with{Music=FieldTravelMusic(next)};
            next=Pump(EnterSurfConnectionMap(next,effect.Destination.Facing));
        }
        return new(CommandStatus.Accepted,next);
    }
    private CampaignState EnterSurfConnectionMap(CampaignState state,Direction arrival,bool isWarp=false)
    {
        var next=EnterMap(state,arrival,isWarp?Game.Domain.Roaming.RoamerTransition.Warp:Game.Domain.Roaming.RoamerTransition.Connection);
        return state.IsSurfing?next with{IsSurfing=true,Music=SurfMusic(next.World.Player.MapId)}:next;
    }
    private string? ValidateFieldSurfState(CampaignState state)
    {
        if(state.SurfEffect is not null)return "save_field_surf_active";
        if(!state.IsSurfing)return null;
        if(!state.Script.Flags.GetValueOrDefault(FieldHmSourceRules.SurfBadge)||state.IsCycling||!IsSurfWater(state,state.World.Player))return "save_field_surf_position_invalid";
        return ActorOccupies(state,state.World.Player.X,state.World.Player.Y,state.World.Player.Elevation)?"save_field_surf_actor_collision":null;
    }
}
