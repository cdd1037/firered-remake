using System.Collections.ObjectModel;
using System.Text.Json;
using System.Security.Cryptography;
namespace Game.Presentation.BattleAnimations;
public readonly record struct AnimationSlot
{
    public int Value { get; }
    public AnimationSlot(int value) => Value = value is >= 0 and < 4 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    public bool IsPlayer => (Value & 1) == 0;
    public AnimationSlot Partner => new(Value ^ 2);
}
public readonly record struct PixelPoint(int X, int Y);
public sealed record AnimationBattler(AnimationSlot Slot, string Asset, int YOffset, int Elevation, int SourceWidth, int SourceHeight)
{
    public PixelPoint? DisplayCenter {get;init;}
    public bool Visible {get;init;} = true;
    public int Subpriority => Slot.Value switch {0=>30,1=>40,2=>20,_=>50};
    public int BackgroundRank => Slot.Value is 0 or 3 ? 2 : 1;
    public PixelPoint Base(bool doubles) => (doubles ? new PixelPoint[]{new(32,80),new(200,40),new(90,88),new(152,32)} : [new(72,80),new(176,40),new(48,40),new(112,80)])[Slot.Value];
    public PixelPoint Center(bool doubles, bool animationTarget = false)
    {
        var p = Base(doubles);
        int y = (p.Y + YOffset - (Slot.IsPlayer ? 0 : Elevation)) & 255;
        if (animationTarget) y = Math.Min(104, y + (Slot.IsPlayer ? 8 : 0));
        return new(p.X,y);
    }
}
public sealed record CommittedMoveAnimation(string BattleId, long Sequence, int MoveId, AnimationSlot Actor, AnimationSlot Target);
public sealed record SourceAnimationCommand(string Op, string[] Args, int Line);
public sealed record SourceSpriteAnimFrame(int Tile,int Duration,bool HFlip,bool VFlip);
public sealed record SourceSpriteRecipe(string Template,string Asset,string Callback,bool Blend,SourceSpriteAnimFrame[][] Animations);
public sealed record SourceAnimationScript(int MoveId, string Label, SourceAnimationCommand[] Commands)
{
    public int[] SineTable {get;init;} = [];
    public IReadOnlyDictionary<string,SourceSpriteRecipe> SpriteRecipes {get;init;} = new Dictionary<string,SourceSpriteRecipe>();
}
public sealed record SourceAnimationSound(string Id, int Pan, int Frame);
public sealed record SourceAnimationDiagnostic(int MoveId, string Script, int SourceLine, string Operation, string Reason);
public sealed class SourceAnimationException(SourceAnimationDiagnostic diagnostic) : Exception(JsonSerializer.Serialize(diagnostic))
{
    public SourceAnimationDiagnostic Diagnostic { get; } = diagnostic;
}
public interface ISourceAnimationAudio
{
    void Play(SourceAnimationSound cue);
    bool IsPlaying { get; }
    void StopAnimationEffects();
}
public sealed record HitSplatFrame(PixelPoint Center, int Scale, string Asset = "impact");
public sealed record SourceVisualSpriteFrame(int Slot,string Template,PixelPoint Position,PixelPoint Offset,int Tile,bool Visible,bool HFlip,bool Blend,int Scale,string? Asset,int Subpriority)
{
    public int OamRank{get;init;}
}
public sealed record SourceAnimationFrame(int Frame, bool Active, PixelPoint[] Offsets, int[] BackgroundSlots,
    int AlphaA, int AlphaB, HitSplatFrame[] HitSplats, string[] Trace)
{
    public SourceVisualSpriteFrame[] Sprites {get;init;} = [];
    public bool BlendEnabled {get;init;}
    public bool Doubles {get;init;}
    public int[] BattlerOamRanks {get;init;} = [];
    public PixelPoint[] ObjectOffsets {get;init;} = new PixelPoint[4];
}

/// <summary>Translated bounded original scripts/callbacks. No gameplay state or RNG.</summary>
public sealed partial class SourceBattleAnimation
{
    public const double SourceFrameSeconds=280896d/16777216;
    public static readonly ReadOnlyCollection<string> SupportedCommands=Array.AsReadOnly(new[]{"loadspritegfx","monbg","setalpha","playsewithpan","createsprite","createvisualtask","delay","waitforvisualfinish","clearmonbg","blendoff","end"});
    private readonly SourceAnimationScript _script;
    private readonly bool _doubles;
    private readonly int _battlerCount;
    private readonly CommittedMoveAnimation _cue;
    private readonly Dictionary<int,AnimationBattler> _battlers;
    private readonly ISourceAnimationAudio _audio;
    private readonly List<VisualSprite> _sprites=[];
    private readonly List<VisualTask> _tasks=[];
    private readonly HashSet<int> _backgrounds=[];
    private readonly PixelPoint[] _offsets=new PixelPoint[4];
    private readonly PixelPoint[] _objectOffsets=new PixelPoint[4];
    private readonly List<string> _trace=[];
    private int _pc,_frame=-1,_wait,_alphaA,_alphaB,_clearTicks=-1,_soundWait;
    private bool _waitCallback,_cancelled,_blendEnabled;
    private readonly HashSet<string> _loaded=[];
    private int[] _oamOrder=Enumerable.Range(0,64).ToArray();
    private readonly int[] _oamY=Enumerable.Repeat(160,64).ToArray();
    public bool Active{get;private set;}=true;
    public CommittedMoveAnimation Cue=>_cue;
    public SourceBattleAnimation(SourceAnimationScript script,CommittedMoveAnimation cue,IEnumerable<AnimationBattler> battlers,bool doubles,ISourceAnimationAudio audio)
    {
        _script=script;_cue=cue;_audio=audio;_doubles=doubles;_battlerCount=doubles?4:2;_battlers=battlers.ToDictionary(x=>x.Slot.Value);
        if(_battlers.Count!=_battlerCount||Enumerable.Range(0,_battlerCount).Any(i=>!_battlers.ContainsKey(i)))throw new InvalidDataException("Animation needs every canonical battle position.");
        if(script.MoveId!=cue.MoveId||!_battlers.ContainsKey(cue.Actor.Value)||!_battlers.ContainsKey(cue.Target.Value))throw new ArgumentException("Animation participants/script differ.");
        if(script.Commands.Length is <1 or >32||script.Commands[^1].Op!="end")throw new InvalidDataException("Unbounded or unterminated script.");
        foreach(var command in script.Commands)Validate(command);
    }
    private void Fail(SourceAnimationCommand c,string reason)=>throw new SourceAnimationException(new(_script.MoveId,_script.Label,c.Line,c.Op,reason));
    private void Validate(SourceAnimationCommand c)
    {
        if(!SupportedCommands.Contains(c.Op))Fail(c,"No accepted source adapter.");
        int count=c.Op switch{"loadspritegfx" or "monbg" or "clearmonbg" or "delay"=>1,"setalpha" or "playsewithpan"=>2,"createsprite"=>c.Args.FirstOrDefault() switch
        {"gBasicHitSplatSpriteTemplate" or "gScratchSpriteTemplate"=>7,"gHorizontalLungeSpriteTemplate"=>5,"gCuttingSliceSpriteTemplate" or "gSlashSliceSpriteTemplate"=>6,"gFalseSwipeSliceSpriteTemplate"=>3,"gFalseSwipePositionedSliceSpriteTemplate"=>4,"gSlideMonToOffsetSpriteTemplate" or "gFistFootSpriteTemplate"=>8,"gSlideMonToOriginalPosSpriteTemplate"=>6,_=>-1},"createvisualtask"=>7,_=>0};
        if(c.Args.Length!=count)Fail(c,"Command argument shape differs.");
        if(c.Op=="loadspritegfx"&&c.Args[0] is not ("ANIM_TAG_IMPACT" or "ANIM_TAG_SCRATCH" or "ANIM_TAG_CUT" or "ANIM_TAG_SLASH" or "ANIM_TAG_SLASH_2" or "ANIM_TAG_HANDS_AND_FEET"))Fail(c,"Unadmitted source graphics tag.");
        if(c.Op is "monbg" or "clearmonbg"&&c.Args[0] is not ("ANIM_TARGET" or "ANIM_DEF_PARTNER"))Fail(c,"Only target/defender-partner monbg is admitted.");
        if(c.Op=="createsprite"&&c.Args[0] is not ("gBasicHitSplatSpriteTemplate" or "gHorizontalLungeSpriteTemplate" or "gSlideMonToOffsetSpriteTemplate" or "gSlideMonToOriginalPosSpriteTemplate"))
        {
            if(!_script.SpriteRecipes.TryGetValue(c.Args[0],out var recipe)||recipe.Callback is not ("AnimSpriteOnMonPos" or "AnimCuttingSlice" or "AnimSlashSlice" or "AnimFalseSwipeSlice" or "AnimFalseSwipePositionedSlice" or "AnimBasicFistOrFoot"))Fail(c,"Unknown sprite callback recipe.");
        }
        if(c.Op=="createvisualtask"&&c.Args[0] is not ("AnimTask_ShakeMon" or "AnimTask_ShakeMon2" or "AnimTask_TranslateMonEllipticalRespectSide"))Fail(c,"Unknown task callback.");
        if(c.Op=="playsewithpan"&&(c.Args[0] is not ("SE_M_DOUBLE_SLAP" or "SE_M_COMET_PUNCH" or "SE_M_SCRATCH" or "SE_M_CUT" or "SE_M_RAZOR_WIND" or "SE_M_VITAL_THROW" or "SE_M_DOUBLE_TEAM" or "SE_M_VITAL_THROW2")||c.Args[1] is not ("SOUND_PAN_TARGET" or "SOUND_PAN_ATTACKER")))Fail(c,"Unknown sound/pan command.");
        ValidateOperands(c);
    }
    private static int Number(string s)=>s switch{"ANIM_ATTACKER"=>0,"ANIM_TARGET"=>1,_=>int.Parse(s,System.Globalization.CultureInfo.InvariantCulture)};
    private AnimationSlot Resolve(string s)=>Number(s) switch{0=>_cue.Actor,1=>_cue.Target,_=>throw new InvalidDataException("Unknown animation participant.")};
    public SourceAnimationFrame Snapshot()=>new(_frame,Active,(PixelPoint[])_offsets.Clone(),[.._backgrounds.Order()],_alphaA,_alphaB,[.._sprites.OfType<HitSplat>().OrderBy(x=>x.Slot).Select(x=>new HitSplatFrame(x.Center,x.Scale))],[.._trace]){BlendEnabled=_blendEnabled,Doubles=_doubles,BattlerOamRanks=Enumerable.Range(0,_battlerCount).Select(i=>Array.IndexOf(_oamOrder,i)).ToArray(),ObjectOffsets=(PixelPoint[])_objectOffsets.Clone(),Sprites=[.._sprites.OrderBy(x=>x.Slot).Select(x=>x.Frame with{OamRank=Array.IndexOf(_oamOrder,x.Slot)})]};
    public SourceAnimationFrame Tick()
    {
        if(!Active)throw new InvalidOperationException("Animation already ended.");
        _frame++;_trace.Clear();
        // callback1 then BattleMainCB2: AnimateSprites, BuildOamBuffer, RunTasks.
        if(_waitCallback){if(_wait<=0){_waitCallback=false;_wait=0;}else _wait--;}
        else RunScript();
        foreach(var sprite in _sprites.OrderBy(x=>x.Slot).ToArray()){sprite.Step(this);if(sprite.Finished)_sprites.Remove(sprite);}
        for(int i=0;i<_battlerCount;i++)
        {
            var b=_battlers[i];if(!b.Visible){_objectOffsets[i]=default;continue;}
            var center=b.DisplayCenter??b.Center(_doubles);var p=_offsets[i];
            int x=((center.X+p.X-32)&511)-center.X+32;if(x>255)x-=512;
            int y=((center.Y+p.Y-32)&255)-center.Y+32;
            _objectOffsets[i]=new(x,y);
        }
        SortSourceSprites();
        foreach(var task in _tasks.ToArray()){task.Step(this);if(task.Finished)_tasks.Remove(task);}
        if(_clearTicks>=0&&++_clearTicks==2){_clearTicks=-1;_backgrounds.Clear();_trace.Add("monbg-cleared");}
        return Snapshot();
    }
    private void SortSourceSprites()
    {
        // Source UpdateOamCoords/BuildSpritePriorities/SortSprites run after sprite
        // callbacks and before tasks. Stable ties retain the preceding frame's OAM order.
        int[] priorities=Enumerable.Repeat(1023,64).ToArray();
        for(int i=0;i<64;i++)
        {
            if(_battlers.TryGetValue(i,out var b)&&b.Visible)
            {
                var center=b.DisplayCenter??b.Center(_doubles);var offset=_offsets[i];
                priorities[i]=512+b.Subpriority;
                if(!_doubles||!_backgrounds.Contains(i)||_clearTicks>=0)_oamY[i]=(center.Y+offset.Y-32)&255;
                continue;
            }
            var sprite=_sprites.FirstOrDefault(s=>s.Slot==i);
            if(sprite is null){_oamY[i]=160;continue;}
            var f=sprite.Frame;priorities[i]=(f.Asset is null?768:512)+f.Subpriority;
            if(f.Visible)_oamY[i]=(f.Position.Y+f.Offset.Y-16)&255;
        }
        _oamOrder=_oamOrder.OrderBy(i=>priorities[i]).ThenByDescending(i=>_oamY[i]>=160?_oamY[i]-256:_oamY[i]).ToArray();
    }
    private void Wait(int frames){_wait=frames==0?-1:frames;_waitCallback=true;}
    private void RunScript()
    {
        int budget=64;
        while(Active&&budget-->0)
        {
            if(_pc>=_script.Commands.Length)throw new InvalidDataException("Script fell through without end.");
            var c=_script.Commands[_pc];var a=c.Args;
            switch(c.Op)
            {
                case "loadspritegfx":_loaded.Add(a[0]);_pc++;Wait(1);return;
                case "monbg":
                    if(_battlers[_cue.Target.Value].Visible)_backgrounds.Add(_cue.Target.Value);
                    if(_doubles&&_battlers[_cue.Target.Partner.Value].Visible)_backgrounds.Add(_cue.Target.Partner.Value);
                    break;
                case "setalpha":_blendEnabled=true;_alphaA=Math.Clamp(Number(a[0]),0,16);_alphaB=Math.Clamp(Number(a[1]),0,16);break;
                case "createsprite":
                    int slot=Enumerable.Range(0,64).First(i=>(!_battlers.TryGetValue(i,out var reserved)||!reserved.Visible)&&_sprites.All(s=>s.Slot!=i));
                    _oamY[slot]=a[0] is "gHorizontalLungeSpriteTemplate" or "gSlideMonToOffsetSpriteTemplate" or "gSlideMonToOriginalPosSpriteTemplate"?160:0;
                    int modifier=Number(a[2]);int priority=Math.Max(3,_battlers[Resolve(a[1]).Value].Subpriority+(modifier>=64?modifier-64:-modifier));
                    if(a[0]=="gBasicHitSplatSpriteTemplate")
                    {
                        var target=Resolve(a[5]);var center=_battlers[target.Value].Center(_doubles,true);
                        int x=Number(a[3])*InitialXDirection();
                        if(!_loaded.Contains("ANIM_TAG_IMPACT"))Fail(c,"Impact graphics have not been loaded.");
                        _sprites.Add(new HitSplat(slot,priority,new(center.X+x,center.Y+Number(a[4])),Number(a[6])));
                    }
                    else if(a[0]=="gHorizontalLungeSpriteTemplate")_sprites.Add(new Lunge(slot,priority,_battlers[_cue.Target.Value].Center(_doubles,true),_cue.Actor,Number(a[3]),Number(a[4])));
                    else if(a[0] is "gSlideMonToOffsetSpriteTemplate" or "gSlideMonToOriginalPosSpriteTemplate")_sprites.Add(new SlideSprite(slot,priority,a[0],a.Skip(3).Select(Number).ToArray(),this));
                    else
                    {
                        var recipe=_script.SpriteRecipes[a[0]];
                        if(!_loaded.Contains("ANIM_TAG_"+recipe.Asset.ToUpperInvariant().Replace('-','_')))Fail(c,"Sprite graphics have not been loaded.");
                        _sprites.Add(new AnimatedSprite(slot,priority,recipe,a.Skip(3).Select(Number).ToArray(),this));
                    }
                    break;
                case "createvisualtask":
                    if(a[0]=="AnimTask_TranslateMonEllipticalRespectSide")
                    {var ellipse=new EllipseTask(Resolve(a[2]),Number(a[3])*(_cue.Actor.IsPlayer?1:-1),Number(a[4]),Number(a[5]),Number(a[6]));ellipse.Step(this);if(!ellipse.Finished)_tasks.Add(ellipse);break;}
                    var shake=new ShakeTask(Resolve(a[2]),Number(a[3]),Number(a[4]),Number(a[5]),Number(a[6]),a[0]=="AnimTask_ShakeMon2");
                    _offsets[shake.Slot.Value]=new(shake.X,shake.Y);shake.Step(this);_tasks.Add(shake);break;
                case "playsewithpan":_audio.Play(new(a[0],AdjustPanning(a[1]=="SOUND_PAN_ATTACKER"?-64:63),_frame));break;
                case "delay":_pc++;Wait(Number(a[0]));return;
                case "waitforvisualfinish":if(_sprites.Count!=0||_tasks.Count!=0)return;break;
                case "clearmonbg":_clearTicks=0;break;
                case "blendoff":_blendEnabled=false;_alphaA=_alphaB=0;break;
                case "end":
                    if(_sprites.Count!=0||_tasks.Count!=0||_backgrounds.Count!=0){_soundWait=0;return;}
                    if(_audio.IsPlaying&&++_soundWait<=90)return;
                    if(_audio.IsPlaying)_audio.StopAnimationEffects();
                    _soundWait=0;Active=false;_loaded.Clear();return;
                default:Fail(c,"Unimplemented source operation.");break;
            }
            _trace.Add($"{c.Line}:{c.Op}");_pc++;
        }
        if(budget<=0)throw new InvalidDataException("Source script exceeded per-frame instruction bound.");
    }
    public void Cancel()
    {
        if(_cancelled)return;_cancelled=true;Active=false;
        _audio.StopAnimationEffects();_sprites.Clear();_tasks.Clear();_backgrounds.Clear();Array.Clear(_offsets);Array.Clear(_objectOffsets);_alphaA=_alphaB=0;_blendEnabled=false;_clearTicks=-1;
    }
    private abstract class VisualSprite(int slot,int subpriority)
    {
        public int Slot{get;}=slot;public int Subpriority{get;}=subpriority;
        public bool Finished{get;protected set;}
        public abstract SourceVisualSpriteFrame Frame{get;}
        public abstract void Step(SourceBattleAnimation owner);
    }
    private sealed class HitSplat:VisualSprite
    {
        public PixelPoint Center{get;}public int Scale{get;}
        private int _state,_delay;
        public override SourceVisualSpriteFrame Frame=>new(Slot,"gBasicHitSplatSpriteTemplate",Center,default,0,true,false,true,Scale,"impact",Subpriority);
        public HitSplat(int slot,int subpriority,PixelPoint center,int affine):base(slot,subpriority)
        {
            Center=center;Scale=affine switch{0=>256,1=>216,2=>176,3=>128,_=>throw new InvalidDataException("Unknown source affine animation.")};
            // CreateSpriteAndAnimate already executes callback and BeginAffineAnim once.
            if(affine==0){_state=1;_delay=7;}
        }
        public override void Step(SourceBattleAnimation owner)
        {
            // Sprite callback precedes each affine step. End -> swap callback -> destroy
            // happen on separate source frames, not when the hold counter reaches zero.
            switch(_state){case 0:_state=1;_delay=7;break;case 1:if(_delay>0)_delay--;else _state=2;break;case 2:_state=3;break;case 3:Finished=true;break;}
        }
    }
    private sealed class Lunge(int spriteSlot,int subpriority,PixelPoint position,AnimationSlot slot,int duration,int delta):VisualSprite(spriteSlot,subpriority)
    {
        public override SourceVisualSpriteFrame Frame=>new(Slot,"gHorizontalLungeSpriteTemplate",position,default,0,false,false,false,256,null,Subpriority);
        private readonly int _duration=duration;
        private int _remaining=duration,_delta=slot.IsPlayer?delta:-delta,_state;
        public override void Step(SourceBattleAnimation owner)
        {
            if(_state is 0 or 2)
            {
                if(_remaining>0){_remaining--;var p=owner._offsets[slot.Value];owner._offsets[slot.Value]=new(p.X+_delta,p.Y);}
                else _state++;
            }
            else if(_state==1){_remaining=_duration;_delta=-_delta;_state=2;}
            else Finished=true;
        }
    }
    // Source non-affine frame engine and callback chains. Recipes carry the authenticated
    // original template, frame tiles/durations and OBJ blend mode; move IDs choose no behavior.
    private sealed class AnimatedSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;
        private readonly SourceSpriteAnimFrame[] _animation;
        private PixelPoint _position,_offset;
        private int _animIndex,_animDelay,_state,_counter,_flickers,_vx,_vy,_fx,_fy,_direction;
        private bool _ended,_invisible,_hFlip;
        public AnimatedSprite(int slot,int subpriority,SourceSpriteRecipe recipe,int[] args,SourceBattleAnimation owner):base(slot,subpriority)
        {
            _recipe=recipe;int anim=recipe.Callback=="AnimBasicFistOrFoot"?args[4]:recipe.Callback=="AnimFalseSwipePositionedSlice"?1:0;
            if(recipe.Animations.Length<=anim||recipe.Animations[anim].Length==0)throw new InvalidDataException("Source sprite frame recipe missing.");
            _animation=recipe.Animations[anim];
            var target=owner._battlers[owner._cue.Target.Value];var center=target.Center(owner._doubles,true);
            switch(recipe.Callback)
            {
                case "AnimBasicFistOrFoot":
                    var fistMon=owner._battlers[(args[3]==0?owner._cue.Actor:owner._cue.Target).Value];var fistCenter=fistMon.Center(owner._doubles,true);
                    int direction=owner.InitialXDirection();
                    _position=new(fistCenter.X+direction*args[0],fistCenter.Y+args[1]);_counter=args[2];break;
                case "AnimSpriteOnMonPos":
                    var mon=owner._battlers[(args[2]==0?owner._cue.Actor:owner._cue.Target).Value];
                    var p=args[3]==0?mon.Center(owner._doubles,true):mon.Base(owner._doubles);
                    _position=new(p.X+(owner._cue.Actor.IsPlayer?args[0]:-args[0]),p.Y+args[1]);break;
                case "AnimCuttingSlice":
                    _direction=args[2];_position=new(center.X+(_direction==0?args[0]:-args[0]),target.Base(owner._doubles).Y+(target.Slot.IsPlayer?8:0)+args[1]);
                    _vx=_direction==0?-1024:1024;_vy=1024;_hFlip=_direction!=0;break;
                case "AnimSlashSlice":
                    var slashMon=owner._battlers[(args[0]==0?owner._cue.Actor:owner._cue.Target).Value];var slashCenter=slashMon.Center(owner._doubles,true);
                    _position=new(slashCenter.X+args[1],slashCenter.Y+args[2]);break;
                case "AnimFalseSwipeSlice":_position=new(center.X-48,center.Y);break;
                case "AnimFalseSwipePositionedSlice":_position=new(center.X-48+args[0],center.Y);_state=4;break;
                default:throw new InvalidDataException("Unsupported source sprite callback.");
            }
            // CreateSpriteAndAnimate invokes initializer then BeginAnim immediately.
            _animDelay=_animation[0].Duration-1;
        }
        public override SourceVisualSpriteFrame Frame=>new(Slot,_recipe.Template,_position,_offset,_animation[_animIndex].Tile,!_invisible,_hFlip^_animation[_animIndex].HFlip,_recipe.Blend,256,_recipe.Asset,Subpriority);
        public override void Step(SourceBattleAnimation owner)
        {
            switch(_recipe.Callback)
            {
                case "AnimBasicFistOrFoot":if(_state==1)Finished=true;else if(_counter>0)_counter--;else _state=1;break;
                case "AnimSpriteOnMonPos":if(_ended)Finished=true;break;
                case "AnimCuttingSlice":
                    if(_state==0)
                    {
                        _fx=(short)(_fx+_vx);_fy=(short)(_fy+_vy);_vx+=_direction==0?24:-24;_vy-=24;
                        _offset=new(_fx>>8,_fy>>8);
                        if(++_counter==20){_state=1;_counter=3;}
                    }
                    else if(_state==1){if(_counter>0)_counter--;else _state=2;}
                    else Finished=true;
                    break;
                default:
                    switch(_state)
                    {
                        case 0:if(_ended)_state=_recipe.Callback=="AnimFalseSwipeSlice"?1:4;break;
                        case 1:if(++_counter>8){_counter=12;_state=2;}break;
                        case 2:if(_counter>0){_counter--;_offset=new(_offset.X+8,_offset.Y);}else _state=3;break;
                        case 3:_counter=0;_flickers=0;_state=4;break;
                        case 4:if(++_counter>1){_counter=0;_invisible=!_invisible;if(++_flickers>8)Finished=true;}break;
                    }
                    break;
            }
            if(Finished)return;
            if(_animDelay>0)_animDelay--;
            else if(_animIndex+1<_animation.Length){_animIndex++;_animDelay=_animation[_animIndex].Duration-1;}
            else _ended=true;
        }
    }
    private sealed class ShakeTask(AnimationSlot slot,int x,int y,int count,int delay,bool negative):VisualTask
    {
        public AnimationSlot Slot=>slot;public int X=>x;public int Y=>y;
        private readonly int _delay=delay;
        private int _count=count,_timer=delay;
        public override void Step(SourceBattleAnimation owner)
        {
            if(Finished)return;
            if(_timer==0)
            {
                var p=owner._offsets[slot.Value];owner._offsets[slot.Value]=new(negative?(p.X==x?-x:x):(p.X==0?x:0),negative?(p.Y==y?-y:y):(p.Y==0?y:0));_timer=_delay;
                if(--_count==0){owner._offsets[slot.Value]=default;Finished=true;}
            }
            else _timer--;
        }
    }
}

public sealed class SourceBattleAnimationGate
{
    private readonly HashSet<(string,long)> _started=[];
    public SourceBattleAnimation? Current{get;private set;}
    public bool IsBlocking=>Current?.Active==true;
    public bool TryStart(CommittedMoveAnimation cue,Func<SourceBattleAnimation> create)
    {
        if(IsBlocking||_started.Contains((cue.BattleId,cue.Sequence)))return false;
        var candidate=create();_started.Add((cue.BattleId,cue.Sequence));Current=candidate;return true;
    }
    public void Reset(){Current?.Cancel();Current=null;_started.Clear();}
}

public static class SourceHitAnimationCatalog
{
    public const string ManifestSha256="4df0c983acec0dea02f80b5a00de218ec4de65707ddd87e5c6ab83f38017382e";
    public static byte[] ReadBounded(string path,int maximum,int? exact=null)
    {
        using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        long length=file.Length;
        if(length<0||length>maximum||exact is {} n&&length!=n)throw new InvalidDataException("Source asset length differs: "+path);
        var bytes=new byte[(int)length];file.ReadExactly(bytes);
        if(file.ReadByte()!=-1)throw new InvalidDataException("Source asset changed while reading.");return bytes;
    }
    public static JsonDocument Read(string folder)
    {
        var bytes=ReadBounded(Path.Combine(folder,"manifest.json"),65536,37781);
        if(Convert.ToHexStringLower(SHA256.HashData(bytes))!=ManifestSha256)throw new InvalidDataException("Source animation manifest differs.");
        return JsonDocument.Parse(bytes);
    }
    public static SourceAnimationScript Script(JsonDocument manifest,int move)
    {
        var row=manifest.RootElement.GetProperty("scripts").EnumerateArray().Single(x=>x.GetProperty("moveId").GetInt32()==move);
        var recipes=manifest.RootElement.GetProperty("spriteRecipes").EnumerateArray().Select(x=>new SourceSpriteRecipe(x.GetProperty("template").GetString()!,x.GetProperty("asset").GetString()!,x.GetProperty("callback").GetString()!,x.GetProperty("blend").GetBoolean(),x.GetProperty("animations").EnumerateArray().Select(a=>a.EnumerateArray().Select(f=>new SourceSpriteAnimFrame(f.GetProperty("tile").GetInt32(),f.GetProperty("duration").GetInt32(),f.GetProperty("hFlip").GetBoolean(),f.GetProperty("vFlip").GetBoolean())).ToArray()).ToArray())).ToDictionary(x=>x.Template);
        return new(move,row.GetProperty("label").GetString()!,row.GetProperty("commands").EnumerateArray().Select(x=>new SourceAnimationCommand(x.GetProperty("op").GetString()!,x.GetProperty("args").EnumerateArray().Select(a=>a.GetString()!).ToArray(),x.GetProperty("line").GetInt32())).ToArray()){SpriteRecipes=recipes,SineTable=manifest.RootElement.GetProperty("sineTable").EnumerateArray().Select(x=>x.GetInt32()).ToArray()};
    }
    public static SourcePixelAsset Asset(JsonDocument manifest,string folder,string id)
    {
        var row=manifest.RootElement.GetProperty("assets").EnumerateArray().Single(x=>x.GetProperty("id").GetString()==id);
        string path=row.GetProperty("path").GetString()!;
        if(Path.GetFileName(path)!=path)throw new InvalidDataException("Asset path differs.");
        int size=row.GetProperty("bytes").GetInt32();
        var bytes=ReadBounded(Path.Combine(folder,path),10244,size);
        if(Convert.ToHexStringLower(SHA256.HashData(bytes))!=row.GetProperty("sha256").GetString())throw new InvalidDataException("Source animation asset differs.");
        int width=System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes),height=System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2));
        if(width!=32||height is not (32 or 128 or 160)||size!=4+width*height*2)throw new InvalidDataException("Impact shape differs.");
        var pixels=new ushort[width*height];for(int i=0;i<pixels.Length;i++)pixels[i]=System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4+i*2));
        return new(width,height,pixels);
    }
}
