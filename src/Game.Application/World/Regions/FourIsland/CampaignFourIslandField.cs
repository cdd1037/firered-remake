using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Encounters;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Application;

public sealed record CampaignFourIslandMovement(FourIslandMovementState Movement, FourIslandFieldCallbacks PendingCallbacks);

public sealed partial class CampaignSession
{
    private static bool IcefallFloor(MapId map) => map == FourIslandFieldSource.FirstFloor || map == FourIslandFieldSource.Basement;
    private static ImmutableSortedDictionary<string,FieldMetatileEdit> IcefallEdits(int mask) => IcefallIceRules.StableCracks(mask)
        .ToImmutableSortedDictionary(c=>c.X+":"+c.Y,c=>new FieldMetatileEdit(c.X,c.Y,c.MetatileId,c.Collision,c.Elevation));
    private CampaignState FourIslandMapEntry(CampaignState state)
    {
        var map=state.World.Player.MapId;
        var ice=map==FourIslandFieldSource.FirstFloor?IcefallIceRules.NewMap(map):null;
        if(ice is not null)
        {
            var tick=IcefallIceRules.Tick(ice,new(new("ice:entry"),ice.Revision,state.World.Player.X,state.World.Player.Y),_content.Maps[map]);
            if(tick.Status!=TransitionStatus.Accepted)throw new InvalidDataException(tick.Diagnostic?.Code);
            ice=tick.State;
        }
        return state with { IcefallIce=ice, FourIslandMovement=null,
            FourIslandMovementDirection=FourIslandMap(map)?state.World.Player.Facing:null };
    }
    private string? ValidateFourIslandField(CampaignState state)
    {
        if(state.FourIslandMovement is not null)return "save_four_island_movement_active";
        if(!FourIslandMap(state.World.Player.MapId))return state.IcefallIce is not null||state.FourIslandMovementDirection is not null?"save_four_island_field_map":null;
        if(state.FourIslandMovementDirection is not {} d||!Enum.IsDefined(d))return "save_four_island_movement_direction";
        if(state.World.Player.MapId!=FourIslandFieldSource.FirstFloor)return state.IcefallIce is null?null:"save_icefall_callback_map";
        var ice=state.IcefallIce;
        if(ice is null||ice.MapId!=state.World.Player.MapId||!ice.CanSave||ice.PreviousX!=state.World.Player.X||ice.PreviousY!=state.World.Player.Y)
            return "save_icefall_callback_state";
        try { _=IcefallIceRules.ReloadStable(ice); } catch(ArgumentException){return "save_icefall_callback_state";}
        int mask=0;for(int n=1;n<=9;n++)if(state.Script.Flags.GetValueOrDefault("FLAG_TEMP_"+n))mask|=1<<(n-1);
        if(mask!=ice.VisitedMask||!(state.MapEdits??ImmutableSortedDictionary<string,FieldMetatileEdit>.Empty).SequenceEqual(IcefallEdits(mask))||
            state.Script.Variables.GetValueOrDefault("VAR_TEMP_1")!=0)return "save_icefall_crack_flags";
        return null;
    }
    private CampaignState CompleteIcefallCallbacks(CampaignState state)
    {
        if(state.World.Player.MapId!=FourIslandFieldSource.FirstFloor)return state;
        var ice=state.IcefallIce??throw new InvalidDataException("icefall_callback_missing");
        // The public move is a COMPLETED tile action (at least six updates). The
        // original callback observes the destination then completes its five-update
        // delay before the next player input / encounter callback can execute.
        for(int n=0;n<6;n++)
        {
            var tick=IcefallIceRules.Tick(ice,new(new("ice:callback:"+ice.Revision),ice.Revision,state.World.Player.X,state.World.Player.Y),_content.Maps[state.World.Player.MapId]);
            if(tick.Status!=TransitionStatus.Accepted)throw new InvalidDataException(tick.Diagnostic?.Code);
            ice=tick.State;state=state with{IcefallIce=ice};
            foreach(var e in tick.Events)
            {
                if(e.TemporaryFlag is {} flag)state=state with{Script=state.Script with{Flags=state.Script.Flags.SetItem("FLAG_TEMP_"+flag,true)}};
                if(e.Mutation is {} m)state=state with{MapEdits=(state.MapEdits??ImmutableSortedDictionary<string,FieldMetatileEdit>.Empty)
                    .SetItem(m.X+":"+m.Y,new(m.X,m.Y,m.MetatileId,m.Collision,m.Elevation))};
            }
            if(ice.FallRequested)return state with{Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_TEMP_1",1)}};
            if(ice.Phase==IcefallCallbackPhase.ObserveDestination)break;
        }
        return state;
    }
    private IFieldMap IcefallRawMap(CampaignState state) => new FourIslandMapAdapter(_content.Maps[state.World.Player.MapId],state.MapEdits,_content,false);
    private IReadOnlyDictionary<MapId,IFieldMap> FourIslandMovementMaps(IReadOnlyDictionary<MapId,IFieldMap> maps,CampaignState state)
    {
        return new CampaignMapView<IFieldMap>(maps,(id,map)=>FourIslandMap(id)?new FourIslandMapAdapter(map,
            id==state.World.Player.MapId?state.MapEdits:null,_content,true):map);
    }
    private sealed class FourIslandMapAdapter(IFieldMap source,ImmutableSortedDictionary<string,FieldMetatileEdit>? edits,ICampaignCatalog content,bool walking):IFieldMap
    {
        public MapId Id=>source.Id;public int Width=>source.Width;public int Height=>source.Height;public ImmutableArray<FieldWarp> FieldWarps=>source.FieldWarps;
        private int? Raw(int x,int y)=>edits?.TryGetValue(x+":"+y,out var e)==true?content.MetatileDefinition(Id,e.SourceMetatileId)!.Behavior:source.SourceBehaviorAt(x,y);
        public int? SourceEncounterTypeAt(int x,int y)=>edits?.TryGetValue(x+":"+y,out var e)==true?content.MetatileDefinition(Id,e.SourceMetatileId)!.EncounterType:source.SourceEncounterTypeAt(x,y);
        // Keep ordinary cave/ladder ground raw so the existing field handler
        // retains source elevation-0 compatibility when leaving a warp tile.
        public int? SourceBehaviorAt(int x,int y)=>walking&&Raw(x,y) is 0x16 or 0x23 or 0x26 or 0x27?0:Raw(x,y);
        public TileCell? GetTile(int x,int y)
        {
            var t=source.GetTile(x,y);if(edits?.TryGetValue(x+":"+y,out var e)==true)t=new(e.Collision!=0,e.Elevation,t?.Behavior??TileBehavior.Unsupported);
            return walking&&t is {Blocked:false}&&Raw(x,y) is 0x08 or 0x16 or 0x23 or 0x26 or 0x27 or 0x2a?t with{Behavior=TileBehavior.Floor}:t;
        }
    }
    private IReadOnlyDictionary<MapId,ConnectedFieldMap> FourIslandWalkingMaps(IReadOnlyDictionary<MapId,ConnectedFieldMap> maps)
    {
        return new CampaignMapView<ConnectedFieldMap>(maps,(id,map)=>FourIslandMap(id)?map with{Cells=map.Cells.Select(c=>
            c.Collision==0&&c.SourceBehavior is 0x16 or 0x23 or 0x26 or 0x27?c with{SourceBehavior=0}:c).ToImmutableArray()}:map);
    }
    private Decision<CampaignState>? TryFourIslandFieldStep(CampaignState state,CampaignCommand command)
    {
        if(command.Kind!="move")return null;
        var p=state.World.Player;var(dx,dy)=Delta(command.Direction);var raw=IcefallRawMap(state);
        if(p.MapId==FourIslandFieldSource.Entrance&&state.IsSurfing&&p.Y==13&&command.Direction==Direction.South&&raw.SourceBehaviorAt(p.X,p.Y+1)==FourIslandFieldSource.Waterfall)
        {
            var descent=FourIslandForcedMovementRules.StartDescent(state.World,new(new("four:descend:"+state.World.Revision),state.World.Revision),raw,true,occupied:(x,y,e)=>ActorOccupies(state,x,y,e));
            return AcceptFourIslandMovement(state,descent);
        }
        if(!IcefallFloor(p.MapId))return null;
        int? here=raw.SourceBehaviorAt(p.X,p.Y),front=raw.SourceBehaviorAt(p.X+dx,p.Y+dy);
        if(here is >=0x60 and <=0x71||front is >=0x60 and <=0x71)return null;
        var step=FourIslandForcedMovementRules.CompletePlayerStep(state.World,new(new("four:step:"+state.World.Revision),state.World.Revision),raw,command.Direction,
            state.IsCycling?FourIslandTravelMode.Bicycle:FourIslandTravelMode.Walk,occupied:(x,y,e)=>ActorOccupies(state,x,y,e));
        if(step.Status!=TransitionStatus.Accepted)return new(step.Status==TransitionStatus.Rejected?CommandStatus.Rejected:CommandStatus.Fault,state,step.Diagnostic?.Code);
        var moved=RefreshActors(state with{World=step.State,FourIslandMovementDirection=command.Direction});
        moved=CompleteIcefallCallbacks(moved);
        if(moved.IcefallIce?.FallRequested==true)return new(CommandStatus.Accepted,Pump(moved with{World=moved.World with{StepCount=state.World.StepCount}}));
        moved=ApplyFourIslandCallbacks(moved,step.Callbacks);
        if(moved.World.Player!=p&&raw.SourceBehaviorAt(moved.World.Player.X,moved.World.Player.Y)==FourIslandFieldSource.Ice&&moved.Script.Completed&&moved.Script.Wait is null&&moved.WildBattle is null)
        {
            var slip=FourIslandForcedMovementRules.StartSlip(moved.World,new(new("four:slip:"+moved.World.Revision),moved.World.Revision),IcefallRawMap(moved),command.Direction,occupied:(x,y,e)=>ActorOccupies(moved,x,y,e));
            return AcceptFourIslandMovement(moved,slip);
        }
        return new(CommandStatus.Accepted,moved);
    }
    private CampaignState ApplyFourIslandCallbacks(CampaignState state,FourIslandFieldCallbacks callbacks)
    {
        if(!callbacks.TookStep)return state;
        state=CheckForestTrainerSight(RefreshActors(state));
        if(state.ForestTrainerBattle is not null||state.DoubleBattle is not null)return state;
        var before=state.Script.Revision;state=Pump(state);
        if(state.Script.Revision!=before||!state.Script.Completed||state.Script.Wait is not null)return state;
        var p=state.World.Player;
        var coord=_content.MapDefinition(p.MapId).CoordTriggers.FirstOrDefault(t=>t.X==p.X&&t.Y==p.Y&&(t.Elevation==0||t.Elevation==p.Elevation)&&state.Script.Variables.GetValueOrDefault(t.Variable)==t.Expected);
        if(coord is not null)return Pump(StartStory(state,coord.ScriptId));
        state=ApplyRenewableHiddenItemWalkingStep(state);
        if(callbacks.AdvanceFriendship)state=ApplyWalkingFriendship(state);
        if(callbacks.AdvancePoison)
        {
            state=ApplyVsSeekerWalkingStep(state,out bool vsExpired);
            if(vsExpired)return state;
            state=ApplyFieldPoisonWalkingStep(state);
        }
        if(state.FieldPoison is null&&callbacks.AdvancePoison)state=ApplyDayCareWalkingStep(state);
        if(state.FieldPoison is null&&state.Script.Completed&&callbacks.AdvanceRepel)state=ApplyRepelWalkingStep(state);
        if(state.FieldPoison is null&&state.Repel is null&&state.Script.Completed&&state.Script.Wait is null&&callbacks.CheckStandardWildEncounter)
            state=CheckFieldEncounter(state,EncounterCheckTrigger.WalkingStepCompleted);
        return state;
    }
    private Decision<CampaignState> AcceptFourIslandMovement(CampaignState state,FourIslandFieldTransition transition)
    {
        if(transition.Status!=TransitionStatus.Accepted)return new(transition.Status==TransitionStatus.Rejected?CommandStatus.Rejected:CommandStatus.Fault,state,transition.Diagnostic?.Code);
        var moved=state with{World=transition.State,FourIslandMovement=transition.ActiveMovement is {} active?new(active,transition.Callbacks):null,
            FourIslandMovementDirection=transition.State.Player.Facing,Motions=[],PresentationEffects=[]};
        foreach(var e in transition.Events)
            if(e.Presentation is {} cue)
            {
                string suffix=cue.Direction switch{Direction.North=>"up",Direction.South=>"down",Direction.East=>"right",_=>"left"};
                moved=moved with{PresentationTicks=cue.MovementUpdates==0?FieldFlashSourcePresentation.ShowMonCueFrames:cue.MovementUpdates,
                    Motions=e.Kind==FourIslandFieldEventKind.Moved?[new("LOCALID_PLAYER",[e.Before,e.After],[cue.SourceAction+"_"+suffix,"step_end"])]:[]};
            }
        if(transition.ActiveMovement is not null)moved=moved with{Script=moved.Script with{Locked=true}};
        else moved=ApplyFourIslandCallbacks(moved,transition.Callbacks);
        return new(CommandStatus.Accepted,moved);
    }
    private Decision<CampaignState> DecideFourIslandMovement(CampaignState state,CampaignCommand command)
    {
        var movement=state.FourIslandMovement!;
        if(command.Kind!="four_island_field_ack"||command.Text!=movement.Movement.PendingPresentationId.Value)return new(CommandStatus.Rejected,state,"four_island_movement_pending");
        var transition=FourIslandForcedMovementRules.CompletePresentation(state.World,movement.Movement,
            new(new("four:ack:"+state.World.Revision),state.World.Revision,movement.Movement.PendingPresentationId),IcefallRawMap(state),occupied:(x,y,e)=>ActorOccupies(state,x,y,e));
        var result=AcceptFourIslandMovement(state,transition);
        if(result.Status!=CommandStatus.Accepted)return result;
        var next=result.State;
        if(transition.ActiveMovement is null)
        {
            next=next with{Script=next.Script with{Locked=false},PresentationTicks=0,Motions=[],FieldEffectArguments=null};
            next=ApplyFourIslandCallbacks(next,movement.PendingCallbacks);
        }
        return new(CommandStatus.Accepted,next);
    }
    private bool TryApplyIcefallEffect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;
        if(TryApplyWaterfallEffect(state,effect,out next))return true;
        const string path="data/maps/FourIsland_IcefallCave_1F/scripts.inc";
        if(effect.Source.Path!=path)return false;
        if(effect.Opcode=="setstepcallback"&&effect.Source.Symbol=="FourIsland_IcefallCave_1F_OnResume"&&effect.Args.SequenceEqual(new[]{"STEP_CB_ICE"}))
        {if(state.IcefallIce is null)throw new InvalidDataException("icefall_callback_context");return true;}
        if(effect.Opcode=="special"&&effect.Source.Symbol=="FourIsland_IcefallCave_1F_OnLoad"&&effect.Args.SequenceEqual(new[]{"SetIcefallCaveCrackedIceMetatiles"}))
        {next=state with{MapEdits=IcefallEdits(state.IcefallIce?.VisitedMask??0)};return true;}
        if(effect.Opcode=="warphole"&&effect.Source.Symbol=="FourIsland_IcefallCave_1F_EventScript_FallDownHole"&&effect.Args.SequenceEqual(new[]{"MAP_FOUR_ISLAND_ICEFALL_CAVE_B1F"}))
        {
            var ice=state.IcefallIce??throw new InvalidDataException("icefall_fall_missing");
            if(!state.Script.Locked||state.PlayerVisible||!ice.FallRequested)throw new InvalidDataException("icefall_fall_context");
            var start=IcefallIceRules.StartFall(state.World,ice,new(new("ice:fall:"+state.World.Revision),state.World.Revision),_content.Maps[state.World.Player.MapId]);
            if(start.Status!=TransitionStatus.Accepted)throw new InvalidDataException(start.Diagnostic?.Code);
            var fall=IcefallIceRules.CompleteFall(start.State,ice,start.ActiveFall!,new(new("ice:land:"+start.State.Revision),start.State.Revision,start.ActiveFall!.PendingPresentationId),_content.Maps[FourIslandFieldSource.Basement]);
            if(fall.Status!=TransitionStatus.Accepted)throw new InvalidDataException(fall.Diagnostic?.Code);
            next=EnterMap(state with{World=fall.State,IsCycling=false,IsSurfing=false,PlayerVisible=true,Dialogue=null,Motions=[],PresentationTicks=0,PresentationEffects=[],
                Script=state.Script with{Stack=[],Wait=null,Completed=true,Locked=false},Exploration=ResetEncounterModifiers(state.Exploration!)},Direction.South);
            return true;
        }
        // CB2_LoadMap replaces the old fall script before its waitstate can resume.
        if(effect.Opcode=="waitstate"&&effect.Source.Symbol=="FourIsland_IcefallCave_1F_EventScript_FallDownHole"&&state.World.Player.MapId==FourIslandFieldSource.Basement)return true;
        return false;
    }
}
