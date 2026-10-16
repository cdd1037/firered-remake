namespace Game.Presentation.BattleAnimations;

// BeginAnim/ContinueAnim's shared, non-affine frame player. Importers retain
// finite LOOP regions as boundaries; repeated images are never duplicated.
public sealed class SourceSpriteFrameProgram
{
    private readonly SourceSpriteAnimFrame[] _frames;
    private readonly SourceSpriteFrameLoop[] _loops;
    private readonly int _jump;
    private int _index;
    public SourceSpriteAnimFrame Frame=>_frames[_index];
    public bool Ended{get;private set;}
    public int DelayCounter{get;private set;}
    public int LoopCounter{get;private set;}

    public SourceSpriteFrameProgram(SourceSpriteRecipe recipe,int animation=0)
    {
        if(animation<0||animation>=recipe.Animations.Length||recipe.FrameLoops.Length>recipe.Animations.Length||recipe.LoopStarts.Length>recipe.Animations.Length)
            throw new InvalidDataException("Sprite frame animation index/metadata differs.");
        _frames=recipe.Animations[animation];
        _loops=animation<recipe.FrameLoops.Length?recipe.FrameLoops[animation]:[];
        _jump=animation<recipe.LoopStarts.Length?recipe.LoopStarts[animation]:-1;
        if(_frames.Length==0||_frames.Length>255||_jump< -1||_jump>=_frames.Length||_frames.Any(f=>f.Tile is <0 or >32767||f.Duration is <0 or >63))
            throw new InvalidDataException("Sprite frame command differs from source widths.");
        int previous=0;
        foreach(var loop in _loops)
        {
            if(loop.At<=previous||loop.At>_frames.Length||loop.Start<previous||loop.Start>=loop.At||loop.Count is <1 or >63)
                throw new InvalidDataException("Sprite finite loop must enclose a bounded frame region.");
            previous=loop.At;
        }
        // CreateSpriteAndAnimate performs BeginAnim immediately. A zero duration
        // remains zero; it never underflows the source six-bit delay counter.
        DelayCounter=Math.Max(0,Frame.Duration-1);
    }
    public void Step()
    {
        if(Ended)return;
        if(DelayCounter>0){DelayCounter--;return;}
        int next=_index+1;
        foreach(var loop in _loops)
        {
            if(loop.At!=next)continue;
            // BeginAnimLoop sets count; later encounters decrement it. Both
            // recurse into ContinueAnim in this same tick, unlike task-affine.
            if(LoopCounter==0)LoopCounter=loop.Count;else LoopCounter--;
            if(LoopCounter!=0)next=loop.Start;
            break;
        }
        if(next==_frames.Length)
        {
            if(_jump<0){Ended=true;return;}
            next=_jump;
        }
        _index=next;DelayCounter=Math.Max(0,Frame.Duration-1);
    }
}
