using System.Collections.Immutable;
using Game.Domain.World;

namespace Game.Application;

// A display recording, never executable game commands or copies of the campaign.
public sealed record CampaignQuestLogActor(string Id,string Graphics,int X,int Y,Direction Facing,bool Visible);
public enum CampaignQuestLogEvent
{
    Input=0, None=2, PartyOrder=3, UsedItem=4, GaveHeldItem=5, GaveHeldItemBag=6, GaveHeldItemPc=7, TookHeldItem=8, SwappedHeldItem=9, SwappedHeldItemPc=10,
    PokemonCenter=11, SwitchedBoxes=20, SwitchedWithinBox=21, SwitchedPartyPc=22,
    MovedBoxes=23, MovedWithinBox=24, WithdrewPokemon=25, DepositedPokemon=26,
    MovedGroup=27, DepositedItem=28, WithdrewItem=29, GymWon=30, WildWon=31,
    EliteFourWon=32, ChampionWon=33, TrainerWon=34, Departed=35, FieldMove=36,
    Bought=37, Sold=38, StoryItem=40, Arrived=42
}
public sealed record CampaignQuestLogAction(int ActorIndex,int X,int Y,Direction Facing,bool Visible,CampaignQuestLogEvent Event,int Frames)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] public int StartFrame{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] public bool Repeat{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public int? LocationId{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public FieldMetatileEdit? Tile{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string? Layout{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string? SourceAction{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string? TextKey{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] public ImmutableArray<string> Arguments{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string? Graphics{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public CampaignQuestLogActor? Spawn{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public CampaignQuestLogMapProjection? Transition{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public CampaignQuestLogTransitionTiming? TransitionTiming{get;init;}
}
public sealed record CampaignQuestLogScene(string Map,ImmutableArray<CampaignQuestLogActor> Actors,ImmutableArray<CampaignQuestLogAction> Actions)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string? Layout{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] public ImmutableArray<FieldMetatileEdit> Tiles{get;init;}

    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] public bool Closed{get;init;}
}
public sealed record CampaignQuestLog
{
    public ImmutableArray<CampaignQuestLogScene> Scenes{get;init;}
    public int Version{get;init;}
    public CampaignQuestLog(ImmutableArray<CampaignQuestLogScene> scenes):this(scenes,2){}
    [System.Text.Json.Serialization.JsonConstructor]
    public CampaignQuestLog(ImmutableArray<CampaignQuestLogScene> scenes,int version){Scenes=scenes;Version=version;}
}

public static partial class CampaignQuestLogRecorder
{
    public const int SceneLimit=4, ActorLimit=16, ActionLimit=QuestLogSource.ScriptBytes/2;
    public const string PlayerId="LOCALID_PLAYER";

    public static string? Validate(CampaignQuestLog? log,QuestLogSource? source=null,ICampaignCatalog? catalog=null)
    {
        if(log is null)return null;
        if(log.Version!=2)return "save_quest_log_version_unsupported";
        if(log.Scenes.IsDefault||log.Scenes.Length is <1 or >SceneLimit)return "save_quest_log_scenes_invalid";
        foreach(var scene in log.Scenes)
        {
            if(scene is null||!Identifier(scene.Map)||scene.Actors.IsDefault||scene.Actors.Length is <1 or >ActorLimit||scene.Actions.IsDefault||scene.Actions.Length is <1 or >ActionLimit)return "save_quest_log_scene_invalid";
            if(LogicalCommandCount(scene.Actions)>QuestLogSource.EventCursorCount)return "save_quest_log_command_limit";
            if(source is not null&&(!source.MapNames.ContainsKey(scene.Map)||Budget(scene.Actions,source)>QuestLogSource.ScriptBytes))return "save_quest_log_map_or_budget_invalid";
            if(ValidateAppearance(scene) is {} appearanceError)return appearanceError;
            if(catalog is not null&&!scene.Tiles.IsDefault&&scene.Tiles.Any(t=>!ValidAppearanceCoordinate(catalog,scene,t)))return "save_quest_log_tiles_invalid";
            if(catalog is not null&&scene.Layout is {} sceneLayout&&catalog.MapLayout(new(scene.Map),sceneLayout) is null)return "save_quest_log_layout_invalid";
            if(scene.Actors.Any(a=>!ValidActor(a))||scene.Actors.Select(a=>a.Id).Distinct(StringComparer.Ordinal).Count()!=scene.Actors.Length||scene.Actors.Count(a=>a.Id==PlayerId)!=1)return "save_quest_log_actor_invalid";
            if(VisualMemoryBudget(scene)>VisualMemoryLimit)return "save_quest_log_visual_budget_invalid";
            var projection=scene;int segmentStart=0,priorMovementEnd=0;
            for(int actionIndex=0;actionIndex<scene.Actions.Length;actionIndex++)
            {
                var action=scene.Actions[actionIndex];
                if(action is null||action.Frames is <1 or >255||action.StartFrame is <0 or >65535||action.SourceAction is {} opcode&&!Identifier(opcode)||!Enum.IsDefined(action.Event)||!Enum.IsDefined(action.Facing))return "save_quest_log_action_invalid";
                if(!IsCaption(action)&&action.StartFrame<segmentStart)return "save_quest_log_transition_time_invalid";
                if(action.TransitionTiming is not null&&action.Transition is null)return "save_quest_log_transition_timing_invalid";
                if(action.Transition is {} transition)
                {
                    if(ValidateTransitionTiming(action) is {} timingError)return timingError;
                    if(action.Event!=CampaignQuestLogEvent.None||action.ActorIndex!=-1||action.X!=0||action.Y!=0||action.Visible||action.TransitionTiming is null&&action.Frames!=1||action.Repeat||action.Tile is not null||action.Layout is not null||action.Graphics is not null||action.Spawn is not null||action.TextKey is not null||action.SourceAction is not null||action.LocationId is not null||!action.Arguments.IsDefault)return "save_quest_log_transition_invalid";
                    if(action.StartFrame<priorMovementEnd)return "save_quest_log_transition_time_invalid";
                    segmentStart=action.StartFrame+(action.TransitionTiming?.EndFrame??0);priorMovementEnd=action.StartFrame+action.Frames;
                    projection=ProjectionScene(transition);
                    if(ValidateProjection(projection,source,catalog) is {} transitionError)return transitionError;
                    if(source?.DisabledMaps.Contains(projection.Map)==true)return "save_quest_log_transition_disabled";
                    continue;
                }
                if(!IsCaption(action))priorMovementEnd=Math.Max(priorMovementEnd,action.StartFrame+action.Frames);
                if(action.LocationId is {} location&&(action.Event!=CampaignQuestLogEvent.Departed||location is <0 or >255||source is not null&&!source.Departures.Any(d=>d.LocationId==location)))return "save_quest_log_location_invalid";
                if(action.Repeat&&actionIndex>=5&&scene.Actions.Skip(actionIndex-4).Take(4).All(a=>a.Repeat))return "save_quest_log_repeat_invalid";
                if(action.Repeat&&(actionIndex==0||!Repeatable(action.Event)||action.Event==CampaignQuestLogEvent.PokemonCenter||scene.Actions[actionIndex-1].Event!=action.Event||scene.Actions[actionIndex-1].StartFrame!=action.StartFrame))return "save_quest_log_repeat_invalid";
                if(action.Event==CampaignQuestLogEvent.Input&&(action.ActorIndex!=-1||action.X!=0||action.Y!=0||action.Visible||action.Tile is not null||action.Layout is not null||action.Graphics is not null||action.Spawn is not null||action.TextKey is not null||!action.Arguments.IsDefault))return "save_quest_log_input_invalid";
                if(action.Tile is not null||action.Layout is not null)
                {if(action.Event!=CampaignQuestLogEvent.None||action.ActorIndex!=-1||action.Spawn is not null||action.Graphics is not null||action.X!=0||action.Y!=0||action.Visible||action.Tile is not null&&action.Layout is not null||action.Tile is {} tile&&!ValidTile(tile,true)||action.Layout is {} layout&&layout.Length>0&&!Identifier(layout))return "save_quest_log_appearance_invalid";}
                else if(action.Event==CampaignQuestLogEvent.None)
                {if((action.Spawn is null?(action.ActorIndex<0||action.ActorIndex>=projection.Actors.Length):action.ActorIndex!=-1||!ValidActor(action.Spawn))||action.X is <-32 or >1024||action.Y is <-32 or >1024)return "save_quest_log_actor_invalid";}
                else if(action.Spawn is not null||action.ActorIndex!=-1||action.X!=0||action.Y!=0||action.Visible)return "save_quest_log_action_invalid";
                if(source is not null&&IsCaption(action))
                {
                    if(action.TextKey is null||action.Arguments.IsDefault)return "save_quest_log_text_missing";
                    try{source.Expand(action.TextKey,"RED",action.Arguments);}catch(Exception e) when(e is KeyNotFoundException or InvalidDataException){return "save_quest_log_arguments_invalid";}
                }
                if(catalog is not null&&action.Tile is {} mapTile&&!ValidAppearanceCoordinate(catalog,projection,mapTile))return "save_quest_log_tiles_invalid";
                if(catalog is not null&&action.Layout is {Length:>0} actionLayout&&catalog.MapLayout(new(projection.Map),actionLayout) is null)return "save_quest_log_layout_invalid";
                if(action.Graphics is {} graphics&&!Identifier(graphics))return "save_quest_log_graphics_invalid";
                if(action.TextKey is {} key&&(!Identifier(key)||source is not null&&!source.Texts.ContainsKey(key)))return "save_quest_log_text_invalid";
                if(!action.Arguments.IsDefault&&(action.Arguments.Length>6||action.Arguments.Any(a=>a is null||a.Length>64||a.Any(char.IsControl))))return "save_quest_log_arguments_invalid";
            }
        }
        return null;
    }
    public static int Budget(ImmutableArray<CampaignQuestLogAction> actions,QuestLogSource? source=null)=>InitialActionBytes+QuestLogSource.SceneEndBytes+actions.Sum(a=>a is null?257:(source?.EventSizes.GetValueOrDefault((int)a.Event,257)??(IsCaption(a)?16:8))-(a.Repeat?4:0)+(a.Graphics is null?0:8)+(a.Spawn is null?0:8)+(a.TransitionTiming is null?0:8));
    private static bool Identifier(string? text)=>text is {Length:>0 and <=128}&&!text.Any(char.IsControl);
    private static bool ValidActor(CampaignQuestLogActor? actor)=>actor is not null&&Identifier(actor.Id)&&Identifier(actor.Graphics)&&actor.X is >=-32 and <=1024&&actor.Y is >=-32 and <=1024&&Enum.IsDefined(actor.Facing);
    private static ImmutableArray<CampaignQuestLogActor> Actors(CampaignState state,QuestLogSource? source)
    {
        var p=state.World.Player;
        return ImmutableArray.Create(new CampaignQuestLogActor(PlayerId,CampaignQuestLogAvatar.Graphics(state),p.X,p.Y,p.Facing,state.PlayerVisible))
            .AddRange((state.ObjectSlots?.Where(s=>s.Key!=0).Select(s=>state.Actors[s.Value])??state.Actors.Values.Where(a=>a.Active).OrderBy(a=>a.SourceLocalId))
                .Select(a=>new CampaignQuestLogActor(a.LocalId,Graphics(a.GraphicsId,state,source),a.X,a.Y,a.Facing,a.Visible)));

    }

    private static string Graphics(string symbol,CampaignState state,QuestLogSource? source)
    {
        const string prefix="OBJ_EVENT_GFX_VAR_";
        if(!symbol.StartsWith(prefix,StringComparison.Ordinal))return symbol;
        var variable="VAR_OBJ_GFX_ID_"+symbol[prefix.Length..];
        return source?.Graphics.GetValueOrDefault(state.Script.Variables.GetValueOrDefault(variable))??symbol;
    }

    // Called inside the existing authoritative accepted-command transaction. Nothing is
    // recorded for rejected/faulted commands. No RNG, flags, inventory or rewards are owned here.
    public static CampaignQuestLog? Record(CampaignQuestLog? log,CampaignState before,CampaignState after,CampaignCommand command,ICampaignCatalog? catalog=null,Func<string,Game.Domain.Trainers.TrainerDefinition?>? trainerLookup=null,bool recordingOpen=true,CampaignQuestLogAction? suppliedEvent=null,bool playedSlots=false,int? recordingFrame=null,CampaignQuestLogStartGate? startGate=null)
    {
        if(recordingFrame is <0 or >CampaignQuestLogFieldClock.FrameLimit)throw new ArgumentOutOfRangeException(nameof(recordingFrame));
        if(command.Kind=="play_time_tick")return log;
        if(log is not null&&after.PresentationEffects.Any(e=>e.Sequence>before.Script.EffectSequence&&e.Opcode=="special"&&e.Args.Contains("QuestLog_CutRecording"))){startGate?.EndRecording();return Close(log);}
        var source=(catalog as ICampaignQuestLogCatalog)?.QuestLogSource;
        if(source?.DisabledMaps.Contains(after.World.Player.MapId.Value)==true){startGate?.EndRecording();return Close(log);}
        var caption=suppliedEvent??Describe(before,after,command,catalog,source,trainerLookup,playedSlots);
        if(recordingOpen&&log is not null&&!log.Scenes[^1].Closed&&caption?.Event==CampaignQuestLogEvent.StoryItem)caption=null;

        var scenes=log?.Scenes??[];
        bool transitioned=false;
        if(recordingOpen&&!scenes.IsEmpty&&!scenes[^1].Closed&&(CurrentProjection(scenes[^1]).Map!=after.World.Player.MapId.Value||after.FieldTransition is {} acceptedTransition&&!ReferenceEquals(before.FieldTransition,acceptedTransition)&&acceptedTransition.From==before.World.Player&&acceptedTransition.To==after.World.Player))
        {
            if(!TryRecordMapTransition(scenes[^1],after,source,recordingFrame,out var crossed)){startGate?.EndRecording();return Close(log);}
            scenes=scenes.SetItem(scenes.Length-1,crossed);log=new(scenes);transitioned=true;
        }
        // Source retains its single wild-event pointer after movement overflow,
        // including quiet ordinary map changes. Update only that existing caption;
        // stopped recording cannot append another appearance or movement segment.
        if(startGate?.WildRecordRetained==true&&!scenes.IsEmpty&&scenes[^1].Closed&&caption?.Event==CampaignQuestLogEvent.WildWon)
        {
            var merged=CampaignQuestLogWildCapture.Merge(scenes[^1].Actions,caption);
            if(merged!=scenes[^1].Actions)return new(scenes.SetItem(scenes.Length-1,scenes[^1] with{Actions=merged}));
        }
        // A different semantic event, save/restore or explicit/disabled-map cut ends it.
        if((recordingOpen&&!scenes.IsEmpty&&!scenes[^1].Closed||startGate?.WildRecordRetained==true)&&!transitioned&&!scenes.IsEmpty&&CurrentProjection(scenes[^1]).Map==after.World.Player.MapId.Value&&caption?.Event==CampaignQuestLogEvent.WildWon)
        {
            if(!TryRecordAppearance(scenes[^1],before,after,source,out var wildVisual,recordingFrame)){startGate?.EndRecording();return Close(log);}
            var merged=CampaignQuestLogWildCapture.Merge(wildVisual.Actions,caption);
            if(merged!=wildVisual.Actions)return new(scenes.SetItem(scenes.Length-1,wildVisual with{Actions=merged}));
        }
        if(!recordingOpen||scenes.IsEmpty||scenes[^1].Closed||CurrentProjection(scenes[^1]).Map!=after.World.Player.MapId.Value)
        {
            if(caption is null||!MayStart(caption.Event,after,trainerLookup))return log;
            var scene=new CampaignQuestLogScene(after.World.Player.MapId.Value,Actors(after,source),[caption]){Closed=caption.Event is CampaignQuestLogEvent.FieldMove or CampaignQuestLogEvent.GymWon or CampaignQuestLogEvent.EliteFourWon or CampaignQuestLogEvent.ChampionWon or CampaignQuestLogEvent.TrainerWon};
            var appearance=CaptureAppearance(scene,after);
            if(appearance is null){startGate?.EndRecording();return Close(log);}
            if((!recordingOpen||scenes.IsEmpty||scenes[^1].Closed)&&startGate?.Admit(caption)==false)return log;
            startGate?.NoteEvent(caption);
            return new(scenes.Add(appearance).TakeLast(SceneLimit).ToImmutableArray());
        }
        var current=scenes[^1];
        var visual=current;
        if(!transitioned&&!TryRecordAppearance(current,before,after,source,out visual,recordingFrame)){startGate?.EndRecording();return Close(log);}
        var timed=AppendTimed(visual,transitioned?after:before,after,command,catalog,source,caption,out bool captionRecorded,transitioned?visual.Actions[^1].StartFrame:recordingFrame);
        if(!captionRecorded){startGate?.EndRecording();return Record(new(scenes.SetItem(scenes.Length-1,timed with{Closed=true})),before,after,command,catalog,trainerLookup,false,caption,playedSlots,recordingFrame,startGate);}
        if(caption is not null)startGate?.NoteEvent(caption);
        return ReferenceEquals(timed,current)?log:new(scenes.SetItem(scenes.Length-1,timed));
    }

    public static CampaignQuestLog? RecordEvents(CampaignQuestLog? log,CampaignState state,ImmutableArray<CampaignQuestLogAction> events,ICampaignCatalog catalog,bool recordingOpen,int? recordingFrame=null,CampaignQuestLogStartGate? startGate=null)
    {
        foreach(var entry in events)
        {
            var recorded=Record(log,state,state,new("quest_log_semantic_batch"),catalog,recordingOpen:recordingOpen,suppliedEvent:entry,recordingFrame:recordingFrame,startGate:startGate);
            if(recorded is not null&&(log is null||recorded.Scenes[^1].Actors!=log.Scenes[^1].Actors))recordingFrame=0;
            if(!ReferenceEquals(recorded,log))recordingOpen=recorded is not null&&!recorded.Scenes[^1].Closed;
            log=recorded;
        }
        return log;
    }
    public static CampaignQuestLogScene SavedScene(CampaignState state,QuestLogSource source)=>new(state.World.Player.MapId.Value,Actors(state,source),
        [Event(CampaignQuestLogEvent.Arrived,"SavedGameAtLocation",source.MapName(state.World.Player.MapId.Value))]){Layout=state.MapLayout,Tiles=state.MapEdits?.Values.ToImmutableArray()??[]};
    public static CampaignQuestLog? Close(CampaignQuestLog? log)=>log is null||log.Scenes[^1].Closed?log:new(log.Scenes.SetItem(log.Scenes.Length-1,log.Scenes[^1] with{Closed=true}));
    private static bool MayStart(CampaignQuestLogEvent kind,CampaignState state,Func<string,Game.Domain.Trainers.TrainerDefinition?>? trainers)
    {
        if(kind is CampaignQuestLogEvent.FieldMove or CampaignQuestLogEvent.PokemonCenter)return false;
        if(kind==CampaignQuestLogEvent.TrainerWon&&!state.Script.Flags.GetValueOrDefault("FLAG_SYS_GAME_CLEAR")&&(state.DoubleBattle is {Stage:CampaignDoubleBattleStage.Finished} finishedDouble?finishedDouble.Trainer.Value:state.ForestTrainerHistory?.LastOrDefault()?.TrainerId) is {} trainerId&&trainers?.Invoke(trainerId)?.TrainerClass is {} trainerClass&&trainerClass is not ("TRAINER_CLASS_RIVAL_EARLY" or "TRAINER_CLASS_RIVAL_LATE" or "TRAINER_CLASS_CHAMPION" or "TRAINER_CLASS_BOSS"))return false;
        if(!state.Script.Flags.GetValueOrDefault("FLAG_SYS_GAME_CLEAR")&&kind is CampaignQuestLogEvent.PartyOrder or CampaignQuestLogEvent.WildWon)return false;
        if(!state.Script.Flags.GetValueOrDefault("FLAG_SYS_CAN_LINK_WITH_RS")&&kind is CampaignQuestLogEvent.UsedItem or CampaignQuestLogEvent.GaveHeldItem or CampaignQuestLogEvent.GaveHeldItemBag or CampaignQuestLogEvent.GaveHeldItemPc or CampaignQuestLogEvent.TookHeldItem or CampaignQuestLogEvent.SwappedHeldItem or CampaignQuestLogEvent.SwappedHeldItemPc or CampaignQuestLogEvent.SwitchedPartyPc or CampaignQuestLogEvent.WithdrewPokemon or CampaignQuestLogEvent.DepositedPokemon)return false;
        return true;
    }
    internal static CampaignQuestLogAction Event(CampaignQuestLogEvent kind,string text,params string[] args)=>new(-1,0,0,Direction.South,false,kind,127){TextKey=text,Arguments=args.ToImmutableArray()};
    public static CampaignQuestLogAction? FirstArrival(CampaignState before,CampaignState after,QuestLogSource source)=>
        source.ArrivalFlags.Any(flag=>!before.Script.Flags.GetValueOrDefault(flag)&&after.Script.Flags.GetValueOrDefault(flag))?
            Event(CampaignQuestLogEvent.Arrived,"ArrivedInLocation",source.MapNames.GetValueOrDefault(after.World.Player.MapId.Value)??Location(after.World.Player.MapId.Value)):null;
    private static CampaignQuestLogAction? Describe(CampaignState before,CampaignState after,CampaignCommand command,ICampaignCatalog? catalog,QuestLogSource? source,Func<string,Game.Domain.Trainers.TrainerDefinition?>? trainerLookup,bool playedSlots)
    {
        string map=source?.MapNames.GetValueOrDefault(after.World.Player.MapId.Value)??Location(after.World.Player.MapId.Value);
        string Species(Game.Domain.Pokemon.PartyPokemon mon)=>catalog?.SourceSpeciesName(mon.Pokemon.SourceSpeciesId)??mon.Nickname;
        string Item(string item)=>catalog?.Inventory.Items.GetValueOrDefault(new Game.Domain.Ids.ItemId(item.StartsWith("ITEM_",StringComparison.Ordinal)?"pret:"+item:item))?.Name??item;
        foreach(var effect in after.PresentationEffects.Where(e=>e.Sequence>before.Script.EffectSequence))
            if(effect.Opcode=="special"&&effect.Args.Contains("SetUsedPkmnCenterQuestLogEvent"))return Event(CampaignQuestLogEvent.PokemonCenter,"MonsWereFullyRestoredAtCenter");
        if(source is not null&&FirstArrival(before,after,source) is {} arrival)return arrival;
        if(source is not null&&CampaignQuestLogDepartureCapture.Observe(before,after,source,playedSlots).Event is {} departure)return departure;
        string SpeciesId(int species)=>catalog?.SourceSpeciesName(species)??"POKéMON";
        if(before.CutEffect is null&&after.CutEffect is {} cut)return Event(CampaignQuestLogEvent.FieldMove,"UsedCut",SpeciesId(cut.SourceSpeciesId));
        if(before.RockSmashEffect is null&&after.RockSmashEffect is {} smash)return Event(CampaignQuestLogEvent.FieldMove,"UsedRockSmash",SpeciesId(smash.SourceSpeciesId));
        if(before.FlashEffect is null&&after.FlashEffect is {} flash)return Event(CampaignQuestLogEvent.FieldMove,"UsedFlash",SpeciesId(flash.SourceSpeciesId));
        if(before.StrengthEffect is null&&after.StrengthEffect is {} strength)return Event(CampaignQuestLogEvent.FieldMove,"UsedStrength",SpeciesId(strength.SourceSpeciesId));
        if(!before.IsSurfing&&after.IsSurfing&&after.SurfEffect is {} surf)return Event(CampaignQuestLogEvent.FieldMove,"UsedSurf",SpeciesId(surf.SourceSpeciesId));
        if(before.FourIslandMovement is null&&after.FourIslandMovement?.Movement is {Kind:FourIslandMovementKind.WaterfallAscent} waterfall&&after.Party.FirstOrDefault(p=>p.InstanceId==waterfall.MoveOwnerInstanceId) is {} waterfallOwner)
            return Event(CampaignQuestLogEvent.FieldMove,"UsedWaterfall",Species(waterfallOwner));
        if(before.FlyEffect is {Destination:not null} fly&&after.FlyEffect is null)return Event(CampaignQuestLogEvent.FieldMove,"UsedFly",SpeciesId(fly.SourceSpeciesId),map);
        if(after.NoncombatFieldEffect is {} noncombat&&
            (before.NoncombatFieldEffect is {Phase:"confirm"}&&noncombat.Phase=="show-mon"||before.NoncombatFieldEffect is null&&noncombat.Move=="pret:MOVE_SWEET_SCENT"))
        {
            string owner=SpeciesId(noncombat.SourceSpeciesId);
            if(noncombat.Move=="pret:MOVE_DIG")return Event(CampaignQuestLogEvent.FieldMove,"UsedDigInLocation",owner,source?.MapName(noncombat.Origin.MapId.Value)??map);
            if(noncombat.Move=="pret:MOVE_TELEPORT"&&noncombat.Destination is {} destination)
                return Event(CampaignQuestLogEvent.FieldMove,"UsedTeleportToLocation",owner,source?.MapName(destination.MapId.Value)??Location(destination.MapId.Value),destination.MapId.Value=="map.pallet-town"?source?.Texts["Home"]??"home":"POKéMON CENTER");
            if(noncombat.Move=="pret:MOVE_SWEET_SCENT")return Event(CampaignQuestLogEvent.FieldMove,"UsedSweetScent",owner);
        }
        if(command.Kind=="field_heal"&&command.PartyIndex>=0&&command.PartyIndex<before.Party.Length&&command.PartyIndex<after.Party.Length&&
            before.Party[command.PartyIndex].Pokemon.CurrentHp!=after.Party[command.PartyIndex].Pokemon.CurrentHp&&command.Text is "pret:MOVE_SOFT_BOILED" or "pret:MOVE_MILK_DRINK")
            return Event(CampaignQuestLogEvent.FieldMove,command.Text=="pret:MOVE_MILK_DRINK"?"UsedMilkDrink":"UsedSoftboiled",Species(before.Party[command.PartyIndex]));
        var opening=after.BattleHistory.LastOrDefault();
        if(opening is not null&&opening!=before.BattleHistory.LastOrDefault()&&opening.Outcome==Game.Domain.FirstBattle.FirstBattleOutcome.Won&&after.Battle is {} lab)
        {
            var p=lab.State.Player.Pokemon;var o=lab.State.Opponent.Pokemon;
            string flavor=source?.Texts[opening.HpFraction==2?"Somehow":opening.HpFraction==1?"Tenaciously":"Handily"]??"handily";
            return Event(CampaignQuestLogEvent.TrainerWon,"TookOnTrainersMonWithMonAndWon",map,after.RivalName,SpeciesId(o.SourceSpeciesId),SpeciesId(p.SourceSpeciesId),flavor);
        }
        var trainer=after.ForestTrainerHistory?.LastOrDefault();
        if(trainer is not null&&trainer!=before.ForestTrainerHistory?.LastOrDefault()&&trainer.Outcome==Game.Domain.WildBattle.WildBattleOutcome.Won&&trainerLookup?.Invoke(trainer.TrainerId) is {} definition&&after.ForestTrainerBattle is {} battle)
        {
            var player=battle.Combat.Battle.Player.Pokemon;var opponent=battle.Combat.Battle.Opponent.Pokemon;
            string victor=SpeciesId(player.SourceSpeciesId),loser=SpeciesId(opponent.SourceSpeciesId);
            string flavor=source?.Texts[player.CurrentHp<player.Stats.MaxHp/3?"Somehow":player.CurrentHp<player.Stats.MaxHp/3*2?"Tenaciously":"Handily"]??"handily";
            return definition.TrainerClass switch
            {
                "TRAINER_CLASS_LEADER"=>Event(CampaignQuestLogEvent.GymWon,"TookOnGymLeadersMonWithMonAndWon",map,definition.SourceTrainerName,loser,victor,flavor),
                "TRAINER_CLASS_ELITE_FOUR"=>Event(CampaignQuestLogEvent.EliteFourWon,"TookOnEliteFoursMonWithMonAndWon",definition.SourceTrainerName,loser,victor,flavor),
                "TRAINER_CLASS_CHAMPION"=>Event(CampaignQuestLogEvent.ChampionWon,"PlayerBattledChampionRival",after.PlayerName,after.RivalName,loser,victor,source?.Texts[player.CurrentHp<player.Stats.MaxHp/3?"Barely":player.CurrentHp<player.Stats.MaxHp/3*2?"Somehow":"Coolly"]??"coolly"),
                _=>Event(CampaignQuestLogEvent.TrainerWon,"TookOnTrainersMonWithMonAndWon",map,definition.SourceTrainerName,loser,victor,flavor)
            };
        }
        var pairHistory=after.DoubleBattleHistory?.LastOrDefault();
        if(pairHistory is not null&&pairHistory!=before.DoubleBattleHistory?.LastOrDefault()&&pairHistory.Outcome==Game.Domain.WildBattle.WildBattleOutcome.Won&&
            after.DoubleBattle is {} pair&&pair.Id==pairHistory.Id&&pair.Frame.LastOpponentFaint is {ScriptAttacker:{} attacker} last&&
            trainerLookup?.Invoke(pairHistory.TrainerId) is {} pairTrainer)
        {
            var left=pair.Frame.Get(new(0)).Body!.Pokemon;var right=pair.Frame.Get(new(2)).Body!.Pokemon;
            var victor=attacker.Side==Game.Domain.Battle.BattleSide.Player?pair.Frame.Get(attacker).Body!.Pokemon:left.CurrentHp!=0?left:right;
            int hp=left.CurrentHp+right.CurrentHp,maxHp=left.Stats.MaxHp+right.Stats.MaxHp;
            string flavor=source?.Texts[hp<maxHp/3?"Somehow":hp<maxHp/3*2?"Tenaciously":"Handily"]??"handily";
            return Event(CampaignQuestLogEvent.TrainerWon,"TookOnTrainersMonWithMonAndWon",map,pairTrainer.SourceTrainerName,SpeciesId(last.Species),SpeciesId(victor.SourceSpeciesId),flavor);
        }
        if(command.Kind=="machine_teach"&&command.PartyIndex>=0&&command.PartyIndex<before.Party.Length&&command.PartyIndex<after.Party.Length&&catalog is not null)
        {
            var old=before.Party[command.PartyIndex];var mon=after.Party[command.PartyIndex];
            var machine=catalog.Machines.Machines.FirstOrDefault(m=>m.Machine.Item.Id==(command.Text is null?Game.Domain.Items.MachineRules.Tm39Id:new Game.Domain.Ids.ItemId(command.Text)));
            if(machine is not null&&!old.Pokemon.Moves.SequenceEqual(mon.Pokemon.Moves))
            {
                string move=catalog.BattleContent!.MoveNames[machine.Move.Value];
                var forgotten=old.Pokemon.Moves.FirstOrDefault(m=>!mon.Pokemon.Moves.Any(n=>n.Move==m.Move));
                bool hm=machine.Machine.Item.SourceItemSymbol.StartsWith("ITEM_HM",StringComparison.Ordinal);
                return forgotten is null?Event(CampaignQuestLogEvent.UsedItem,hm?"MonLearnedMoveFromHM":"MonLearnedMoveFromTM",Species(mon),move):
                    Event(CampaignQuestLogEvent.UsedItem,hm?"MonReplacedMoveWithHM":"MonReplacedMoveWithTM",Species(mon),move,catalog.BattleContent.MoveNames[forgotten.Move.Value]);
            }
        }
        if(catalog is not null&&CampaignQuestLogItemUseCapture.Observe(before,after,catalog,command) is {} used)
            return used.KeyItem?Event(CampaignQuestLogEvent.UsedItem,"UsedTheKeyItem",Item(used.Item.Value)):
                used.TargetSpeciesId is {} targetSpecies?Event(CampaignQuestLogEvent.UsedItem,"UsedItemOnMonAtThisLocation",Item(used.Item.Value),SpeciesId(targetSpecies)):
                Event(CampaignQuestLogEvent.UsedItem,"UsedTheItem",Item(used.Item.Value));
        if(before.TeachyTv is null&&after.TeachyTv is not null)return Event(CampaignQuestLogEvent.UsedItem,"UsedTheKeyItem",Item("ITEM_TEACHY_TV"));
        if(before.FameChecker is null&&after.FameChecker is not null)return Event(CampaignQuestLogEvent.UsedItem,"UsedTheKeyItem",Item("ITEM_FAME_CHECKER"));
        if(before.CoinsBox is null&&after.CoinsBox is not null)return Event(CampaignQuestLogEvent.UsedItem,"UsedTheKeyItem",Item("ITEM_COIN_CASE"));
        if(before.EscapeRope is null&&after.EscapeRope is not null)return Event(CampaignQuestLogEvent.UsedItem,"UsedEscapeRope",Item("ITEM_ESCAPE_ROPE"),source?.MapName(before.World.Player.MapId.Value)??map);
        var heldUse=CampaignQuestLogHeldItemCapture.Observe(before,after,command);
        foreach(var mon in after.Party.Where(p=>p.InstanceId==heldUse?.InstanceId))
        {
            var old=before.Party.FirstOrDefault(p=>p.InstanceId==mon.InstanceId);
            if(old is null||old.HeldItem==mon.HeldItem)continue;
            bool oldEmpty=old.HeldItem is "" or "ITEM_NONE" or "item.none" or "none";bool newEmpty=mon.HeldItem is "" or "ITEM_NONE" or "item.none" or "none";
            if(oldEmpty)
            {
                if(heldUse!.Origin==CampaignQuestLogHeldItemOrigin.Pc)return Event(CampaignQuestLogEvent.GaveHeldItemPc,"GaveMonHeldItemFromPC",Item(mon.HeldItem),Species(mon));
                bool bag=heldUse!.Origin==CampaignQuestLogHeldItemOrigin.Bag;
                return Event(bag?CampaignQuestLogEvent.GaveHeldItemBag:CampaignQuestLogEvent.GaveHeldItem,bag?"GaveMonHeldItem2":"GaveMonHeldItem",Species(mon),Item(mon.HeldItem));
            }
            if(newEmpty)return Event(CampaignQuestLogEvent.TookHeldItem,"TookHeldItemFromMon",Species(mon),Item(old.HeldItem));
            return heldUse!.Origin==CampaignQuestLogHeldItemOrigin.Pc?Event(CampaignQuestLogEvent.SwappedHeldItemPc,"SwappedHeldItemFromPC",Item(mon.HeldItem),Species(mon),Item(old.HeldItem)):
                Event(CampaignQuestLogEvent.SwappedHeldItem,"SwappedHeldItemsOnMon",Species(mon),Item(old.HeldItem),Item(mon.HeldItem));
        }
        if(before.Party.Length==after.Party.Length&&before.Party.Select(p=>p.InstanceId).Order().SequenceEqual(after.Party.Select(p=>p.InstanceId).Order())&&!before.Party.Select(p=>p.InstanceId).SequenceEqual(after.Party.Select(p=>p.InstanceId)))
        {
            int index=Enumerable.Range(0,before.Party.Length).First(i=>before.Party[i].InstanceId!=after.Party[i].InstanceId);
            return Event(CampaignQuestLogEvent.PartyOrder,"SwitchMon1WithMon2",Species(before.Party[index]),Species(after.Party[index]));
        }
        if(before.Inventory is {} bi&&after.Inventory is {} ai)
        {
            if(source is not null&&catalog is not null)
                foreach(var stack in ai.BagItems.Concat(ai.KeyItems??[]))
                    if(stack.Quantity>bi.BagItems.Concat(bi.KeyItems??[]).Where(x=>x.Item==stack.Item).Sum(x=>x.Quantity)&&catalog.Inventory.Items.TryGetValue(stack.Item,out var storyItemDefinition)&&source.StoryItems.Contains(storyItemDefinition.SourceItemSymbol)&&(storyItemDefinition.SourceItemSymbol!="ITEM_TOWN_MAP"||after.World.Player.MapId.Value==source.TownMapGiftMap))
                        return Event(CampaignQuestLogEvent.StoryItem,"ObtainedItemInLocation",map,storyItemDefinition.Name);
            foreach(var stack in ai.PcItems)
                if(stack.Quantity>bi.PcItems.Where(x=>x.Item==stack.Item).Sum(x=>x.Quantity))return Event(CampaignQuestLogEvent.DepositedItem,"StoredItemInPC",Item(stack.Item.Value));
            foreach(var stack in bi.PcItems)
                if(stack.Quantity>ai.PcItems.Where(x=>x.Item==stack.Item).Sum(x=>x.Quantity))return Event(CampaignQuestLogEvent.WithdrewItem,"WithdrewItemFromPC",Item(stack.Item.Value));

        }
        if(before.Pc?.GroupCursor is {} group&&after.Pc?.GroupCursor is null&&command.Kind=="storage_group_place"&&after.Storage is {} gs&&command.StorageLocation is {} groupTarget)
            return Event(CampaignQuestLogEvent.MovedGroup,"SwitchedMultipleMons",gs.Boxes[group.Origin.BoxIndex].Name??"BOX "+(group.Origin.BoxIndex+1),gs.Boxes[groupTarget.BoxIndex].Name??"BOX "+(groupTarget.BoxIndex+1));
        if(before.Pc?.MoveCursor is {} held&&command.StorageLocation is {} target&&command.Kind is "storage_move_place" or "storage_move_shift"&&after.Storage is {} targetStorage)
        {
            string Box(int index)=>targetStorage.Boxes[index].Name??"BOX "+(index+1);
            var origin=held.Origin;
            if(command.Kind=="storage_move_shift"&&after.Pc?.MoveCursor is {} other)
            {
                if(origin.IsParty!=target.IsParty)
                    return origin.IsParty?Event(CampaignQuestLogEvent.SwitchedPartyPc,"SwitchedPartyMonForPCMon",Box(target.BoxIndex),Species(other.Pokemon),Species(held.Pokemon)):
                        Event(CampaignQuestLogEvent.SwitchedPartyPc,"SwitchedPartyMonForPCMon",Box(origin.BoxIndex),Species(held.Pokemon),Species(other.Pokemon));
                if(!origin.IsParty)return origin.BoxIndex==target.BoxIndex?
                    Event(CampaignQuestLogEvent.SwitchedWithinBox,"SwitchedMonsWithinBox",Box(origin.BoxIndex),Species(held.Pokemon),Species(other.Pokemon)):
                    Event(CampaignQuestLogEvent.SwitchedBoxes,"SwitchedMonsBetweenBoxes",Box(origin.BoxIndex),Species(held.Pokemon),Box(target.BoxIndex),Species(other.Pokemon));
            }
            else if(after.Pc?.MoveCursor is null)
            {
                if(origin.IsParty&&!target.IsParty)return Event(CampaignQuestLogEvent.DepositedPokemon,"DepositedMonInPC",Species(held.Pokemon),Box(target.BoxIndex));
                if(!origin.IsParty&&target.IsParty)return Event(CampaignQuestLogEvent.WithdrewPokemon,"WithdrewMonFromPC",Box(origin.BoxIndex),Species(held.Pokemon));
                if(!origin.IsParty&&!target.IsParty)return origin.BoxIndex==target.BoxIndex?
                    Event(CampaignQuestLogEvent.MovedWithinBox,"MovedMonWithinBox",Box(origin.BoxIndex),Species(held.Pokemon)):
                    Event(CampaignQuestLogEvent.MovedBoxes,"MovedMonToNewBox",Box(origin.BoxIndex),Species(held.Pokemon),Box(target.BoxIndex));
            }
        }
        if(before.Storage is {} bs&&after.Storage is {} storage&&before.Pc is not null)
        {
            for(int box=0;box<storage.Boxes.Length;box++)foreach(var mon in storage.Boxes[box].Slots.Where(p=>p is not null))
                if(before.Party.Any(p=>p.InstanceId==mon!.InstanceId)&&!after.Party.Any(p=>p.InstanceId==mon!.InstanceId))return Event(CampaignQuestLogEvent.DepositedPokemon,"DepositedMonInPC",Species(mon!),storage.Boxes[box].Name??"BOX "+(box+1));
            foreach(var mon in after.Party)
                for(int box=0;box<bs.Boxes.Length;box++)if(bs.Boxes[box].Slots.Any(p=>p?.InstanceId==mon.InstanceId)&&!before.Party.Any(p=>p.InstanceId==mon.InstanceId))return Event(CampaignQuestLogEvent.WithdrewPokemon,"WithdrewMonFromPC",bs.Boxes[box].Name??"BOX "+(box+1),Species(mon));
        }
        var wild=after.Exploration?.Battles.LastOrDefault();
        if(wild is not null&&wild!=before.Exploration?.Battles.LastOrDefault()&&source?.ExcludedWildSpecies.Contains(wild.SourceSpeciesId)!=true)
        {
            string species=catalog?.SourceSpeciesName(wild.SourceSpeciesId)??"POKéMON";
            if(wild.Outcome==Game.Domain.WildBattle.WildBattleOutcome.Won)return Event(CampaignQuestLogEvent.WildWon,"DefeatedWildMon",map,species,"1","","0",after.PlayerName);
            if(wild.Outcome==Game.Domain.WildBattle.WildBattleOutcome.Caught)return Event(CampaignQuestLogEvent.WildWon,"CaughtWildMon",map,"","0",species,"1",after.PlayerName);
        }
        return null;
    }
    internal static string Location(string id)
    {
        var text=System.Text.RegularExpressions.Regex.Replace(id.Replace('_',' ').Replace('-',' '),"(?<=[a-z])(?=[A-Z])"," ");
        return text.Length<=100?text:text[..100];
    }
}

// Transient playback cursor: the saved campaign is never swapped out, advanced, or
// re-executed. Finishing or skipping therefore returns the identical authoritative state.
public sealed class CampaignQuestLogPlayback
{
    private readonly CampaignQuestLog _log;
    private readonly string _playerName;
    private readonly QuestLogSource? _source;
    private readonly CampaignQuestLogScene? _savedScene;
    private CampaignQuestLogTimeline _timeline=null!;
    private string? _savedCaption;
    private double _savedWait,_exitFrames;
    public bool Ending{get;private set;}
    // Task_EndQuestLog: blank header, 16 one-pixel bar updates, one
    // removal update, 33 hold updates, then release field controls.
    public bool HeaderVisible=>!Ending||_exitFrames+1e-7<1;
    public int BarHeight=>Ending?Math.Clamp(17-(int)Math.Floor(_exitFrames+1e-7),0,16):16;
    // RestoreScreenAfterPlayback shares the bar-update phase: header first, then tones 15 through 0.
    public int GrayscaleCoefficient=>Ending?Math.Clamp(17-(int)Math.Floor(_exitFrames+1e-7),0,16):16;
    public bool SavedGameVisible{get;private set;}
    private CampaignQuestLogScene CurrentScene=>SavedGameVisible?_savedScene!:_log.Scenes[SceneIndex];
    public int SceneIndex{get;private set;}
    public int SceneCount=>_log.Scenes.Length;
    public bool Finished{get;private set;}
    public string Map=>_timeline.Map;
    public int ArtRevision=>_timeline.ArtRevision;
    public double Frame=>_timeline.Frame;
    public CampaignQuestLogTransitionTiming? ActiveTransitionTiming=>_timeline.ActiveTransitionTiming;
    public double TransitionElapsedFrames=>_timeline.TransitionElapsedFrames;
    public string? Layout=>_timeline.Layout;
    public ImmutableArray<FieldMetatileEdit> Tiles=>_timeline.Tiles;
    public string? Caption=>Ending?null:SavedGameVisible?_savedCaption:_timeline.Caption;
    public IEnumerable<CampaignQuestLogActor> Actors=>_timeline.Actors;
    public (CampaignQuestLogActor From,double Fraction,int DrawYOffset,bool IsJump)? Movement(string id)=>_timeline.Movement(id);
    public CampaignQuestLogPlayback(CampaignQuestLog log,string playerName="RED",QuestLogSource? source=null,CampaignQuestLogScene? savedScene=null)
    {
        if(CampaignQuestLogRecorder.Validate(log,source) is {} error)throw new InvalidDataException(error);
        _log=log;_playerName=playerName;_source=source;_savedScene=savedScene;BeginScene();
    }
    private void BeginScene()
    {
        _timeline=new(CurrentScene,_playerName,_source,SavedGameVisible);
        if(SavedGameVisible){_savedCaption=_timeline.Caption;_savedWait=0;}
    }
    private void BeginExit(){Ending=true;_exitFrames=0;}
    private void AdvanceExit(double frames)
    {
        _exitFrames+=frames;
        if(_exitFrames+1e-7>=52)Finished=true;
    }
    public void Tick(double seconds)
    {
        if(!double.IsFinite(seconds)||seconds<0)throw new ArgumentOutOfRangeException(nameof(seconds));
        if(Finished)return;
        if(Ending){_timeline.Tick(seconds);AdvanceExit(seconds*60);return;}
        if(SavedGameVisible)
        {
            _timeline.Tick(seconds);
            // The source checks timer >=127 before incrementing: the caption
            // closes on the 128th task update, then EndQuestLog starts next frame.
            _savedWait+=seconds*60;
            if(_savedWait+1e-7>=128){BeginExit();AdvanceExit(Math.Max(0,_savedWait-128));}
            return;
        }
        _timeline.Tick(seconds);if(_timeline.Finished)NextScene();
    }
    public void NextScene()
    {
        if(Finished||Ending)return;
        if(SavedGameVisible){BeginExit();return;}
        if(SceneIndex+1==SceneCount)
        {
            if(_savedScene is null)Finished=true;
            else{SavedGameVisible=true;BeginScene();}
            return;
        }
        SceneIndex++;BeginScene();
    }
    public void Skip()
    {
        if(Finished||Ending)return;
        if(_savedScene is null){Finished=true;return;}
        if(!SavedGameVisible){SavedGameVisible=true;BeginScene();}
        // B selects END_MODE_FINISH, which suppresses the final saved caption.
        BeginExit();
    }
}

public sealed partial class CampaignSession
{
    private Game.Domain.Trainers.TrainerDefinition? QuestLogTrainer(string id)
    {
        var key=new Game.Domain.Ids.TrainerId(id);
        if(OrdinaryTrainerBindings.TryGetValue(key,out var ordinary))return ordinary.Definition;
        if(DoubleTrainers.Trainers.TryGetValue(key,out var pair))return pair;
        return _content.VsSeekerTrainers?.Trainers.GetValueOrDefault(key);
    }

    private bool _questLogRecordingOpen;
    private CampaignQuestLogStartGate? _questLogStartGate;
    private CampaignQuestLogShopCapture? _questLogShop;
    private bool _questLogPlayedSlots;
    public void StopQuestLogRecording(){DiscardUnfinishedFieldTransitionObservation();_questLogRecordingOpen=false;_questLogFieldClock.Reset();_questLogStartGate?.EndRecording();}
    private Decision<CampaignState> DecideWithQuestLogRecording(CampaignState state,CampaignCommand command)
    {
        var previousLog=state.QuestLog;
        state=ReconcileFieldTransitionObservation(state);
        if(state.FieldTransition is not null)state=state with{FieldTransition=null};
        var result=DecideWithWildHistoryRetention(state,command);
        if(result.Status!=CommandStatus.Accepted)return result;
        if(state.FieldWeather is not null&&result.State.FieldTransition is {Cause:FieldTransitionCause.PlainWarp})result=result with{State=result.State with{FieldTransition=null}};
        _questLogStartGate??=new(state.QuestLog);
        if((result.State.SlotMachine?.PlayedSpins??0)>(state.SlotMachine?.PlayedSpins??0))_questLogPlayedSlots=true;
        var departure=(_content as ICampaignQuestLogCatalog)?.QuestLogSource is {} departureSource?
            CampaignQuestLogDepartureCapture.Observe(state,result.State,departureSource,_questLogPlayedSlots):(result.State,(CampaignQuestLogAction?)null,false);
        result=result with{State=departure.Item1};
        var log=CampaignQuestLogRecorder.Record(state.QuestLog,state,result.State,command,_content,
            id=>QuestLogTrainer(id),_questLogRecordingOpen,suppliedEvent:departure.Item2,playedSlots:_questLogPlayedSlots,recordingFrame:_questLogFieldClock.Frame,startGate:_questLogStartGate);
        if(departure.Item3)_questLogPlayedSlots=false;
        var shop=CampaignQuestLogShopCapture.Observe(_questLogShop,state,result.State,_content);
        bool recordingOpen=ReferenceEquals(log,previousLog)?_questLogRecordingOpen:log is not null&&!log.Scenes[^1].Closed;
        var followUps=shop.Events;
        // Map-load departure runs before the new map's first-arrival script event.
        // Both may occur in one accepted host command; retain both bounded captions.
        if(departure.Item2 is not null&&(_content as ICampaignQuestLogCatalog)?.QuestLogSource is {} arrivals&&
            CampaignQuestLogRecorder.FirstArrival(state,result.State,arrivals) is {} arrival)
            followUps=followUps.Insert(0,arrival);
        int followUpFrame=log is not null&&(state.QuestLog is null||log.Scenes[^1].Actors!=state.QuestLog.Scenes[^1].Actors)?0:_questLogFieldClock.Frame;
        log=CampaignQuestLogRecorder.RecordEvents(log,result.State,followUps,_content,recordingOpen,followUpFrame,_questLogStartGate);
        bool inputBoundary=command.Kind is "field_start_menu" or "use_registered" || (command.Kind is "interact" or "move" or "cycle_input") && QuestLogFieldControlsAvailable(state)&&(!QuestLogFieldControlsAvailable(result.State)||state.Battle is null&&result.State.Battle is not null||state.SafariBattle is null&&result.State.SafariBattle is not null||state.WildBattle is null&&result.State.WildBattle is not null||state.DoubleBattle is null&&result.State.DoubleBattle is not null||state.ForestTrainerBattle is null&&result.State.ForestTrainerBattle is not null);
        if(inputBoundary)log=CampaignQuestLogRecorder.RecordFieldInput(log,followUpFrame,(_content as ICampaignQuestLogCatalog)?.QuestLogSource,recordingOpen);
        _questLogShop=shop.Capture;
        if(!ReferenceEquals(log,previousLog))
        {
            _questLogRecordingOpen=log is not null&&!log.Scenes[^1].Closed;
            SynchronizeQuestLogFieldClock(previousLog,log);
        }
        CompleteQuestLogScriptDelay(state,result.State,log);
        result=ReferenceEquals(log,result.State.QuestLog)?result:result with{State=result.State with{QuestLog=log}};
        CaptureFieldTransitionObservation(state,result.State,command);
        return result;
    }
}
