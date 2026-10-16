using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Application;

public sealed record CampaignSeafoamMovement(SeafoamMovementState Movement);

public sealed partial class CampaignSession
{
    public bool SeafoamCurrentStopped(MapId map) => SeafoamCurrentStopped(State, map);
    private static bool SeafoamCurrentStopped(CampaignState state, MapId map) =>
        state.Script.Flags.GetValueOrDefault(map == SeafoamFieldRules.B3F ? SeafoamFieldRules.B3FStopped : SeafoamFieldRules.B4FStopped);
    private IReadOnlyDictionary<MapId, ConnectedFieldMap> SeafoamConnectedMaps(CampaignState state)
    {
        var maps = TrainerTowerConnectedMaps(state);
        if (!_content.SeafoamEnabled) return maps;
        var result = maps.ToDictionary(pair => pair.Key, pair => pair.Value);
        foreach (var id in new[] { SeafoamFieldRules.B3F, SeafoamFieldRules.B4F })
            result[id] = _content.SeafoamConnectedMap(id, SeafoamCurrentStopped(state, id))!;
        return result;
    }
    private IReadOnlyDictionary<MapId, IFieldMap> SeafoamRawMaps(CampaignState state)
    {
        var maps = TrainerTowerMovementMaps(state);
        if (!_content.SeafoamEnabled) return maps;
        var result = maps.ToDictionary(pair => pair.Key, pair => pair.Value);
        foreach (var id in new[] { SeafoamFieldRules.B3F, SeafoamFieldRules.B4F })
            result[id] = new SeafoamEditedMap(_content.SeafoamMap(id, SeafoamCurrentStopped(state, id))!,
                id == state.World.Player.MapId ? state.MapEdits : null, _content);
        return result;
    }
    private sealed class SeafoamEditedMap(IFieldMap source, ImmutableSortedDictionary<string,FieldMetatileEdit>? edits, ICampaignCatalog content) : IFieldMap
    {
        public MapId Id => source.Id;
        public int Width => source.Width;
        public int Height => source.Height;
        public ImmutableArray<FieldWarp> FieldWarps => source.FieldWarps;
        public int? SourceBehaviorAt(int x, int y) => edits?.TryGetValue(x+":"+y, out var edit) == true
            ? content.MetatileDefinition(Id, edit.SourceMetatileId)!.Behavior : source.SourceBehaviorAt(x, y);
        public int? SourceEncounterTypeAt(int x, int y) => edits?.TryGetValue(x+":"+y, out var edit) == true
            ? content.MetatileDefinition(Id, edit.SourceMetatileId)!.EncounterType : source.SourceEncounterTypeAt(x, y);
        public TileCell? GetTile(int x, int y) => edits?.TryGetValue(x+":"+y, out var edit) == true
            ? new(edit.Collision != 0, edit.Elevation, TileBehavior.Floor) : source.GetTile(x, y);
    }
    private IReadOnlyDictionary<MapId, ConnectedFieldMap> SeafoamWalkingMaps(IReadOnlyDictionary<MapId, ConnectedFieldMap> maps)
    {
        if (!_content.SeafoamEnabled) return maps;
        var result = maps.ToDictionary(pair => pair.Key, pair => pair.Value);
        foreach (var pair in maps.Where(pair => SeafoamFieldRules.IsWaterFloor(pair.Key)))
            result[pair.Key] = pair.Value with { Cells = pair.Value.Cells.Select(cell => cell.Collision == 0 &&
                (SeafoamFieldRules.CurrentDirection(cell.SourceBehavior) is not null || cell.SourceBehavior is 0x11 or 0x12 or 0x17 or 0x1a or 0x1b) ? cell with { SourceBehavior = 0x10 } : cell).ToImmutableArray() };
        var coast=new MapId("map.route-20");
        if(result.TryGetValue(coast,out var coastMap))result[coast]=coastMap with {Cells=coastMap.Cells.Select(cell=>cell.Collision==0&&cell.SourceBehavior==0x17&&cell.Elevation==3?cell with {SourceBehavior=0}:cell).ToImmutableArray()};
        return result;
    }
    private bool SeafoamForcedTile(CampaignState state) => _content.SeafoamEnabled && state.IsSurfing &&
        SeafoamFieldRules.IsWaterFloor(state.World.Player.MapId) && SeafoamFieldRules.CurrentDirection(SeafoamRawMaps(state)[state.World.Player.MapId].SourceBehaviorAt(state.World.Player.X,state.World.Player.Y)??-1) is not null;
    private Decision<CampaignState>? TrySeafoamHoleStep(CampaignState state, CampaignCommand command)
    {
        if (!_content.SeafoamEnabled || command.Kind != "move" || !SeafoamFieldRules.IsCave(state.World.Player.MapId)) return null;
        var p = state.World.Player; var (dx, dy) = Delta(command.Direction);
        if (!SeafoamFieldRules.Holes.Any(h => h.Map == p.MapId && h.X == p.X + dx && h.Y == p.Y + dy)) return null;
        if (state.Actors.Values.Any(a => a.Active && a.X == p.X + dx && a.Y == p.Y + dy && (a.Elevation == 0 || a.Elevation == p.Elevation))) return null;
        return AcceptSeafoamMovement(state, SeafoamMovementRules.StepOntoHole(state.World,
            new(new("seafoam:fall:" + state.World.Revision), state.World.Revision), SeafoamRawMaps(state), command.Direction,
            state.IsCycling ? FourIslandTravelMode.Bicycle : FourIslandTravelMode.Walk, occupied: (x,y,e) => ActorOccupies(state,x,y,e)));
    }
    private CampaignState? StartSeafoamCurrent(CampaignState state)
    {
        var p = state.World.Player;
        if (!_content.SeafoamEnabled || !SeafoamFieldRules.IsWaterFloor(p.MapId) || !state.IsSurfing ||
            !state.Script.Completed || state.Script.Wait is not null || state.Script.Locked || state.WildBattle is not null ||
            state.ForestTrainerBattle is not null || state.DoubleBattle is not null ||
            SeafoamFieldRules.CurrentDirection(SeafoamRawMaps(state)[p.MapId].SourceBehaviorAt(p.X,p.Y) ?? -1) is null) return null;
        var result = AcceptSeafoamMovement(state, SeafoamMovementRules.StartCurrent(state.World,
            new(new("seafoam:current:" + state.World.Revision),state.World.Revision),SeafoamRawMaps(state)[p.MapId],true,
            occupied:(x,y,e) => ActorOccupies(state,x,y,e)));
        if (result.Status != CommandStatus.Accepted) throw new InvalidDataException(result.Error);
        return result.State;
    }
    private Decision<CampaignState> AcceptSeafoamMovement(CampaignState state, SeafoamFieldTransition result)
    {
        if (result.Status != TransitionStatus.Accepted) return new(CommandStatus.Rejected,state,result.Diagnostic?.Code);
        var next = state with { World = result.State, SeafoamMovement = result.Active is {} active ? new(active) : null,
            Motions = [], PresentationEffects = [], PresentationTicks = 0 };
        if (result.MapLoad is not null)
        {
            next = Pump(EnterMap(next with { IsCycling=false, IsSurfing=false, SurfEffect=null, PlayerVisible=true, Dialogue=null,
                Script=next.Script with { Stack=[], Wait=null, Completed=true, Locked=false },
                Exploration=ResetEncounterModifiers(next.Exploration!) },Direction.South));
            if (next.Script.Wait is not null || !next.Script.Completed)
                throw new InvalidDataException("seafoam_fall_map_entry_continuation");
        }
        foreach (var e in result.Events)
            if (e.Presentation is {} cue)
            {
                string suffix=cue.Direction switch {Direction.North=>"up",Direction.South=>"down",Direction.East=>"right",_=>"left"};
                next=next with { PresentationTicks=cue.MovementUpdates,
                    Motions=[new("LOCALID_PLAYER",[e.Before,e.After],[cue.SourceAction+"_"+suffix,"step_end"])] };
            }
        if (result.Active?.Phase == SeafoamMovementPhase.Departure)
            next=next with { PresentationTicks=DottedHoleFallPresentation.BeforeInvisibleDelay+DottedHoleFallPresentation.BeforeWarpDelay };
        if (result.Surfing is {} surfing) next=next with {IsSurfing=surfing,IsCycling=false,Music=surfing?SurfMusic(next.World.Player.MapId):_content.MapDefinition(next.World.Player.MapId).Music};
        next=next with { Script=next.Script with { Locked=result.Active is not null } };
        if (result.Callbacks.TookStep)
            next = result.Active?.Hole is not null ? ApplyRenewableHiddenItemWalkingStep(next) : ApplyFourIslandCallbacks(next,result.Callbacks);
        if (next.SeafoamMovement is {} movement && next.World != movement.Movement.AwaitingWorld)
            next=next with {SeafoamMovement=new(SeafoamMovementRules.ReconcileCallbackRng(movement.Movement,next.World))};
        if (result.SetFallingVariable)
            next=Pump(next with {Script=next.Script with {Variables=next.Script.Variables.SetItem(SeafoamFieldRules.FallingVariable,1)}});
        return new(CommandStatus.Accepted,next);
    }
    private Decision<CampaignState> DecideSeafoamMovement(CampaignState state, CampaignCommand command)
    {
        var movement=state.SeafoamMovement!.Movement;
        if(command.Kind!="seafoam_field_ack"||command.Text!=movement.PendingPresentationId.Value)
            return new(CommandStatus.Rejected,state,"seafoam_movement_pending");
        return AcceptSeafoamMovement(state,SeafoamMovementRules.CompletePresentation(state.World,movement,
            new(new("seafoam:ack:"+state.World.Revision),state.World.Revision,movement.PendingPresentationId),SeafoamRawMaps(state),
            occupied:(x,y,e)=>ActorOccupies(state,x,y,e)));
    }
    private string? ValidateSeafoamField(CampaignState state)
    {
        if(state.SeafoamMovement is not null)return "save_seafoam_movement_active";
        var map=state.World.Player.MapId;
        foreach(var id in new[]{SeafoamFieldRules.B3F,SeafoamFieldRules.B4F})
            if(SeafoamCurrentStopped(state,id)&&!SeafoamFieldRules.BothBouldersPresent(id,state.Script.Flags))return "save_seafoam_current_boulders";
        if(!SeafoamFieldRules.IsCave(map))return null;
        if(state.Script.Variables.GetValueOrDefault(SeafoamFieldRules.FallingVariable)!=0||
            state.Script.Variables.GetValueOrDefault(SeafoamFieldRules.B4FSceneVariable)!=0)return "save_seafoam_current_script_pending";
        var expected=SeafoamFieldRules.OnLoadChanges(map,state.Script.Flags).ToImmutableSortedDictionary(c=>c.X+":"+c.Y,
            c=>new FieldMetatileEdit(c.X,c.Y,c.MetatileId,c.Collision,c.Elevation));
        if(!(state.MapEdits??ImmutableSortedDictionary<string,FieldMetatileEdit>.Empty).SequenceEqual(expected))return "save_seafoam_metatile_state";
        return null;
    }
    private bool TryApplySeafoamFieldEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next=state; var map=state.World.Player.MapId;
        if(!_content.SeafoamEnabled||!SeafoamFieldRules.IsCave(map)||!effect.Source.Path.StartsWith("data/maps/SeafoamIslands_",StringComparison.Ordinal))return false;
        var a=effect.Args;
        if(effect.Opcode=="addvar"&&a.SequenceEqual(new[]{"VAR_TEMP_2","1"}))
        {
            int count=state.Script.Variables.GetValueOrDefault("VAR_TEMP_2");
            if(count is <0 or >=2||!effect.Source.Symbol.EndsWith("_EventScript_AddBoulderPresent",StringComparison.Ordinal))throw new InvalidDataException("seafoam_boulder_count_context");
            next=state with {Script=state.Script with {Variables=state.Script.Variables.SetItem("VAR_TEMP_2",count+1)}};return true;
        }
        if(effect.Opcode=="setmaplayoutindex")
        {
            if(a.Length!=1||a[0]!=SeafoamFieldRules.Layout(map,state.Script.Flags)||!SeafoamCurrentStopped(state,map))throw new InvalidDataException("seafoam_layout_context");
            return true;
        }
        if(effect.Opcode=="setmetatile")
        {
            if(a.Length!=4||a[2]!="METATILE_General_CalmWater"||a[3]!="0"||
                !int.TryParse(a[0],out int x)||!int.TryParse(a[1],out int y))throw new InvalidDataException("seafoam_water_patch_context");
            var patch=SeafoamFieldRules.OnLoadChanges(map,state.Script.Flags).SingleOrDefault(c=>c.X==x&&c.Y==y)
                ??throw new InvalidDataException("seafoam_water_patch_source");
            next=state with {MapEdits=(state.MapEdits??ImmutableSortedDictionary<string,FieldMetatileEdit>.Empty).SetItem(x+":"+y,new(x,y,patch.MetatileId,patch.Collision,patch.Elevation))};return true;
        }
        if(effect.Opcode=="getplayerxy"&&a.SequenceEqual(new[]{"VAR_0x8008","VAR_0x8009"}))
        {next=state with {Script=state.Script with {Variables=state.Script.Variables.SetItem(a[0],state.World.Player.X).SetItem(a[1],state.World.Player.Y)}};return true;}
        if(effect.Opcode=="special"&&a.SequenceEqual(new[]{"ForcePlayerToStartSurfing"}))
        {
            if(map!=SeafoamFieldRules.B4F||state.World.Player.X!=27||state.World.Player.Y!=21||state.Script.Variables.GetValueOrDefault(SeafoamFieldRules.B4FSceneVariable)!=1)
                throw new InvalidDataException("seafoam_script_surf_context");
            next=state with {IsSurfing=true,IsCycling=false,Music=SurfMusic(map)};return true;
        }
        if(effect.Opcode=="special"&&a.SequenceEqual(new[]{"SeafoamIslandsB4F_CurrentDumpsPlayerOnLand"}))
        {
            if(map!=SeafoamFieldRules.B4F||(state.World.Player.X,state.World.Player.Y)!=(12,13)||!state.IsSurfing||!state.Script.Locked)
                throw new InvalidDataException("seafoam_current_dismount_context");
            var result=SeafoamMovementRules.StartScriptDismount(state.World,
                new(new("seafoam:script-dismount:"+state.World.Revision),state.World.Revision),SeafoamRawMaps(state)[map],true,
                occupied:(x,y,e)=>ActorOccupies(state,x,y,e));
            var decision=AcceptSeafoamMovement(state,result);
            if(decision.Status!=CommandStatus.Accepted)throw new InvalidDataException(decision.Error);
            next=decision.State;return true;
        }
        if(effect.Opcode=="warp"&&map==SeafoamFieldRules.B3F&&a.SequenceEqual(new[]{"MAP_SEAFOAM_ISLANDS_B4F","27","21"}))
        {
            if(!state.Script.Locked||state.Script.Variables.GetValueOrDefault(SeafoamFieldRules.B4FSceneVariable)!=1)throw new InvalidDataException("seafoam_current_warp_context");
            next=EnterMap(state with {World=state.World with {Player=new(SeafoamFieldRules.B4F,27,21,1,Direction.North)},
                Script=state.Script with {Stack=[],Wait=null,Completed=true,Locked=false},Motions=[],PresentationEffects=[],PresentationTicks=0},Direction.North);return true;
        }
        if(effect.Opcode=="waitstate"&&effect.Source.Symbol=="SeafoamIslands_B3F_EventScript_EnterByFalling"&&map==SeafoamFieldRules.B4F)return true;
        return false;
    }
}
