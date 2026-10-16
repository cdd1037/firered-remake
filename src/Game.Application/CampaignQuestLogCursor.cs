using System.Collections.Immutable;
using Game.Domain.World;

namespace Game.Application;

public static partial class CampaignQuestLogRecorder
{
    // ResetActions seeds facing + zero-input; the pose already lives in Actors.
    public const int InitialActionBytes=16;
    // ReadQuestLogScriptFromSav1 has 32 command pointers: initial facing/input
    // and the scene-end consume three; a repeated payload shares its group pointer.
    public static int LogicalCommandCount(ImmutableArray<CampaignQuestLogAction> actions)=>3+actions.Sum(a=>a is null?33:(a.Repeat?0:1)+(a.Graphics is null?0:1)+(a.Spawn is null?0:1));
    public static bool FitsActions(ImmutableArray<CampaignQuestLogAction> actions,QuestLogSource? source=null)=>actions.Length<=ActionLimit&&LogicalCommandCount(actions)<=QuestLogSource.EventCursorCount&&Budget(actions,source)<=QuestLogSource.ScriptBytes;
    public static bool IsCaption(CampaignQuestLogAction action)=>action.Event>CampaignQuestLogEvent.None;
    private static bool Repeatable(CampaignQuestLogEvent kind)=>kind>CampaignQuestLogEvent.None&&kind is not (CampaignQuestLogEvent.WildWon or CampaignQuestLogEvent.ChampionWon);

    // Consecutive event payloads have the same source action cursor. Input/movement
    // records separate groups; strings and elapsed host time never determine identity.
    public static bool TryAppendEvent(ImmutableArray<CampaignQuestLogAction> actions,CampaignQuestLogAction caption,QuestLogSource? source,out ImmutableArray<CampaignQuestLogAction> result)
    {
        if(!IsCaption(caption))throw new ArgumentException("quest_log_event_required",nameof(caption));
        result=actions;
        bool repeats=Repeatable(caption.Event)&&!actions.IsEmpty&&actions[^1].Event==caption.Event;
        if(repeats&&caption.Event==CampaignQuestLogEvent.PokemonCenter)return true;
        int first=actions.Length;
        if(repeats){first--;while(first>0&&actions[first].Repeat)first--;}
        int cost=(source?.EventSizes.GetValueOrDefault((int)caption.Event,257)??16)-(repeats?4:0);
        // Original checks room before shifting even when the sixth payload replaces one.
        if(Budget(actions,source)+cost>QuestLogSource.ScriptBytes)return false;
        if(repeats&&actions.Length-first==5)
        {
            result=actions.RemoveAt(first).SetItem(first,actions[first+1] with{Repeat=false});
        }
        var candidate=result.Add(caption with{Repeat=repeats,StartFrame=repeats?actions[first].StartFrame:caption.StartFrame});
        if(!FitsActions(candidate,source)){result=actions;return false;}
        result=candidate;
        return true;
    }

    public static CampaignQuestLog? RecordFieldInput(CampaignQuestLog? log,int frame,QuestLogSource? source,bool recordingOpen)
    {
        if(frame is <0 or >CampaignQuestLogFieldClock.FrameLimit)throw new ArgumentOutOfRangeException(nameof(frame));
        if(!recordingOpen||log is null||log.Scenes[^1].Closed)return log;
        var scene=log.Scenes[^1];
        var input=new CampaignQuestLogAction(-1,0,0,Direction.South,false,CampaignQuestLogEvent.Input,1){StartFrame=frame};
        if(!FitsActions(scene.Actions.Add(input),source))return Close(log);
        return new(log.Scenes.SetItem(log.Scenes.Length-1,scene with{Actions=scene.Actions.Add(input)}));
    }
}
