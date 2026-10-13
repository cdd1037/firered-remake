using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Encounters;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Randomness;
using Game.Domain.World;
namespace Game.Application;

public sealed record CampaignWildHistory(string Id,int SourceSpeciesId,int Level,Game.Domain.WildBattle.WildBattleOutcome Outcome,int ExperienceGained)
{[System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public Game.Domain.WildBattle.SeviiRubyWildOrigin? SeviiRuby{get;init;}
[System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public Game.Domain.WildBattle.VictoryRoadWildOrigin? VictoryRoad{get;init;}
 [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] public bool ScriptedMarowak{get;init;}
 [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] public int ScriptedSnorlaxRoute{get;init;}
 [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public CampaignLostelleHypnoReceipt? LostelleHypno{get;init;}
 [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] public bool Safari{get;init;}};
public sealed record CampaignExploration([property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] Route1EncounterState? Encounter,long NextWildBattleId,ImmutableArray<CampaignWildHistory> Battles)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public EarlyLandEncounterState? EarlyEncounter{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<CampaignCapturedPokemon>? Captured{get;init;}
}

public sealed partial class CampaignSession
{
    private CampaignState InitializeExploration(CampaignState state)
    {
        if(_content.ForestEnabled)
        {
            var early=EarlyLandEncounterRules.SeedFromGlobal(state.World.Rng,rulesVersion:_content.SeviiRubyEnabled?EarlyLandEncounterRules.SeviiRubyRulesVersion:_content.VictoryRoadEnabled?EarlyLandEncounterRules.VictoryRoadRulesVersion:_content.FirstSeviiEnabled?EarlyLandEncounterRules.FirstSeviiRulesVersion:_content.CinnabarEnabled?EarlyLandEncounterRules.CinnabarRulesVersion:_content.SafariEnabled?EarlyLandEncounterRules.SafariRulesVersion:_content.FuchsiaRouteEnabled?EarlyLandEncounterRules.FuchsiaRulesVersion:_content.SnorlaxEnabled?EarlyLandEncounterRules.SnorlaxRulesVersion:_content.PokemonTowerEnabled?EarlyLandEncounterRules.PokemonTowerRulesVersion:_content.RocketHideoutEnabled?EarlyLandEncounterRules.RocketHideoutRulesVersion:_content.CeladonGymEnabled?EarlyLandEncounterRules.CeladonGymRulesVersion:_content.CeladonApproachEnabled?EarlyLandEncounterRules.CeladonApproachRulesVersion:_content.RockTunnelEnabled?EarlyLandEncounterRules.RockTunnelRulesVersion:_content.Route11Enabled?EarlyLandEncounterRules.Route11RulesVersion:_content.SsAnneEnabled?EarlyLandEncounterRules.SsAnneRulesVersion:_content.VermilionEnabled?EarlyLandEncounterRules.VermilionRulesVersion:_content.BillEnabled?EarlyLandEncounterRules.BillRulesVersion:_content.MtMoonEnabled?EarlyLandEncounterRules.MtMoonRulesVersion:_content.Route3Enabled?EarlyLandEncounterRules.Route3RulesVersion:_content.Route22Enabled?EarlyLandEncounterRules.Route22RulesVersion:EarlyLandEncounterRules.RulesVersion);
            return state with{Exploration=new(null,1,[]){EarlyEncounter=early.State,Captured=[]},World=state.World with{Rng=early.GlobalRng}};
        }
        var seeded=Route1EncounterRules.SeedFromGlobal(state.World.Rng);
        return state with{Exploration=new CampaignExploration(seeded.State,1,[]){Captured=_content.CaptureEnabled?[]:null},World=state.World with{Rng=seeded.GlobalRng}};
    }
    private string? ValidateCampaignWorld(CampaignState state,IFieldMap map)
    {
        if(_content.ConnectedFieldMaps is not {} maps)
            return state.Exploration is not null||state.WildBattle is not null?"save_exploration_content_missing":WorldRules.ValidateState(state.World,map)?.Code;
        if(state.Exploration is not {} exploration||exploration.Battles.IsDefault||exploration.NextWildBattleId<1)return "save_exploration_missing";
        if(state.Script.Variables.GetValueOrDefault("VAR_HAPPINESS_STEP_COUNTER") is <0 or >=128)return "save_happiness_counter_invalid";
        if(_content.ForestEnabled)
        {
            if(exploration.Encounter is not null||exploration.EarlyEncounter is null)return "save_encounter_profile_invalid";
            try{EarlyLandEncounterRules.ValidateState(exploration.EarlyEncounter);}catch(Exception ex) when(ex is ArgumentException or NotSupportedException){return "save_encounter_invalid";}
            if(exploration.EarlyEncounter.RulesVersion!=(_content.SeviiRubyEnabled?EarlyLandEncounterRules.SeviiRubyRulesVersion:_content.VictoryRoadEnabled?EarlyLandEncounterRules.VictoryRoadRulesVersion:_content.FirstSeviiEnabled?EarlyLandEncounterRules.FirstSeviiRulesVersion:_content.CinnabarEnabled?EarlyLandEncounterRules.CinnabarRulesVersion:_content.SafariEnabled?EarlyLandEncounterRules.SafariRulesVersion:_content.FuchsiaRouteEnabled?EarlyLandEncounterRules.FuchsiaRulesVersion:_content.SnorlaxEnabled?EarlyLandEncounterRules.SnorlaxRulesVersion:_content.PokemonTowerEnabled?EarlyLandEncounterRules.PokemonTowerRulesVersion:_content.RocketHideoutEnabled?EarlyLandEncounterRules.RocketHideoutRulesVersion:_content.CeladonGymEnabled?EarlyLandEncounterRules.CeladonGymRulesVersion:_content.CeladonApproachEnabled?EarlyLandEncounterRules.CeladonApproachRulesVersion:_content.RockTunnelEnabled?EarlyLandEncounterRules.RockTunnelRulesVersion:_content.Route11Enabled?EarlyLandEncounterRules.Route11RulesVersion:_content.SsAnneEnabled?EarlyLandEncounterRules.SsAnneRulesVersion:_content.VermilionEnabled?EarlyLandEncounterRules.VermilionRulesVersion:_content.BillEnabled?EarlyLandEncounterRules.BillRulesVersion:_content.MtMoonEnabled?EarlyLandEncounterRules.MtMoonRulesVersion:_content.Route3Enabled?EarlyLandEncounterRules.Route3RulesVersion:_content.Route22Enabled?EarlyLandEncounterRules.Route22RulesVersion:EarlyLandEncounterRules.RulesVersion))return "save_encounter_profile_invalid";
            if(exploration.EarlyEncounter.Pending is not null)return "save_encounter_pending";
        }
        else
        {
            if(exploration.EarlyEncounter is not null||exploration.Encounter is null)return "save_encounter_profile_invalid";
            try{Route1EncounterRules.ValidateState(exploration.Encounter);}catch(Exception ex) when(ex is ArgumentException or NotSupportedException){return "save_encounter_invalid";}
            if(exploration.Encounter.Pending is not null)return "save_encounter_pending";
        }
        if(exploration.NextWildBattleId!=exploration.Battles.Length+1L||exploration.Battles.Where((b,i)=>b is null||b.Id!="wild-battle:"+(i+1)||!(b.SeviiRuby is not null?IsSeviiRubyWildHistory(b):b.VictoryRoad is not null?IsVictoryRoadWildHistory(b):b.LostelleHypno is not null?IsLostelleHypnoHistory(state,b):b.Safari?IsSafariHistory(b):b.ScriptedSnorlaxRoute!=0?IsSnorlaxHistory(b):b.ScriptedMarowak?_content.PokemonTowerEnabled&&b.SourceSpeciesId==105&&b.Level==30&&b.Outcome!=Game.Domain.WildBattle.WildBattleOutcome.Caught:WildOriginSupported(b.SourceSpeciesId,b.Level))||!Enum.IsDefined(b.Outcome)||b.Outcome==Game.Domain.WildBattle.WildBattleOutcome.Ongoing||(!_content.CaptureEnabled&&b.Outcome==Game.Domain.WildBattle.WildBattleOutcome.Caught)||b.ExperienceGained<0).Any())return "save_wild_history_invalid";
        if(state.World.RulesVersion!=RuleVersions.GridWorldV0)return "save_world_profile_invalid";
        if(_content.SupportsTeleportPads(map.Id))map=new SilphMovementMap(map,state.MapEdits,_content);
        var p=state.World.Player;var here=map.SourceBehaviorAt(p.X,p.Y);
        if(here is >=0x60 and <=0x71)return (_content.MtMoonEnabled?FieldMovementRules.ValidateLadderPosition(state.World,map):WorldRules.ValidateState(state.World,map))?.Code;
        var check=ConnectedFieldRules.Complete(state.World with{RulesVersion=ConnectedFieldRules.RulesVersion},new(new("validate:connected"),state.World.Revision,ConnectedFieldAction.Idle,p.Facing),SeviiRubyWalkingMaps(VictoryRoadWalkingMaps(SafariConnectedMaps(EditedFieldMaps(state)))),_content.MtMoonEnabled,_content.VermilionGymEnabled,enableSpinTiles:_content.RocketHideoutEnabled,surfing:state.IsSurfing,sourceTerrain:_content.TerrainAt);
        return check.Status==TransitionStatus.Accepted?null:check.Diagnostic?.Code??"save_connected_invalid";
    }
    private IReadOnlyDictionary<MapId,ConnectedFieldMap> OccupiedFieldMaps(CampaignState state)
    {
        var maps=EditedFieldMaps(state).ToDictionary(x=>x.Key,x=>x.Value);
        // Complete reads the current map and at most one direct connection; ledges stay on the current map.
        var occupiedMaps=new HashSet<MapId>{state.World.Player.MapId};
        if(maps.TryGetValue(state.World.Player.MapId,out var current)&&current is not null&&!current.Connections.IsDefault)
            foreach(var connection in current.Connections)
                if(connection is not null)occupiedMaps.Add(connection.DestinationMapId);
        foreach(var pair in maps.ToArray())
        {
            if(!occupiedMaps.Contains(pair.Key))continue;
            var cells=pair.Value.Cells.ToBuilder();
            var actors=pair.Key==state.World.Player.MapId?state.Actors.Values.Where(a=>a.Active).Select(a=>(a.X,a.Y,a.Elevation)):
                _content.MapDefinition(pair.Key).Actors.Where(a=>a.HideFlag=="0"||!state.Script.Flags.GetValueOrDefault(a.HideFlag)).Select(a=>(a.X,a.Y,a.Elevation));
            foreach(var actor in actors)
            {
                int index=actor.Y*pair.Value.Width+actor.X;
                if(actor.X<0||actor.Y<0||actor.X>=pair.Value.Width||actor.Y>=pair.Value.Height)continue;
                var cell=cells[index];if(actor.Elevation==0||cell.Elevation==0||actor.Elevation==cell.Elevation)cells[index]=cell with{Occupied=true};
            }
            maps[pair.Key]=pair.Value with{Cells=cells.ToImmutable()};
        }
        return SeviiRubyWalkingMaps(CinnabarWalkingMaps(VictoryRoadWalkingMaps(SafariConnectedMaps(maps))));
    }
    private Decision<CampaignState> DecideConnectedField(CampaignState state,CampaignCommand command)
    {
        if(!Enum.IsDefined(command.Direction))return new(CommandStatus.Rejected,state,"invalid_direction");
        var before=state.World.Player;var movementMaps=SeviiRubyMovementMaps(PokemonLeagueMovementMaps(CinnabarMovementMaps(SafariMovementMaps(SilphMovementMaps(RocketMovementMaps(state),state)),state),state,command),state);var map=movementMaps[before.MapId];var (dx,dy)=Delta(command.Direction);int x=before.X+dx,y=before.Y+dy;
        bool turn=command.Kind=="turn";
        if(!turn&&TryStartFieldSurfDismount(state,command) is {} surfDismount)return surfDismount;
        if(!turn&&TryStartFieldStrengthPush(state,command) is {} strengthPush)return strengthPush;
        if(!turn&&_content.FuchsiaRouteEnabled&&_content.Maps[before.MapId].FieldWarps.Any(w=>(w.X==x&&w.Y==y||w.X==before.X&&w.Y==before.Y)&&!_content.IsFuchsiaRouteWarpAllowed(before.MapId,w.Index)))return new(CommandStatus.Rejected,state,"fuchsia_route_warp_not_open");
        if(!turn&&_content.SnorlaxEnabled&&_content.Maps[before.MapId].FieldWarps.Any(w=>(w.X==x&&w.Y==y||w.X==before.X&&w.Y==before.Y)&&!_content.IsSnorlaxWarpAllowed(before.MapId,w.Index)))return new(CommandStatus.Rejected,state,"snorlax_warp_not_open");
        if(TrySilphPadStep(state,command) is {} silphPad)return silphPad;
        if(TrySilphDynamicExit(state,command) is {} silphExit)return silphExit;
        if(TryDepartmentDynamicExit(state,command) is {} departmentExit)return departmentExit;
        if(TryRocketDynamicExit(state,command) is {} dynamicExit)return dynamicExit;
        if(!turn&&command.Direction is Direction.North or Direction.South&&before.Facing==command.Direction&&RegionalWalkIntoSign(state,x,y) is {} regionalSign)
            return new(CommandStatus.Accepted,regionalSign);
        if(!_content.SupportsRegionalFlavor(before.MapId)&&!turn&&command.Direction is Direction.North or Direction.South&&before.Facing==command.Direction&&map.SourceBehaviorAt(x,y)==0x84)
        {
            var sign=_content.MapDefinition(map.Id).BackgroundTriggers.FirstOrDefault(z=>z.X==x&&z.Y==y);
            if(sign is not null)return new(CommandStatus.Accepted,Pump(StartStory(state,sign.ScriptId)));
        }
        var here=map.SourceBehaviorAt(before.X,before.Y);var front=map.SourceBehaviorAt(x,y);
        if(turn&&here is >=0x60 and <=0x71)
        {
            var error=_content.MtMoonEnabled?FieldMovementRules.ValidateLadderPosition(state.World,map):WorldRules.ValidateState(state.World,map);if(error is not null)return new(CommandStatus.Fault,state,error.Code);
            if(before.Facing==command.Direction)return new(CommandStatus.Rejected,state,"invalid-turn-action");
            return new(CommandStatus.Accepted,state with{World=state.World with{Revision=checked(state.World.Revision+1),Player=before with{Facing=command.Direction}}});
        }
        bool legacySpecial=!turn&&(here is >=0x60 and <=0x71||front is >=0x60 and <=0x71);
        CampaignState moved;bool changed,checkEncounter;bool preempted=false;EncounterCheckTrigger trigger;
        if(legacySpecial)
        {
            if(VisibleActorAt(state,x,y) is not null)return new(CommandStatus.Accepted,state with{World=state.World with{Revision=checked(state.World.Revision+1),Player=before with{Facing=command.Direction}}});
            var transition=FieldMovementRules.Move(state.World,new(new("field:"+state.World.Revision),state.World.Revision,command.Direction),movementMaps,_content.ForestEnabled,_content.MtMoonEnabled,_content.VermilionGymEnabled);
            if(transition.Status!=TransitionStatus.Accepted)return new(transition.Status==TransitionStatus.Rejected?CommandStatus.Rejected:CommandStatus.Fault,state,transition.Diagnostic?.Code);
            moved=state with{World=transition.State};changed=transition.MapChanged;
            if(changed)moved=RecordEscapeWarp(state,moved,command.Direction);
            checkEncounter=!changed&&(before.X!=transition.State.Player.X||before.Y!=transition.State.Player.Y);trigger=EncounterCheckTrigger.WalkingStepCompleted;
        }
        else
        {
            var action=turn?ConnectedFieldAction.TurnInPlace:ConnectedFieldAction.Walk;
            var transition=ConnectedFieldRules.Complete(state.World with{RulesVersion=ConnectedFieldRules.RulesVersion},new(new("field:"+state.World.Revision),state.World.Revision,action,command.Direction),OccupiedFieldMaps(state),_content.MtMoonEnabled,_content.VermilionGymEnabled,biking:_content.CityServicesEnabled&&state.IsCycling,enableSpinTiles:_content.RocketHideoutEnabled,activeSpin:state.RocketSpin?.Forced,enableCardinalLedges:_content.FuchsiaRouteEnabled,surfing:state.IsSurfing,sourceTerrain:_content.TerrainAt);
            if(transition.Status!=TransitionStatus.Accepted)return new(transition.Status==TransitionStatus.Rejected?CommandStatus.Rejected:CommandStatus.Fault,state,transition.Diagnostic?.Code);
            moved=state with{World=transition.State with{RulesVersion=RuleVersions.GridWorldV0}};changed=transition.MapChanged;checkEncounter=transition.RequiresEncounterCheck;
            trigger=turn?EncounterCheckTrigger.TurnInPlaceCompleted:EncounterCheckTrigger.WalkingStepCompleted;
        }
        if(state.IsSurfing&&!SurfRegionAllows(moved.World.Player))return new(CommandStatus.Rejected,state,"field_surf_region_boundary_not_open");
        if(_content.SnorlaxEnabled&&!_content.IsSnorlaxStablePosition(moved.World.Player.MapId,moved.World.Player.X,moved.World.Player.Y))return new(CommandStatus.Rejected,state,"snorlax_region_boundary_not_open");
        if(changed&&moved.World.Player.MapId.Value==ViridianGymMap&&(!_content.ViridianGymEnabled||state.Script.Variables.GetValueOrDefault(ViridianGymDoor)!=1||!ViridianGymDoorBadges(state)))return new(CommandStatus.Rejected,state,"viridian_gym_door_locked");
        if(changed&&moved.World.Player.MapId.Value=="map.saffron-city-gym"&&(!_content.SaffronGymEnabled||!state.Script.Flags.GetValueOrDefault("FLAG_HIDE_SAFFRON_ROCKETS")))return new(CommandStatus.Rejected,state,"saffron_gym_rocket_blockade");
        if(changed)
        {
            moved=RecordSilphElevatorEntrance(state,moved);
            moved=RecordDepartmentElevatorEntrance(state,moved);
            moved=RecordRocketElevatorEntrance(state,moved);
            moved=moved with{Exploration=ResetEncounterModifiers(moved.Exploration!)};
            bool tookStep=moved.World.StepCount>state.World.StepCount;
            // Cave step-warps count before the new map's transition callback;
            // camera connections enter first, then complete the player-input step.
            if(legacySpecial&&tookStep)moved=ApplyRenewableHiddenItemWalkingStep(moved);
            moved=EnterSurfConnectionMap(moved,moved.World.Player.Facing);
            if(_content.FirstSeviiEnabled&&!legacySpecial&&tookStep)moved=moved with{EntryPrograms=moved.EntryPrograms.Add("@renewable-camera-step")};
            moved=Pump(moved);
        }
        else
        {
            moved=StartRocketArrowArrival(state,RefreshActors(moved));
            if(moved.RocketSpin is not null)return new(CommandStatus.Accepted,moved);
            moved=CheckForestTrainerSight(moved);
            if(moved.ForestTrainerBattle is not null||moved.DoubleBattle is not null)return new(CommandStatus.Accepted,moved);
            // ProcessPlayerFieldInput runs on-frame scripts before completed-step coordinate
            // scripts. Even a synchronous script that ends immediately preempts this input.
            if(_content.RepelEnabled)
            {
                var onFrame=Pump(moved);preempted=onFrame.Script.Revision!=moved.Script.Revision;moved=onFrame;
            }
            if(!preempted&&moved.World.StepCount>state.World.StepCount)moved=ApplyRenewableHiddenItemWalkingStep(moved);
            var p=moved.World.Player;
            if(!preempted&&(p.X!=before.X||p.Y!=before.Y))
            {
                var coord=_content.MapDefinition(p.MapId).CoordTriggers.FirstOrDefault(t=>t.X==p.X&&t.Y==p.Y&&(t.Elevation==0||t.Elevation==p.Elevation)&&moved.Script.Variables.GetValueOrDefault(t.Variable)==t.Expected);
                preempted=coord is not null;
                if(coord is not null&&_content.BattleContent is not null&&p.MapId==_content.MapFromSourceSymbol("MAP_PALLET_TOWN_PROFESSOR_OAKS_LAB")&&moved.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_PALLET_TOWN_PROFESSOR_OAKS_LAB")==3)PreflightLabBattle(moved);
                if(coord is not null)moved=StartStory(moved,coord.ScriptId);
            }
            if(!preempted&&TryStartCinnabarFall(state,moved) is {} cinnabarFall)return new(CommandStatus.Accepted,cinnabarFall);
            moved=Pump(moved);
        }
        // Coordinate/map scripts take precedence over TryStandardWildEncounter.
        if(!preempted&&(!_content.ForestEnabled||!legacySpecial||!changed)&&moved.Script.Completed&&moved.Script.Wait is null&&moved.Battle is null&&moved.Dialogue is null)
        {
            if(moved.World.StepCount>state.World.StepCount)
            {
                moved=ApplyFieldPoisonWalkingStep(ApplyWalkingFriendship(moved));
                if(moved.FieldPoison is null)moved=ApplySafariWalkingStep(moved);
                if(moved.FieldPoison is null&&moved.Script.Completed)moved=ApplyRepelWalkingStep(moved);
            }
            // The expiration message owns this input. Dismissing it never replays the
            // encounter check, cooldown increment, or either encounter RNG stream.
            if(checkEncounter&&moved.FieldPoison is null&&moved.Repel is null&&moved.Script.Completed&&moved.Script.Wait is null)
            {
                if(_content.RepelEnabled)
                {
                    var p=moved.World.Player;var (sx,sy)=Delta(p.Facing);
                    if(RegionalWalkIntoSign(moved,p.X+sx,p.Y+sy) is {} regionalSignAfterStep)return new(CommandStatus.Accepted,regionalSignAfterStep);
                    var sign=!_content.SupportsRegionalFlavor(p.MapId)&&p.Facing is Direction.North or Direction.South&&_content.Maps[p.MapId].SourceBehaviorAt(p.X+sx,p.Y+sy)==0x84
                        ?_content.MapDefinition(p.MapId).BackgroundTriggers.FirstOrDefault(z=>z.X==p.X+sx&&z.Y==p.Y+sy):null;
                    if(sign is not null)return new(CommandStatus.Accepted,Pump(StartStory(moved,sign.ScriptId)));
                }
                moved=CheckFieldEncounter(moved,trigger);
            }
        }
        return new(CommandStatus.Accepted,moved);
    }
    private CampaignState ApplyWalkingFriendship(CampaignState state)
    {
        int count=(state.Script.Variables.GetValueOrDefault("VAR_HAPPINESS_STEP_COUNTER")+1)%128;
        state=state with{Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_HAPPINESS_STEP_COUNTER",count)}};
        if(count!=0)return state;
        var rng=state.World.Rng;var party=state.Party;
        for(int i=0;i<party.Length;i++)
        {
            var owned=party[i];if(owned.IsEgg)continue;
            if(!OwnedHeldItemSupported(state,owned))throw new InvalidDataException("walking_friendship_held_item_unsupported");
            var draw=Gen3Rng.Next(rng,"field.walking-friendship:"+owned.InstanceId);rng=draw.State;
            if((draw.Value&1)!=0)continue;
            int delta=1+(owned.Ball=="ITEM_LUXURY_BALL"?1:0)+(owned.MetLocation==_content.MapDefinition(state.World.Player.MapId).RegionMapSection?1:0);
            party=party.SetItem(i,owned with{Pokemon=owned.Pokemon with{Friendship=Math.Min(255,owned.Pokemon.Friendship+delta)}});
        }
        return state with{Party=party,World=state.World with{Rng=rng}};
    }
    private CampaignState CheckFieldEncounter(CampaignState state,EncounterCheckTrigger trigger)
    {
        if(state.IsSurfing)return CheckSurfWaterEncounter(state,trigger);
        if(_content.ForestEnabled)return CheckEarlyLandEncounter(state,trigger);
        var p=state.World.Player;var cell=_content.ConnectedFieldMaps![p.MapId].CellAt(p.X,p.Y)!;
        var exploration=state.Exploration!;
        if(p.MapId!=Route1EncounterRules.Map)return state with{Exploration=exploration with{Encounter=exploration.Encounter! with{PreviousMetatileBehavior=(ushort)cell.SourceBehavior}}};
        if(state.Party.IsEmpty||state.Party.Length>6||state.Party.All(p=>p.IsEgg||p.Pokemon.CurrentHp==0)||(!_content.CaptureEnabled&&state.Party.Length!=1))throw new InvalidDataException("wild_encounter_live_single_party_required");
        var lead=state.Party[0];
        var result=Route1EncounterRules.Check(exploration.Encounter!,new(p.MapId,trigger,(byte)cell.SourceBehavior,cell.SourceBehavior==2?EncounterTileType.Land:EncounterTileType.None),new(LeadAbility:lead.Pokemon.AbilityId,LeadHeldItem:lead.HeldItem),state.World.Rng);
        var next=state with{Exploration=exploration with{Encounter=result.State},World=state.World with{Rng=result.GlobalRng}};
        return result.Candidate is null?next:BeginWildBattle(next);
    }
}
