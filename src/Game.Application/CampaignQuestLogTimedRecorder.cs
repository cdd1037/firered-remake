using System.Collections.Immutable;
using Game.Domain.World;

namespace Game.Application;

public static partial class CampaignQuestLogRecorder
{
    private static CampaignQuestLogScene AppendTimed(CampaignQuestLogScene scene,CampaignState before,CampaignState after,CampaignCommand command,ICampaignCatalog? catalog,QuestLogSource? source,CampaignQuestLogAction? caption,out bool captionRecorded,int? recordingFrame=null)
    {
        bool overflow=false;
        var actions=scene.Actions;
        int start=actions.Where(a=>a.Event==CampaignQuestLogEvent.None&&a.Tile is null&&a.Layout is null).Select(a=>a.StartFrame+a.Frames).DefaultIfEmpty(0).Max();
        start=Math.Max(start,recordingFrame??0);
        var segment=CurrentProjection(scene);
        int transitionIndex=Array.FindLastIndex(actions.ToArray(),a=>a.Transition is not null);
        var previous=segment.Actors.ToDictionary(a=>a.Id,StringComparer.Ordinal);
        foreach(var a in actions.Skip(transitionIndex+1).Where(a=>a.Event==CampaignQuestLogEvent.None&&a.Tile is null&&a.Layout is null).OrderBy(a=>a.StartFrame))
        {
            var initial=a.Spawn??segment.Actors[a.ActorIndex];var old=previous.GetValueOrDefault(initial.Id)??initial;
            previous[initial.Id]=old with{X=a.X,Y=a.Y,Facing=a.Facing,Visible=a.Visible,Graphics=a.Graphics??old.Graphics};
        }
        var latest=Actors(after,source).ToDictionary(a=>a.Id,StringComparer.Ordinal);
        var scripted=new HashSet<string>(StringComparer.Ordinal);
        foreach(var motion in after.Motions.Where(m=>!before.Motions.Contains(m)))
        {
            if(motion.Path.IsDefaultOrEmpty||!latest.TryGetValue(motion.ActorId,out var final))continue;
            scripted.Add(motion.ActorId);int path=0,time=start;
            var position=motion.Path[0];var actor=previous.GetValueOrDefault(motion.ActorId)??final with{X=position.X,Y=position.Y,Facing=position.Facing};
            if(!previous.ContainsKey(motion.ActorId))previous[motion.ActorId]=actor;
            foreach(string opcode in motion.Actions)
            {
                if(opcode=="step_end")break;
                bool noPath=opcode is "lock_facing_direction" or "unlock_facing_direction" or "disable_anim" or "restore_anim" or "set_invisible" or "set_visible" or "face_player"||opcode.StartsWith("delay_",StringComparison.Ordinal)||opcode.StartsWith("emote_",StringComparison.Ordinal);
                if(!noPath&&path+1<motion.Path.Length)position=motion.Path[++path];
                actor=actor with{X=position.X,Y=position.Y,Facing=opcode=="face_player"?final.Facing:position.Facing,Visible=opcode=="set_visible"||opcode!="set_invisible"&&actor.Visible,Graphics=final.Graphics};
                int frames=MovementFrames(opcode);
                if(!Append(actor,time,frames,opcode))break;
                time+=frames;
            }
        }
        foreach(var initial in previous.Values.Concat(latest.Values.Where(a=>!previous.ContainsKey(a.Id))).ToArray())
        {
            if(scripted.Contains(initial.Id))continue;
            var actor=latest.GetValueOrDefault(initial.Id)??initial with{Visible=false};
            if(!previous.TryGetValue(initial.Id,out var prior)||actor!=prior)
            {
                bool moved=prior is not null&&(actor.X!=prior.X||actor.Y!=prior.Y);
                var timing=actor.Id==PlayerId&&moved&&command.Kind is "move" or "cycle_input"?CampaignFieldMovementTiming.Resolve(before,command,catalog,after):null;
                Append(actor,start,timing?.Frames??(prior is not null&&!moved?1:16),timing?.SourceAction);
            }
        }
        captionRecorded=caption is null||!overflow&&TryAppendEvent(actions,caption with{StartFrame=start},source,out actions);
        return actions==scene.Actions&&!overflow?scene:scene with{Actions=actions,Closed=overflow||caption?.Event is CampaignQuestLogEvent.FieldMove or CampaignQuestLogEvent.GymWon or CampaignQuestLogEvent.EliteFourWon or CampaignQuestLogEvent.ChampionWon or CampaignQuestLogEvent.TrainerWon};

        bool Append(CampaignQuestLogActor actor,int at,int frames,string? opcode)
        {
            if(overflow)return false;
            if(at>ushort.MaxValue-frames){overflow=true;return false;}
            int index=Array.FindIndex(segment.Actors.ToArray(),a=>a.Id==actor.Id);
            previous.TryGetValue(actor.Id,out var prior);
            var action=new CampaignQuestLogAction(index,actor.X,actor.Y,actor.Facing,actor.Visible,CampaignQuestLogEvent.None,frames)
            {StartFrame=at,SourceAction=opcode,Graphics=actor.Graphics!=prior?.Graphics?actor.Graphics:null,Spawn=index<0?prior??actor:null};
            int reserved=caption is null?0:source?.EventSizes[(int)caption.Event]??16;
            if(actions.Length>=ActionLimit-(caption is null?0:1)||LogicalCommandCount(actions.Add(action))+(caption is null?0:1)>QuestLogSource.EventCursorCount||Budget(actions.Add(action),source)+reserved>QuestLogSource.ScriptBytes){overflow=true;return false;}
            actions=actions.Add(action);previous[actor.Id]=actor;return true;
        }
    }
    private static int MovementFrames(string action)
    {
        if(CampaignFieldMovementTiming.SourceDuration(action) is {} sourceFrames)return sourceFrames;
        if(action.StartsWith("delay_",StringComparison.Ordinal)&&int.TryParse(action[6..],out int delay))return Math.Clamp(delay,1,255);
        if(action.StartsWith("emote_",StringComparison.Ordinal)||action.StartsWith("face_",StringComparison.Ordinal))return 1;
        if(action is "lock_facing_direction" or "unlock_facing_direction" or "disable_anim" or "restore_anim" or "set_invisible" or "set_visible" or "face_player" or "face_original_direction")return 1;
        if(action=="nurse_joy_bow")return 48;
        if(action.StartsWith("jump_2_",StringComparison.Ordinal)||action.StartsWith("jump_special_",StringComparison.Ordinal))return FieldSurfSourcePresentation.JumpCueFrames;
        if(action=="cut_tree")return FieldCutSourcePresentation.TreeCutCueFrames;
        if(action=="rock_smash_break")return 64;
        if(action.Contains("fastest",StringComparison.Ordinal))return 2;
        if(action.Contains("faster",StringComparison.Ordinal))return 4;
        if(action.Contains("fast",StringComparison.Ordinal)||action.StartsWith("player_run_",StringComparison.Ordinal))return 8;
        // event_object_movement.c: slower takes a pixel on ticks0,2,...30; slow
        // takes two pixels per three ticks; slowest one pixel per ten ticks.
        if(action.StartsWith("walk_slowest_",StringComparison.Ordinal))return 160;
        if(action.StartsWith("walk_slower_",StringComparison.Ordinal))return 31;
        if(action.StartsWith("walk_slow_",StringComparison.Ordinal))return 23;
        return 16;
    }
}
