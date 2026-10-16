using System.Collections.Immutable;
using Game.Domain.World;

namespace Game.Application;

// A display recording, never executable game commands or copies of the campaign.
public sealed record CampaignQuestLogActor(string Id,string Graphics,int X,int Y,Direction Facing,bool Visible);
public enum CampaignQuestLogEvent
{
    None=2, PartyOrder=3, UsedItem=4, GaveHeldItem=5, GaveHeldItemBag=6, GaveHeldItemPc=7, TookHeldItem=8, SwappedHeldItem=9, SwappedHeldItemPc=10,
    PokemonCenter=11, SwitchedBoxes=20, SwitchedWithinBox=21, SwitchedPartyPc=22,
    MovedBoxes=23, MovedWithinBox=24, WithdrewPokemon=25, DepositedPokemon=26,
    MovedGroup=27, DepositedItem=28, WithdrewItem=29, GymWon=30, WildWon=31,
    EliteFourWon=32, ChampionWon=33, TrainerWon=34, Departed=35, FieldMove=36,
    Bought=37, Sold=38, StoryItem=40, Arrived=42
}
public sealed record CampaignQuestLogAction(int ActorIndex,int X,int Y,Direction Facing,bool Visible,CampaignQuestLogEvent Event,int Frames)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string? TextKey{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] public ImmutableArray<string> Arguments{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string? Graphics{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public CampaignQuestLogActor? Spawn{get;init;}
}
public sealed record CampaignQuestLogScene(string Map,ImmutableArray<CampaignQuestLogActor> Actors,ImmutableArray<CampaignQuestLogAction> Actions)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] public bool Closed{get;init;}
}
public sealed record CampaignQuestLog(ImmutableArray<CampaignQuestLogScene> Scenes);

public static class CampaignQuestLogRecorder
{
    public const int SceneLimit=4, ActorLimit=16, ActionLimit=32;
    public const string PlayerId="LOCALID_PLAYER";

    public static string? Validate(CampaignQuestLog? log,QuestLogSource? source=null)
    {
        if(log is null)return null;
        if(log.Scenes.IsDefault||log.Scenes.Length is <1 or >SceneLimit)return "save_quest_log_scenes_invalid";
        foreach(var scene in log.Scenes)
        {
            if(scene is null||!Identifier(scene.Map)||scene.Actors.IsDefault||scene.Actors.Length is <1 or >ActorLimit||scene.Actions.IsDefault||scene.Actions.Length is <1 or >ActionLimit)return "save_quest_log_scene_invalid";
            if(source is not null&&(!source.MapNames.ContainsKey(scene.Map)||Budget(scene.Actions,source)>QuestLogSource.ScriptBytes))return "save_quest_log_map_or_budget_invalid";
            if(scene.Actors.Any(a=>!ValidActor(a))||scene.Actors.Select(a=>a.Id).Distinct(StringComparer.Ordinal).Count()!=scene.Actors.Length||scene.Actors.Count(a=>a.Id==PlayerId)!=1)return "save_quest_log_actor_invalid";
            foreach(var action in scene.Actions)
            {
                if(action is null||action.Frames is <1 or >255||!Enum.IsDefined(action.Event)||!Enum.IsDefined(action.Facing))return "save_quest_log_action_invalid";
                if(action.Event==CampaignQuestLogEvent.None)
                {if((action.Spawn is null?(action.ActorIndex<0||action.ActorIndex>=scene.Actors.Length):action.ActorIndex!=-1||!ValidActor(action.Spawn))||action.X is <-32 or >1024||action.Y is <-32 or >1024)return "save_quest_log_actor_invalid";}
                else if(action.Spawn is not null||action.ActorIndex!=-1||action.X!=0||action.Y!=0||action.Visible)return "save_quest_log_action_invalid";
                if(source is not null&&action.Event!=CampaignQuestLogEvent.None)
                {
                    if(action.TextKey is null||action.Arguments.IsDefault)return "save_quest_log_text_missing";
                    try{source.Expand(action.TextKey,"RED",action.Arguments);}catch(Exception e) when(e is KeyNotFoundException or InvalidDataException){return "save_quest_log_arguments_invalid";}
                }
                if(action.Graphics is {} graphics&&!Identifier(graphics))return "save_quest_log_graphics_invalid";
                if(action.TextKey is {} key&&(!Identifier(key)||source is not null&&!source.Texts.ContainsKey(key)))return "save_quest_log_text_invalid";
                if(!action.Arguments.IsDefault&&(action.Arguments.Length>6||action.Arguments.Any(a=>a is null||a.Length>64||a.Any(char.IsControl))))return "save_quest_log_arguments_invalid";
            }
        }
        return null;
    }
    public static int Budget(ImmutableArray<CampaignQuestLogAction> actions,QuestLogSource? source=null)=>QuestLogSource.SceneEndBytes+actions.Sum(a=>a is null?257:(source?.EventSizes.GetValueOrDefault((int)a.Event,257)??(a.Event==CampaignQuestLogEvent.None?8:16))+(a.Graphics is null?0:8)+(a.Spawn is null?0:8));
    private static bool Identifier(string? text)=>text is {Length:>0 and <=128}&&!text.Any(char.IsControl);
    private static bool ValidActor(CampaignQuestLogActor? actor)=>actor is not null&&Identifier(actor.Id)&&Identifier(actor.Graphics)&&actor.X is >=-32 and <=1024&&actor.Y is >=-32 and <=1024&&Enum.IsDefined(actor.Facing);
    private static ImmutableArray<CampaignQuestLogActor> Actors(CampaignState state,QuestLogSource? source)
    {
        var p=state.World.Player;
        return ImmutableArray.Create(new CampaignQuestLogActor(PlayerId,"player",p.X,p.Y,p.Facing,state.PlayerVisible))
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
    public static CampaignQuestLog? Record(CampaignQuestLog? log,CampaignState before,CampaignState after,CampaignCommand command,ICampaignCatalog? catalog=null,Func<string,Game.Domain.Trainers.TrainerDefinition?>? trainerLookup=null,bool recordingOpen=true,CampaignQuestLogAction? suppliedEvent=null)
    {
        if(command.Kind=="play_time_tick")return log;
        if(log is not null&&after.PresentationEffects.Any(e=>e.Sequence>before.Script.EffectSequence&&e.Opcode=="special"&&e.Args.Contains("QuestLog_CutRecording")))return Close(log);
        var source=(catalog as ICampaignQuestLogCatalog)?.QuestLogSource;
        if(source?.DisabledMaps.Contains(after.World.Player.MapId.Value)==true)return log;
        var caption=suppliedEvent??Describe(before,after,command,catalog,source,trainerLookup);
        if(recordingOpen&&log is not null&&!log.Scenes[^1].Closed&&caption?.Event==CampaignQuestLogEvent.StoryItem)caption=null;
        bool changedMap=before.World.Player.MapId!=after.World.Player.MapId;
        var scenes=log?.Scenes??[];
        if(!recordingOpen||changedMap||scenes.IsEmpty||scenes[^1].Closed||scenes[^1].Map!=after.World.Player.MapId.Value||Budget(scenes[^1].Actions,source)+8>QuestLogSource.ScriptBytes||scenes[^1].Actions.Length>=ActionLimit)
        {
            if(caption is null||!MayStart(caption.Event,after,trainerLookup))return log;
            var scene=new CampaignQuestLogScene(after.World.Player.MapId.Value,Actors(after,source),[caption]){Closed=caption.Event is CampaignQuestLogEvent.FieldMove or CampaignQuestLogEvent.GymWon or CampaignQuestLogEvent.EliteFourWon or CampaignQuestLogEvent.ChampionWon or CampaignQuestLogEvent.TrainerWon};
            return new(scenes.Add(scene).TakeLast(SceneLimit).ToImmutableArray());
        }
        var current=scenes[^1];
        var actions=current.Actions;
        var previous=current.Actors.ToDictionary(a=>a.Id,StringComparer.Ordinal);
        foreach(var action in actions)if(action.Event==CampaignQuestLogEvent.None)
        {var initial=action.Spawn??current.Actors[action.ActorIndex];var actor=previous.GetValueOrDefault(initial.Id)??initial;previous[actor.Id]=actor with{X=action.X,Y=action.Y,Facing=action.Facing,Visible=action.Visible,Graphics=action.Graphics??actor.Graphics};}
        var currentActors=Actors(after,source).ToDictionary(a=>a.Id,StringComparer.Ordinal);
        foreach(var initial in previous.Values.Concat(currentActors.Values.Where(a=>!previous.ContainsKey(a.Id))).ToArray())
        {
            var p=after.World.Player;
            var actor=initial.Id==PlayerId?initial with{X=p.X,Y=p.Y,Facing=p.Facing,Visible=after.PlayerVisible}:
                currentActors.TryGetValue(initial.Id,out var npc)?npc:initial with{Visible=false};
            if(actions.Length>=ActionLimit-(caption is null?0:1)||Budget(actions,source)+8+(caption is null?0:source?.EventSizes[(int)caption.Event]??16)>QuestLogSource.ScriptBytes)break;
            if(!previous.TryGetValue(actor.Id,out var old)||actor!=old)
            {
                int index=Array.FindIndex(current.Actors.ToArray(),a=>a.Id==actor.Id);
                var action=new CampaignQuestLogAction(index,actor.X,actor.Y,actor.Facing,actor.Visible,CampaignQuestLogEvent.None,16){Graphics=index>=0&&actor.Graphics!=old?.Graphics?actor.Graphics:null,Spawn=index<0?actor:null};
                if(Budget(actions.Add(action),source)+(caption is null?0:source?.EventSizes[(int)caption.Event]??16)>QuestLogSource.ScriptBytes)break;
                actions=actions.Add(action);
            }
        }
        if(caption is not null&&actions.Length<ActionLimit&&Budget(actions,source)+(source?.EventSizes[(int)caption.Event]??16)<=QuestLogSource.ScriptBytes)actions=actions.Add(caption);
        return actions==current.Actions?log:new(scenes.SetItem(scenes.Length-1,current with{Actions=actions,Closed=caption?.Event is CampaignQuestLogEvent.FieldMove or CampaignQuestLogEvent.GymWon or CampaignQuestLogEvent.EliteFourWon or CampaignQuestLogEvent.ChampionWon or CampaignQuestLogEvent.TrainerWon}));
    }
    public static CampaignQuestLog? RecordEvents(CampaignQuestLog? log,CampaignState state,ImmutableArray<CampaignQuestLogAction> events,ICampaignCatalog catalog,bool recordingOpen)
    {
        foreach(var entry in events)
        {
            var recorded=Record(log,state,state,new("quest_log_semantic_batch"),catalog,recordingOpen:recordingOpen,suppliedEvent:entry);
            if(!ReferenceEquals(recorded,log))recordingOpen=recorded is not null&&!recorded.Scenes[^1].Closed;
            log=recorded;
        }
        return log;
    }
    public static CampaignQuestLogScene SavedScene(CampaignState state,QuestLogSource source)=>new(state.World.Player.MapId.Value,Actors(state,source),
        [Event(CampaignQuestLogEvent.Arrived,"SavedGameAtLocation",source.MapName(state.World.Player.MapId.Value))]);
    public static CampaignQuestLog? Close(CampaignQuestLog? log)=>log is null?null:new(log.Scenes.SetItem(log.Scenes.Length-1,log.Scenes[^1] with{Closed=true}));
    private static bool MayStart(CampaignQuestLogEvent kind,CampaignState state,Func<string,Game.Domain.Trainers.TrainerDefinition?>? trainers)
    {
        if(kind is CampaignQuestLogEvent.FieldMove or CampaignQuestLogEvent.PokemonCenter)return false;
        if(kind==CampaignQuestLogEvent.TrainerWon&&!state.Script.Flags.GetValueOrDefault("FLAG_SYS_GAME_CLEAR")&&state.ForestTrainerHistory?.LastOrDefault() is {} history&&trainers?.Invoke(history.TrainerId)?.TrainerClass is {} trainerClass&&trainerClass is not ("TRAINER_CLASS_RIVAL_EARLY" or "TRAINER_CLASS_RIVAL_LATE" or "TRAINER_CLASS_CHAMPION" or "TRAINER_CLASS_BOSS"))return false;
        if(!state.Script.Flags.GetValueOrDefault("FLAG_SYS_GAME_CLEAR")&&kind is CampaignQuestLogEvent.PartyOrder or CampaignQuestLogEvent.WildWon)return false;
        if(!state.Script.Flags.GetValueOrDefault("FLAG_SYS_CAN_LINK_WITH_RS")&&kind is CampaignQuestLogEvent.UsedItem or CampaignQuestLogEvent.GaveHeldItem or CampaignQuestLogEvent.GaveHeldItemBag or CampaignQuestLogEvent.GaveHeldItemPc or CampaignQuestLogEvent.TookHeldItem or CampaignQuestLogEvent.SwappedHeldItem or CampaignQuestLogEvent.SwappedHeldItemPc or CampaignQuestLogEvent.SwitchedPartyPc or CampaignQuestLogEvent.WithdrewPokemon or CampaignQuestLogEvent.DepositedPokemon)return false;
        return true;
    }
    internal static CampaignQuestLogAction Event(CampaignQuestLogEvent kind,string text,params string[] args)=>new(-1,0,0,Direction.South,false,kind,127){TextKey=text,Arguments=args.ToImmutableArray()};
    private static CampaignQuestLogAction? Describe(CampaignState before,CampaignState after,CampaignCommand command,ICampaignCatalog? catalog,QuestLogSource? source,Func<string,Game.Domain.Trainers.TrainerDefinition?>? trainerLookup)
    {
        string map=source?.MapNames.GetValueOrDefault(after.World.Player.MapId.Value)??Location(after.World.Player.MapId.Value);
        string Species(Game.Domain.Pokemon.PartyPokemon mon)=>catalog?.SourceSpeciesName(mon.Pokemon.SourceSpeciesId)??mon.Nickname;
        string Item(string item)=>catalog?.Inventory.Items.GetValueOrDefault(new Game.Domain.Ids.ItemId(item.StartsWith("ITEM_",StringComparison.Ordinal)?"pret:"+item:item))?.Name??item;
        foreach(var effect in after.PresentationEffects.Where(e=>e.Sequence>before.Script.EffectSequence))
            if(effect.Opcode=="special"&&effect.Args.Contains("SetUsedPkmnCenterQuestLogEvent"))return Event(CampaignQuestLogEvent.PokemonCenter,"MonsWereFullyRestoredAtCenter");
        if(source is not null&&source.ArrivalFlags.Any(f=>!before.Script.Flags.GetValueOrDefault(f)&&after.Script.Flags.GetValueOrDefault(f)))return Event(CampaignQuestLogEvent.Arrived,"ArrivedInLocation",map);
        if(before.World.Player.MapId!=after.World.Player.MapId&&source?.Departures.FirstOrDefault(d=>d.Inside==before.World.Player.MapId.Value&&d.Outside==after.World.Player.MapId.Value) is {} departure&&departure.Kind!="GAME_CORNER")
            return Event(CampaignQuestLogEvent.Departed,departure.TextKey,source.MapName(before.World.Player.MapId.Value),departure.Place);
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
                "TRAINER_CLASS_CHAMPION"=>Event(CampaignQuestLogEvent.ChampionWon,"PlayerBattledChampionRival",after.PlayerName,after.RivalName),
                _=>Event(CampaignQuestLogEvent.TrainerWon,"TookOnTrainersMonWithMonAndWon",map,definition.SourceTrainerName,loser,victor,flavor)
            };
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
        if(before.TeachyTv is null&&after.TeachyTv is not null)return Event(CampaignQuestLogEvent.UsedItem,"UsedTheKeyItem",Item("ITEM_TEACHY_TV"));
        if(before.FameChecker is null&&after.FameChecker is not null)return Event(CampaignQuestLogEvent.UsedItem,"UsedTheKeyItem",Item("ITEM_FAME_CHECKER"));
        if(before.CoinsBox is null&&after.CoinsBox is not null)return Event(CampaignQuestLogEvent.UsedItem,"UsedTheKeyItem",Item("ITEM_COIN_CASE"));
        if(before.EscapeRope is null&&after.EscapeRope is not null)return Event(CampaignQuestLogEvent.UsedItem,"UsedEscapeRope",Item("ITEM_ESCAPE_ROPE"),source?.MapName(before.World.Player.MapId.Value)??map);
        foreach(var mon in after.Party)
        {
            var old=before.Party.FirstOrDefault(p=>p.InstanceId==mon.InstanceId);
            if(old is null||old.HeldItem==mon.HeldItem)continue;
            bool oldEmpty=old.HeldItem is "" or "ITEM_NONE" or "item.none" or "none";bool newEmpty=mon.HeldItem is "" or "ITEM_NONE" or "item.none" or "none";
            if(oldEmpty)
            {
                if(command.Kind=="pc_give")return Event(CampaignQuestLogEvent.GaveHeldItemPc,"GaveMonHeldItemFromPC",Item(mon.HeldItem),Species(mon));
                bool bag=before.ItemMenu?.Origin=="bag";
                return Event(bag?CampaignQuestLogEvent.GaveHeldItemBag:CampaignQuestLogEvent.GaveHeldItem,bag?"GaveMonHeldItem2":"GaveMonHeldItem",Species(mon),Item(mon.HeldItem));
            }
            if(newEmpty)return Event(CampaignQuestLogEvent.TookHeldItem,"TookHeldItemFromMon",Species(mon),Item(old.HeldItem));
            return command.Kind=="pc_give"?Event(CampaignQuestLogEvent.SwappedHeldItemPc,"SwappedHeldItemFromPC",Item(mon.HeldItem),Species(mon),Item(old.HeldItem)):
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
            if(command.Kind.StartsWith("item_",StringComparison.Ordinal)||command.Kind.StartsWith("bag_",StringComparison.Ordinal)||command.Kind.StartsWith("field_",StringComparison.Ordinal))
                foreach(var stack in bi.BagItems)
                    if(stack.Quantity>ai.BagItems.Where(x=>x.Item==stack.Item).Sum(x=>x.Quantity)&&!command.Kind.Contains("toss",StringComparison.Ordinal))return command.Kind!="field_repel"&&command.PartyIndex>=0&&command.PartyIndex<after.Party.Length?Event(CampaignQuestLogEvent.UsedItem,"UsedItemOnMonAtThisLocation",Item(stack.Item.Value),Species(after.Party[command.PartyIndex])):Event(CampaignQuestLogEvent.UsedItem,"UsedTheItem",Item(stack.Item.Value));
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
    private int _action;
    private readonly string _playerName;
    private readonly QuestLogSource? _source;
    private readonly CampaignQuestLogScene? _savedScene;
    public bool SavedGameVisible{get;private set;}
    private CampaignQuestLogScene CurrentScene=>SavedGameVisible?_savedScene!:_log.Scenes[SceneIndex];
    public CampaignQuestLogActor? MovingFrom{get;private set;}
    public double MovementFraction=>Math.Clamp(_frames/Math.Max(1,_duration),0,1);
    private double _frames;
    private int _duration;
    private ImmutableSortedDictionary<string,CampaignQuestLogActor> _actors=ImmutableSortedDictionary<string,CampaignQuestLogActor>.Empty;
    public int SceneIndex{get;private set;}
    public int SceneCount=>_log.Scenes.Length;
    public bool Finished{get;private set;}
    public string Map=>CurrentScene.Map;
    public string? Caption{get;private set;}
    public IEnumerable<CampaignQuestLogActor> Actors=>_actors.Values;
    public CampaignQuestLogPlayback(CampaignQuestLog log,string playerName="RED",QuestLogSource? source=null,CampaignQuestLogScene? savedScene=null)
    {
        if(CampaignQuestLogRecorder.Validate(log,source) is {} error)throw new InvalidDataException(error);
        _log=log;_playerName=playerName;_source=source;_savedScene=savedScene;BeginScene();
    }
    private void BeginScene()
    {
        _actors=CurrentScene.Actors.ToImmutableSortedDictionary(a=>a.Id,a=>a,StringComparer.Ordinal);
        _action=0;_frames=0;Caption=null;ApplyAction();
    }
    private void ApplyAction()
    {
        var action=CurrentScene.Actions[_action];
        MovingFrom=null;_duration=action.Frames;
        if(action.Event==CampaignQuestLogEvent.None)
        {
            var id=action.Spawn?.Id??CurrentScene.Actors[action.ActorIndex].Id;
            if(!_actors.TryGetValue(id,out var from))from=action.Spawn!;
            MovingFrom=from;
            _actors=_actors.SetItem(id,MovingFrom with{X=action.X,Y=action.Y,Facing=action.Facing,Visible=action.Visible,Graphics=action.Graphics??MovingFrom.Graphics});
        }
        else if(_source is not null&&action.TextKey is {} key)Caption=_source.Expand(key,_playerName,action.Arguments.IsDefault?[]:action.Arguments);
        else Caption=action.Event switch
        {
            CampaignQuestLogEvent.Arrived=>$"{_playerName} arrived at {CampaignQuestLogRecorder.Location(Map)}.",
            CampaignQuestLogEvent.PokemonCenter=>$"{_playerName} restored the POKEMON at a POKEMON CENTER.",
            CampaignQuestLogEvent.TrainerWon=>$"{_playerName} defeated a TRAINER in battle.",
            CampaignQuestLogEvent.WildWon=>$"{_playerName} defeated a wild POKEMON.",
            _=>Caption
        };
        if(_source is not null&&action.Event!=CampaignQuestLogEvent.None&&!SavedGameVisible)
        {
            int count=(Caption??"").Count(c=>c!='\n');
            _duration=count<20?95:count<36?127:count<46?191:255;
        }
    }
    public void Tick(double seconds)
    {
        if(!double.IsFinite(seconds)||seconds<0)throw new ArgumentOutOfRangeException(nameof(seconds));
        if(Finished)return;
        _frames+=seconds*60;
        while(!Finished&&_frames>=_duration)
        {
            _frames-=_duration;
            if(++_action>=CurrentScene.Actions.Length){NextScene();break;}
            ApplyAction();
        }
    }
    public void NextScene()
    {
        if(Finished)return;
        if(SavedGameVisible){Finished=true;return;}
        if(SceneIndex+1==SceneCount){Skip();return;}
        SceneIndex++;BeginScene();
    }
    public void Skip()
    {
        if(Finished)return;
        if(_savedScene is not null&&!SavedGameVisible){SavedGameVisible=true;BeginScene();}else Finished=true;
    }
}

public sealed partial class CampaignSession
{
    private bool _questLogRecordingOpen;
    private CampaignQuestLogShopCapture? _questLogShop;
    public void StopQuestLogRecording()=>_questLogRecordingOpen=false;
    private Decision<CampaignState> DecideWithQuestLogRecording(CampaignState state,CampaignCommand command)
    {
        var result=DecideWithWildHistoryRetention(state,command);
        if(result.Status!=CommandStatus.Accepted)return result;
        var log=CampaignQuestLogRecorder.Record(state.QuestLog,state,result.State,command,_content,
            id=>OrdinaryTrainerBindings.TryGetValue(new Game.Domain.Ids.TrainerId(id),out var binding)?binding.Definition:null,_questLogRecordingOpen);
        var shop=CampaignQuestLogShopCapture.Observe(_questLogShop,state,result.State,_content);
        bool recordingOpen=ReferenceEquals(log,state.QuestLog)?_questLogRecordingOpen:log is not null&&!log.Scenes[^1].Closed;
        log=CampaignQuestLogRecorder.RecordEvents(log,result.State,shop.Events,_content,recordingOpen);
        _questLogShop=shop.Capture;
        if(!ReferenceEquals(log,state.QuestLog))_questLogRecordingOpen=log is not null&&!log.Scenes[^1].Closed;
        return ReferenceEquals(log,result.State.QuestLog)?result:result with{State=result.State with{QuestLog=log}};
    }
}
