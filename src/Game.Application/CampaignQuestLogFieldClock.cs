namespace Game.Application;

// Counts actually observed controllable-field loop steps, not wall time or gaps in
// host stamps. It never belongs to CampaignState or a save and never executes a command.
public sealed class CampaignQuestLogFieldClock
{
    // Leave room for one maximum source-duration action inside the u16 timeline.
    public const int FrameLimit=ushort.MaxValue-byte.MaxValue;
    private ulong? _lastStamp;
    public int Frame{get;private set;}
    public bool Full=>Frame>=FrameLimit;
    public void Observe(ulong stamp,bool enabled)
    {
        if(_lastStamp==stamp)return;
        _lastStamp=stamp;
        if(enabled&&!Full)Frame++;
    }
    public void AdvanceTo(int frame)=>Frame=Math.Max(Frame,Math.Clamp(frame,0,FrameLimit));
    public void Reset()=>Frame=0;
}

public sealed partial class CampaignSession
{
    private readonly CampaignQuestLogFieldClock _questLogFieldClock=new();
    public void ObserveQuestLogFieldFrame(ulong frame)
    {
        _questLogFieldClock.Observe(frame,_questLogRecordingOpen&&QuestLogFieldControlsAvailable(State));
        if(_questLogFieldClock.Full)_questLogRecordingOpen=false;
    }
    private static bool QuestLogFieldControlsAvailable(CampaignState state)=>state.Script.Completed&&!state.Script.Locked&&state.Script.Wait is null&&state.Dialogue is null;
    private void CompleteQuestLogScriptDelay(CampaignState before,CampaignState after,CampaignQuestLog? log)
    {
        if(_questLogRecordingOpen&&log is not null&&!QuestLogFieldControlsAvailable(before)&&QuestLogFieldControlsAvailable(after))
            _questLogFieldClock.AdvanceTo(log.Scenes[^1].Actions.Where(a=>a.Event==CampaignQuestLogEvent.None).Select(a=>a.StartFrame+a.Frames).DefaultIfEmpty(0).Max());
    }
    private void SynchronizeQuestLogFieldClock(CampaignQuestLog? before,CampaignQuestLog? after)
    {
        if(after is null||before is null||after.Scenes[^1].Actors!=before.Scenes[^1].Actors)_questLogFieldClock.Reset();
        else _questLogFieldClock.AdvanceTo(after.Scenes[^1].Actions.Where(a=>a.Event==CampaignQuestLogEvent.None).Select(a=>a.StartFrame).DefaultIfEmpty(0).Max());
    }
}
