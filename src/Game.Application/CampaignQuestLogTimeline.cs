using System.Collections.Immutable;
using Game.Domain.World;

namespace Game.Application;

// Pure presentation timeline. Captions never block independent actor tracks.
internal sealed class CampaignQuestLogTimeline
{
    private ImmutableArray<CampaignQuestLogActor> _segmentActors;
    public string Map{get;private set;}
    private readonly ImmutableArray<CampaignQuestLogAction> _moves;
    private readonly ImmutableArray<(int Start,int VisibleEnd,string Text)> _captions;
    private readonly Dictionary<string,CampaignQuestLogActor> _actors;
    private readonly Dictionary<string,(CampaignQuestLogActor From,int Start,int Duration,string? Action)> _moving=new(StringComparer.Ordinal);
    private readonly int _end;
    private int _move;
    private double _frame;
    private CampaignQuestLogAction? _transition;
    private bool _transitionSwitched;
    public CampaignQuestLogTransitionTiming? ActiveTransitionTiming=>_transition?.TransitionTiming is {} timing&&_frame<_transition.StartFrame+timing.EndFrame?timing:null;
    public double TransitionElapsedFrames=>_transition is {} transition?Math.Max(0,_frame-transition.StartFrame):0;
    public IEnumerable<CampaignQuestLogActor> Actors=>_actors.Values;
    public int ArtRevision{get;private set;}
    public double Frame=>_frame;
    public string? Layout{get;private set;}
    public ImmutableArray<FieldMetatileEdit> Tiles{get;private set;}
    public string? Caption=>_captions.Where(c=>c.Start<=_frame&&c.VisibleEnd>_frame).Select(c=>c.Text).FirstOrDefault();
    public bool Finished=>_frame>=_end;
    public (CampaignQuestLogActor From,double Fraction,int DrawYOffset,bool IsJump)? Movement(string id)
    {
        if(!_moving.TryGetValue(id,out var m)||_frame>=m.Start+m.Duration)return null;
        var pose=CampaignFieldMovementTiming.Sample(m.Action,m.Duration,_frame-m.Start);
        return(m.From,pose.Fraction,pose.DrawYOffset,pose.IsJump);
    }
    public CampaignQuestLogTimeline(CampaignQuestLogScene scene,string player,QuestLogSource? source,bool savedScene=false)
    {
        Layout=scene.Layout;Tiles=scene.Tiles.IsDefault?[]:scene.Tiles;
        Map=scene.Map;_segmentActors=scene.Actors;_actors=scene.Actors.ToDictionary(a=>a.Id,StringComparer.Ordinal);
        _moves=scene.Actions.Where(a=>a.Event==CampaignQuestLogEvent.None).OrderBy(a=>a.StartFrame).ToImmutableArray();
        // Ordinary warp tasks lock field controls until their recorded release.
        // The source skips the Quest callback during that lock, but actor/map tasks
        // continue. Only authenticated observed intervals provide this evidence;
        // seamless edges and older unknown map markers do not imply a pause.
        var locks=_moves.Where(a=>a.TransitionTiming?.Cause is FieldTransitionCause.PlainWarp or FieldTransitionCause.AnimatedDoor)
            .Select(a=>(Start:a.StartFrame,Switch:a.StartFrame+a.TransitionTiming!.SwitchFrame,End:a.StartFrame+a.TransitionTiming.EndFrame)).ToArray();
        var captions=ImmutableArray.CreateBuilder<(int Start,int VisibleEnd,string Text)>();int captionEnd=0;
        foreach(var action in scene.Actions.Where(a=>CampaignQuestLogRecorder.IsCaption(a)).OrderBy(a=>a.StartFrame))
        {
            string text=source is not null&&action.TextKey is {} key?source.Expand(key,player,action.Arguments):action.Event switch{CampaignQuestLogEvent.Arrived=>$"{player} arrived at {CampaignQuestLogRecorder.Location(scene.Map)}.",CampaignQuestLogEvent.PokemonCenter=>$"{player} restored the POKEMON at a POKEMON CENTER.",CampaignQuestLogEvent.TrainerWon=>$"{player} defeated a TRAINER in battle.",CampaignQuestLogEvent.WildWon=>$"{player} defeated a wild POKEMON.",_=>player+" continued the adventure."};
            Add(text,action);
            if(source is not null&&action.Event==CampaignQuestLogEvent.ChampionWon&&action.Arguments.Length==5)
            {
                Add(source.Expand("PlayerSentOutMon1RivalSentOutMon2",player,[action.Arguments[1],action.Arguments[2],action.Arguments[0],action.Arguments[3]]),action);
                Add(source.Expand("WonTheMatchAsAResult",player,[action.Arguments[4]]),action);
            }
        }
        _captions=captions.ToImmutable();_end=Math.Max(captionEnd,scene.Actions.Where(a=>!CampaignQuestLogRecorder.IsCaption(a)).Select(a=>a.StartFrame+a.Frames).DefaultIfEmpty(0).Max());
        Tick(0);
        void Add(string text,CampaignQuestLogAction action)
        {
            int count=text.Count(c=>c!='\n');int duration=source is null||savedScene?action.Frames:count<20?95:count<36?127:count<46?191:255;
            int start=Math.Max(captionEnd,action.StartFrame);
            foreach(var held in locks)
                if(start>=held.Start&&start<held.End)start=held.End;
            int end=start+duration;
            foreach(var held in locks)
            {
                if(held.Start>=end)break;
                if(held.End>start)end+=held.End-Math.Max(start,held.Start);
            }
            // Map reconstruction clears WIN_DESCRIPTION without restarting the
            // active event timer. The next queued caption still waits for its end.
            int visibleEnd=end;
            foreach(var held in locks)
                if(held.Switch>=start){visibleEnd=Math.Min(visibleEnd,held.Switch);break;}
            captionEnd=end;captions.Add((start,visibleEnd,text));
        }
    }
    public void Tick(double seconds)
    {
        _frame+=seconds*60;
        SwitchPendingTransition();
        while(_move<_moves.Length&&_moves[_move].StartFrame<=_frame)
        {
            var action=_moves[_move++];
            if(action.Transition is not null)
            {
                _transition=action;_transitionSwitched=false;
                SwitchPendingTransition();continue;
            }
            if(action.Layout is {} layout){ArtRevision++;Layout=layout.Length==0?null:layout;continue;}
            if(action.Tile is {} tile){ArtRevision++;Tiles=Tiles.Where(t=>t.X!=tile.X||t.Y!=tile.Y).ToImmutableArray();if(tile.SourceMetatileId>=0)Tiles=Tiles.Add(tile);continue;}
            if(action.Graphics is not null||action.Spawn is not null)ArtRevision++;
            var initial=action.Spawn??_segmentActors[action.ActorIndex];
            var prior=_actors.GetValueOrDefault(initial.Id)??initial;
            _moving[initial.Id]=(prior,action.StartFrame,action.Frames,action.SourceAction);
            _actors[initial.Id]=prior with{X=action.X,Y=action.Y,Facing=action.Facing,Visible=action.Visible,Graphics=action.Graphics??prior.Graphics};
        }
    }
    private void SwitchPendingTransition()
    {
        if(_transition is not {Transition:{} transition} action||_transitionSwitched||
            _frame<action.StartFrame+(action.TransitionTiming?.SwitchFrame??0))return;
        _transitionSwitched=true;
        Map=transition.Map;Layout=transition.Layout;Tiles=transition.Tiles.IsDefault?[]:transition.Tiles;
        _segmentActors=transition.Actors;_actors.Clear();foreach(var actor in transition.Actors)_actors.Add(actor.Id,actor);
        _moving.Clear();ArtRevision++;
    }
}
