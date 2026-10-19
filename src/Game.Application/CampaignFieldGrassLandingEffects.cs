using System.Collections.Immutable;

namespace Game.Application;

public sealed record CampaignFieldGrassLandingCover(string ActorId,int X,int Y,int Age);
public readonly record struct CampaignFieldGrassActorGeometry(int CenterX,int CenterY,int Height,int Subpriority,bool Active=true);

/// <summary>Transient actor-linked covers spawned by ordinary grass jump landings.
/// Walking rustle/spawn is a separate effect. Callers own the field/map lifecycle.</summary>
public sealed class CampaignFieldGrassLandingEffects
{
    private readonly record struct Position(int PreviousX,int PreviousY,int X,int Y);
    private sealed record Change(double Frame,long Order,string ActorId,Position Position,bool Landing);
    private sealed record Cover(string ActorId,int X,int Y,double Spawn,double? Removal=null);
    private readonly Dictionary<string,Position> _positions=new(StringComparer.Ordinal);
    private readonly List<Change> _pending=[];
    private readonly List<Cover> _covers=[];
    private double _frame;
    private long _order;
    private const double Epsilon=1e-7;

    public ImmutableArray<CampaignFieldGrassLandingCover> Covers=>_covers.Select(c=>
        new CampaignFieldGrassLandingCover(c.ActorId,c.X,c.Y,(int)Math.Floor(_frame-c.Spawn+Epsilon))).ToImmutableArray();

    public void Clear()
    {
        _positions.Clear();_pending.Clear();_covers.Clear();_frame=0;_order=0;
    }

    public void QueueMovement(string actorId,int fromX,int fromY,int toX,int toY,string? action,int duration,double startFrame)
    {
        ArgumentException.ThrowIfNullOrEmpty(actorId);
        if(duration<1)throw new ArgumentOutOfRangeException(nameof(duration));
        if(!double.IsFinite(startFrame)||startFrame<0||startFrame+Epsilon<_frame)
            throw new ArgumentOutOfRangeException(nameof(startFrame));
        _positions.TryAdd(actorId,new(fromX,fromY,fromX,fromY));
        bool ordinary=CampaignFieldJumpEffects.IsOrdinaryJump(action);
        bool far=ordinary&&action!.StartsWith("jump_2_",StringComparison.Ordinal);
        int firstX=far?(fromX+toX)/2:toX,firstY=far?(fromY+toY)/2:toY;
        Add(startFrame+1,new(fromX,fromY,firstX,firstY),false);
        if(far)Add(startFrame+16,new(firstX,firstY,toX,toY),false);
        Add(startFrame+duration,new(toX,toY,toX,toY),ordinary);
        _pending.Sort((a,b)=>a.Frame!=b.Frame?a.Frame.CompareTo(b.Frame):a.Order.CompareTo(b.Order));
        void Add(double frame,Position position,bool landing)=>_pending.Add(new(frame,_order++,actorId,position,landing));
    }

    public void AdvanceTo(double frame,Func<int,int,int?> behaviorAt,Func<string,bool> actorExists)
    {
        ArgumentNullException.ThrowIfNull(behaviorAt);ArgumentNullException.ThrowIfNull(actorExists);
        if(!double.IsFinite(frame)||frame<0||frame+Epsilon<_frame)throw new ArgumentOutOfRangeException(nameof(frame));
        // Removed actors cannot produce a delayed landing from an already queued clip.
        var missing=_positions.Keys.Where(id=>!actorExists(id)).ToHashSet(StringComparer.Ordinal);
        foreach(string id in missing)_positions.Remove(id);
        _pending.RemoveAll(e=>missing.Contains(e.ActorId));
        _covers.RemoveAll(c=>missing.Contains(c.ActorId));
        while(true)
        {
            double next=_pending.Count>0?_pending[0].Frame:double.PositiveInfinity;
            foreach(var cover in _covers)
                if(cover.Removal is {} removal)next=Math.Min(next,removal);
            if(next>frame+Epsilon)break;
            Process(next);
        }
        Process(frame);
        _frame=frame;
        // Do not retain a movement history or actors which no longer own work.
        foreach(string id in _positions.Keys.ToArray())
            if(!_covers.Any(c=>c.ActorId==id)&&!_pending.Any(e=>e.ActorId==id))_positions.Remove(id);

        void Process(double now)
        {
            int count=0;
            while(count<_pending.Count&&_pending[count].Frame<=now+Epsilon)count++;
            // Actors update before the high-index field-effect callbacks. Landing
            // spawn/dedup also precedes an existing cover's removal callback.
            for(int i=0;i<count;i++)_positions[_pending[i].ActorId]=_pending[i].Position;
            for(int i=0;i<count;i++)
            {
                var e=_pending[i];var p=e.Position;
                if(e.Landing&&TallGrass(behaviorAt(p.X,p.Y))&&actorExists(e.ActorId)&&
                    !_covers.Any(c=>c.ActorId==e.ActorId&&c.X==p.X&&c.Y==p.Y))
                    _covers.Add(new(e.ActorId,p.X,p.Y,e.Frame));
            }
            if(count>0)_pending.RemoveRange(0,count);
            for(int i=_covers.Count-1;i>=0;i--)
            {
                var c=_covers[i];
                if(!actorExists(c.ActorId)||!TallGrass(behaviorAt(c.X,c.Y))||
                    c.Removal is {} removal&&removal<=now+Epsilon)
                { _covers.RemoveAt(i);continue; }
                if(c.Removal is null&&_positions.TryGetValue(c.ActorId,out var p)&&
                    (p.X!=c.X||p.Y!=c.Y)&&(p.PreviousX!=c.X||p.PreviousY!=c.Y))
                    // SeekSpriteAnim(4) selects stationary image0 immediately.
                    // END is observed at age10; callback age11 may remove it.
                    // Departure is sticky and only the next callback removes it.
                    _covers[i]=c with{Removal=Math.Max(c.Spawn+11,now+1)};
            }
        }
    }

    public static bool TallGrass(int? behavior)=>behavior is 0x02 or 0xd1;

    public static int ObjectPriority(int elevation)=>elevation switch
    { 4 or 6 or 8 or 10 or 12=>1,13 or 14=>0,_=>2 };

    /// <summary>Source SetObjectSubpriorityByElevation; sprite center excludes jump Y2.</summary>
    public static int Subpriority(int elevation,int centerY,int height,int cameraOffsetY,int offset)
    {
        ReadOnlySpan<int> values=[115,115,83,115,83,115,83,115,83,115,83,115,83,0,0,115];
        int y=(centerY+height/2+cameraOffsetY+8)&255;
        return values[elevation]+((16-(y>>4))<<1)+offset;
    }

    /// <summary>Source UpdateGrassFieldEffectSubpriority for stationary image0.
    /// Actor order must be the source object-event order; smaller values are in front.</summary>
    public static int CoverSubpriority(int tileX,int tileY,int elevation,int cameraOffsetY,
        IEnumerable<CampaignFieldGrassActorGeometry> actors)
    {
        int x=tileX*16+8,y=tileY*16+8;
        return AdjustCoverSubpriority(x,y,Subpriority(elevation,y,16,cameraOffsetY,0),actors);
    }

    public static int AdjustCoverSubpriority(int centerX,int centerY,int priority,
        IEnumerable<CampaignFieldGrassActorGeometry> actors)
    {
        foreach(var actor in actors)
        {
            if(!actor.Active||centerX-8>=actor.CenterX||centerX+8<=actor.CenterX)continue;
            int actorTop=actor.CenterY-actor.Height/2,bottom=centerY+8,overlapTop=bottom-actor.Height/2;
            if((actorTop<overlapTop||actorTop<bottom)&&actor.CenterY>overlapTop&&priority<=actor.Subpriority)
                return actor.Subpriority+2;
        }
        return priority;
    }
}
